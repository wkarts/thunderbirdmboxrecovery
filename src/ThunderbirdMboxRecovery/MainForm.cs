using System.Diagnostics;
using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery;

public sealed class MainForm : Form
{
    private readonly TextBox _sourceText = new();
    private readonly Button _browseSourceButton = new();
    private readonly TextBox _passwordText = new();
    private readonly Button _analyzeButton = new();
    private readonly ComboBox _archiveEntries = new();
    private readonly TextBox _outputText = new();
    private readonly Button _browseOutputButton = new();
    private readonly CheckBox _splitOutputCheck = new();
    private readonly NumericUpDown _chunkSize = new();
    private readonly CheckBox _createMsfCheck = new();
    private readonly Button _startButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _openOutputButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly Label _statusLabel = new();
    private readonly Label _summaryLabel = new();
    private readonly TextBox _logText = new();

    private CancellationTokenSource? _cancellation;
    private long _expectedInputBytes;
    private string? _lastOutputDirectory;
    private bool _running;

    public MainForm()
    {
        Text = "Thunderbird MBOX Recovery";
        Width = 980;
        Height = 720;
        MinimumSize = new Size(860, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        AllowDrop = true;

        BuildInterface();
        WireEvents();
        UpdateArchiveControls();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4,
            AutoSize = false
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var header = new Panel { Dock = DockStyle.Top, Height = 72 };
        var title = new Label
        {
            Text = "Recuperação de caixas MBOX do Thunderbird",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            Location = new Point(0, 0)
        };
        var subtitle = new Label
        {
            Text = "Processa qualquer caixa MBOX do Thunderbird, direta ou compactada, sem alterar a origem.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(2, 38)
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        root.Controls.Add(header, 0, 0);

        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(0, 4, 0, 8)
        };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

        AddRow(settings, 0, "Arquivo MBOX ou backup:", _sourceText, _browseSourceButton);
        _browseSourceButton.Text = "Selecionar...";

        _passwordText.UseSystemPasswordChar = true;
        _passwordText.PlaceholderText = "Deixe vazio quando o backup não possuir senha";
        AddRow(settings, 1, "Senha do arquivo .7z:", _passwordText, _analyzeButton);
        _analyzeButton.Text = "Analisar backup";

        _archiveEntries.DropDownStyle = ComboBoxStyle.DropDownList;
        _archiveEntries.DisplayMember = nameof(ArchiveEntryInfo.DisplayText);
        AddRow(settings, 2, "Caixa dentro do backup:", _archiveEntries, new Label());

        AddRow(settings, 3, "Pasta de destino:", _outputText, _browseOutputButton);
        _browseOutputButton.Text = "Selecionar...";

        _splitOutputCheck.Text = "Fracionar o arquivo recuperado em várias partes";
        _splitOutputCheck.AutoSize = true;
        _splitOutputCheck.Checked = false;
        AddRow(settings, 4, "Formato da saída:", _splitOutputCheck, new Label());

        _chunkSize.DecimalPlaces = 2;
        _chunkSize.Minimum = 0.25M;
        _chunkSize.Maximum = 64.00M;
        _chunkSize.Increment = 0.25M;
        _chunkSize.Value = 1.50M;
        _chunkSize.ThousandsSeparator = true;
        _chunkSize.Enabled = false;
        var chunkPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        chunkPanel.Controls.Add(_chunkSize);
        chunkPanel.Controls.Add(new Label { Text = "GiB por parte; utilizado somente quando o fracionamento estiver marcado", AutoSize = true, Margin = new Padding(8, 6, 0, 0) });
        AddRow(settings, 5, "Tamanho das partes:", chunkPanel, new Label());

        _createMsfCheck.Text = "Criar o arquivo .msf correspondente para reconstrução pelo Thunderbird";
        _createMsfCheck.AutoSize = true;
        _createMsfCheck.Checked = true;
        AddRow(settings, 6, "Índice Thunderbird:", _createMsfCheck, new Label());

        _summaryLabel.Text = "Selecione Inbox, Sent, Drafts, Archives, Trash ou qualquer pasta MBOX personalizada.";
        _summaryLabel.AutoSize = true;
        _summaryLabel.ForeColor = SystemColors.GrayText;
        settings.Controls.Add(new Label { Text = "Análise:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 3, 8) }, 0, 7);
        settings.Controls.Add(_summaryLabel, 1, 7);
        settings.SetColumnSpan(_summaryLabel, 2);

        root.Controls.Add(settings, 0, 1);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 5,
            Padding = new Padding(0, 6, 0, 8)
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _startButton.Text = "Iniciar recuperação";
        _startButton.AutoSize = true;
        _startButton.Padding = new Padding(12, 4, 12, 4);
        _cancelButton.Text = "Cancelar";
        _cancelButton.AutoSize = true;
        _cancelButton.Padding = new Padding(12, 4, 12, 4);
        _cancelButton.Enabled = false;
        _openOutputButton.Text = "Abrir resultado";
        _openOutputButton.AutoSize = true;
        _openOutputButton.Padding = new Padding(12, 4, 12, 4);
        _openOutputButton.Enabled = false;

        actions.Controls.Add(_startButton, 0, 0);
        actions.Controls.Add(_cancelButton, 1, 0);
        actions.Controls.Add(_openOutputButton, 2, 0);
        _statusLabel.Text = "Pronto.";
        _statusLabel.AutoSize = true;
        _statusLabel.Anchor = AnchorStyles.Right;
        actions.Controls.Add(_statusLabel, 4, 0);
        root.Controls.Add(actions, 0, 2);

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _progressBar.Dock = DockStyle.Fill;
        _progressBar.Maximum = 1000;
        _logText.Dock = DockStyle.Fill;
        _logText.Multiline = true;
        _logText.ReadOnly = true;
        _logText.ScrollBars = ScrollBars.Both;
        _logText.WordWrap = false;
        _logText.Font = new Font("Consolas", 9F);
        statusPanel.Controls.Add(_progressBar, 0, 0);
        statusPanel.Controls.Add(_logText, 0, 1);
        root.Controls.Add(statusPanel, 0, 3);
    }

    private static void AddRow(TableLayoutPanel panel, int row, string labelText, Control input, Control action)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 3, 8)
        };
        input.Dock = DockStyle.Fill;
        input.Margin = new Padding(3, 4, 3, 4);
        action.Dock = DockStyle.Fill;
        action.Margin = new Padding(3, 4, 3, 4);
        panel.Controls.Add(label, 0, row);
        panel.Controls.Add(input, 1, row);
        panel.Controls.Add(action, 2, row);
    }

    private void WireEvents()
    {
        _browseSourceButton.Click += async (_, _) => await BrowseSourceAsync();
        _browseOutputButton.Click += (_, _) => BrowseOutput();
        _analyzeButton.Click += async (_, _) => await AnalyzeSourceAsync();
        _startButton.Click += async (_, _) => await StartRecoveryAsync();
        _cancelButton.Click += (_, _) => CancelRecovery();
        _openOutputButton.Click += (_, _) => OpenLastOutput();
        _sourceText.TextChanged += (_, _) => UpdateArchiveControls();
        _archiveEntries.SelectedIndexChanged += (_, _) => UpdateSelectedArchiveEntry();
        _splitOutputCheck.CheckedChanged += (_, _) => UpdateSplitControls();
        DragEnter += OnDragEnter;
        DragDrop += async (_, args) => await OnDragDropAsync(args);
        FormClosing += OnFormClosing;
    }

    private async Task BrowseSourceAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Selecione qualquer arquivo MBOX do Thunderbird ou um backup compactado",
            Filter = "Todos os arquivos (inclui MBOX sem extensão)|*.*|MBOX exportado|*.mbox|Backups compactados|*.7z;*.zip;*.rar;*.tar;*.gz;*.bz2;*.xz",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _sourceText.Text = dialog.FileName;
        await AnalyzeSourceAsync();
    }

    private void BrowseOutput()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecione a pasta onde será criada a recuperação",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _outputText.Text = dialog.SelectedPath;
    }

    private async Task AnalyzeSourceAsync()
    {
        if (_running) return;
        var source = _sourceText.Text.Trim();
        if (!File.Exists(source))
        {
            ShowWarning("Selecione um arquivo de origem existente.");
            return;
        }

        try
        {
            SetAnalyzing(true);
            _archiveEntries.DataSource = null;
            _expectedInputBytes = 0;
            var sourceInfo = new FileInfo(source);
            AppendLog($"Analisando: {source}");

            if (!ArchiveService.IsArchive(source))
            {
                MboxSourceValidator.ValidateDirectFile(source);
                _expectedInputBytes = sourceInfo.Length;
                var mailboxName = MailboxNameResolver.FromSource(source);
                _summaryLabel.Text = $"MBOX direto: {mailboxName} — {SizeFormatter.Format(sourceInfo.Length)}. A origem será aberta somente para leitura.";
                AppendLog($"Arquivo MBOX direto selecionado: {mailboxName} ({SizeFormatter.Format(sourceInfo.Length)}).");
                return;
            }

            var entries = await ArchiveService.InspectAsync(source, _passwordText.Text, CancellationToken.None);
            if (entries.Count == 0)
                throw new InvalidDataException("Nenhuma caixa MBOX provável foi encontrada dentro do arquivo compactado.");

            _archiveEntries.DataSource = entries.ToList();
            var preferredIndex = entries.ToList().FindIndex(entry =>
                string.Equals(Path.GetFileName(entry.Key.Replace('/', Path.DirectorySeparatorChar)), "Inbox", StringComparison.OrdinalIgnoreCase));
            _archiveEntries.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;
            _expectedInputBytes = ((ArchiveEntryInfo)_archiveEntries.SelectedItem!).Size;
            _summaryLabel.Text = $"Backup: {SizeFormatter.Format(sourceInfo.Length)}; caixa selecionada descompactada: {SizeFormatter.Format(_expectedInputBytes)}.";
            AppendLog($"Encontradas {entries.Count} caixas prováveis. Selecionada: {((ArchiveEntryInfo)_archiveEntries.SelectedItem!).Key}.");
        }
        catch (Exception ex)
        {
            _summaryLabel.Text = "Não foi possível analisar a origem.";
            AppendLog($"ERRO: {ex.Message}");
            MessageBox.Show(this,
                "Falha ao analisar o arquivo. Quando o .7z possuir senha, informe-a antes de analisar.\n\n" + ex.Message,
                "Falha na análise",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetAnalyzing(false);
        }
    }

    private async Task StartRecoveryAsync()
    {
        if (_running) return;

        var source = _sourceText.Text.Trim();
        var outputRoot = _outputText.Text.Trim();
        if (!File.Exists(source))
        {
            ShowWarning("Selecione um arquivo de origem existente.");
            return;
        }
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            ShowWarning("Selecione a pasta de destino.");
            return;
        }

        if (!ArchiveService.IsArchive(source))
        {
            try
            {
                MboxSourceValidator.ValidateDirectFile(source);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Origem inválida", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        if (ArchiveService.IsArchive(source) && _archiveEntries.SelectedItem is not ArchiveEntryInfo)
        {
            await AnalyzeSourceAsync();
            if (_archiveEntries.SelectedItem is not ArchiveEntryInfo) return;
        }

        var archiveEntry = _archiveEntries.SelectedItem as ArchiveEntryInfo;
        var expectedBytes = ArchiveService.IsArchive(source)
            ? archiveEntry?.Size ?? _expectedInputBytes
            : new FileInfo(source).Length;

        string recoveryDirectory;
        try
        {
            var mailboxName = MailboxNameResolver.FromSource(source, archiveEntry?.Key);
            recoveryDirectory = RecoveryCoordinator.CreateOutputDirectory(outputRoot, mailboxName);
            RecoveryCoordinator.ValidateFreeSpace(recoveryDirectory, expectedBytes);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Destino inválido", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _lastOutputDirectory = recoveryDirectory;
        _openOutputButton.Enabled = true;
        _cancellation = new CancellationTokenSource();
        SetRunning(true);
        _progressBar.Value = 0;
        AppendLog(new string('-', 90));
        AppendLog($"Destino desta execução: {recoveryDirectory}");

        var options = new RecoveryOptions
        {
            SourcePath = source,
            ArchiveEntryKey = archiveEntry?.Key,
            ArchivePassword = _passwordText.Text,
            OutputDirectory = recoveryDirectory,
            MailboxName = MailboxNameResolver.FromSource(source, archiveEntry?.Key),
            SplitOutput = _splitOutputCheck.Checked,
            CreateMsfPlaceholder = _createMsfCheck.Checked,
            TargetChunkBytes = (long)(_chunkSize.Value * 1024M * 1024M * 1024M),
            ExpectedInputBytes = expectedBytes
        };

        var progress = new Progress<RecoveryProgress>(UpdateProgress);

        try
        {
            var result = await Task.Run(
                () => new MboxSplitter().Execute(options, progress, _cancellation.Token),
                _cancellation.Token);

            AppendLog($"CONCLUÍDO: {result.Parts.Count} arquivo(s) MBOX, {result.TotalMessages:N0} mensagens estimadas.");
            AppendLog($"SHA-256 da entrada: {result.InputSha256}");
            _statusLabel.Text = "Recuperação concluída.";
            _progressBar.Value = _progressBar.Maximum;

            MessageBox.Show(this,
                $"Recuperação concluída com sucesso.\n\n" +
                $"Arquivos MBOX gerados: {result.Parts.Count}\n" +
                $"Mensagens estimadas: {result.TotalMessages:N0}\n" +
                $"Destino: {result.OutputDirectory}",
                "Recuperação concluída",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Operação cancelada.";
            AppendLog("Operação cancelada. Não importe arquivos com extensão .partial.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Falha na recuperação.";
            AppendLog($"ERRO: {ex.Message}");
            MessageBox.Show(this,
                "A recuperação não foi concluída. O arquivo original não foi alterado.\n\n" +
                ex.Message + "\n\nConsulte recuperacao.log na pasta de resultado.",
                "Falha na recuperação",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            SetRunning(false);
        }
    }

    private void UpdateProgress(RecoveryProgress value)
    {
        if (value.TotalBytes > 0)
        {
            var ratio = Math.Clamp((double)value.ProcessedBytes / value.TotalBytes, 0, 1);
            _progressBar.Value = (int)Math.Round(ratio * _progressBar.Maximum);
        }

        _statusLabel.Text = value.Detail ?? value.Stage;
        AppendLog($"{value.Stage}: {SizeFormatter.Format(value.ProcessedBytes)} / {SizeFormatter.Format(value.TotalBytes)} | " +
                  $"mensagens: {value.Messages:N0} | arquivos concluídos: {value.CompletedParts}" +
                  (string.IsNullOrWhiteSpace(value.CurrentFile) ? string.Empty : $" | atual: {value.CurrentFile}"));
    }

    private void CancelRecovery()
    {
        if (_cancellation is null) return;
        _cancelButton.Enabled = false;
        _statusLabel.Text = "Cancelando com segurança...";
        _cancellation.Cancel();
    }

    private void OpenLastOutput()
    {
        if (string.IsNullOrWhiteSpace(_lastOutputDirectory) || !Directory.Exists(_lastOutputDirectory)) return;
        Process.Start(new ProcessStartInfo
        {
            FileName = _lastOutputDirectory,
            UseShellExecute = true
        });
    }

    private void UpdateSelectedArchiveEntry()
    {
        if (_archiveEntries.SelectedItem is not ArchiveEntryInfo selected) return;
        _expectedInputBytes = selected.Size;
        var mailboxName = MailboxNameResolver.FromSource(_sourceText.Text.Trim(), selected.Key);
        _summaryLabel.Text = $"Caixa selecionada: {mailboxName} — {SizeFormatter.Format(selected.Size)} descompactados.";
    }

    private void UpdateArchiveControls()
    {
        var isArchive = ArchiveService.IsArchive(_sourceText.Text.Trim());
        _passwordText.Enabled = isArchive && !_running;
        _analyzeButton.Enabled = isArchive && !_running;
        _archiveEntries.Enabled = isArchive && !_running;
        if (!isArchive)
            _archiveEntries.DataSource = null;
    }


    private void UpdateSplitControls()
    {
        _chunkSize.Enabled = !_running && _splitOutputCheck.Checked;
        _summaryLabel.Text = _splitOutputCheck.Checked
            ? "Saída fracionada habilitada. O corte ocorrerá somente entre mensagens completas."
            : "Padrão selecionado: um único arquivo MBOX recuperado, sem fracionamento.";
    }

    private void SetAnalyzing(bool analyzing)
    {
        UseWaitCursor = analyzing;
        _analyzeButton.Enabled = !analyzing && ArchiveService.IsArchive(_sourceText.Text.Trim());
        _browseSourceButton.Enabled = !analyzing;
        _startButton.Enabled = !analyzing;
        _statusLabel.Text = analyzing ? "Analisando origem..." : "Pronto.";
    }

    private void SetRunning(bool running)
    {
        _running = running;
        _sourceText.ReadOnly = running;
        _passwordText.ReadOnly = running;
        _outputText.ReadOnly = running;
        _browseSourceButton.Enabled = !running;
        _browseOutputButton.Enabled = !running;
        _analyzeButton.Enabled = !running && ArchiveService.IsArchive(_sourceText.Text.Trim());
        _archiveEntries.Enabled = !running && ArchiveService.IsArchive(_sourceText.Text.Trim());
        _splitOutputCheck.Enabled = !running;
        _chunkSize.Enabled = !running && _splitOutputCheck.Checked;
        _createMsfCheck.Enabled = !running;
        _startButton.Enabled = !running;
        _cancelButton.Enabled = running;
    }

    private void AppendLog(string line)
    {
        if (_logText.TextLength > 2_000_000)
            _logText.Clear();
        _logText.AppendText($"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}");
    }

    private void OnDragEnter(object? sender, DragEventArgs args)
    {
        if (args.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            args.Effect = DragDropEffects.Copy;
    }

    private async Task OnDragDropAsync(DragEventArgs args)
    {
        if (_running || args.Data?.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        if (!File.Exists(files[0])) return;
        _sourceText.Text = files[0];
        await AnalyzeSourceAsync();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs args)
    {
        if (!_running) return;
        var response = MessageBox.Show(this,
            "Existe uma recuperação em andamento. Deseja cancelar e fechar?",
            "Confirmar cancelamento",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (response != DialogResult.Yes)
        {
            args.Cancel = true;
            return;
        }
        _cancellation?.Cancel();
    }

    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
