using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class IndexPage : OperationPageBase
{
    private readonly MboxSourceControl _source = new();
    private readonly ComboBox _installations = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _detect = new() { Text = "Detectar Thunderbird", AutoSize = true };
    private readonly Button _manual = new() { Text = "Selecionar EXE", AutoSize = true };
    private readonly TextBox _output = CreatePathTextBox();
    private readonly Button _browseOutput = CreateBrowseButton();
    private readonly TextBox _mailbox = new() { Width = 220 };
    private readonly NumericUpDown _timeout = new() { Minimum = 5, Maximum = 1440, Value = 360, Width = 90 };
    private readonly CheckBox _automation = new() { Text = "Selecionar pasta automaticamente", AutoSize = true, Checked = true };
    private readonly CheckBox _keepProfile = new() { Text = "Preservar perfil temporário para diagnóstico", AutoSize = true };
    private readonly Button _run = new() { Text = "Criar índice pelo Thunderbird", AutoSize = true };

    public IndexPage()
    {
        Dock = DockStyle.Fill;
        _output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Thunderbird Recovery Suite", "Indices");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(_source, 0, 0);

        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.Controls.Add(new Label { Text = "Instalação:", AutoSize = true }, 0, 0);
        form.Controls.Add(_installations, 1, 0);
        form.Controls.Add(_detect, 2, 0);
        form.Controls.Add(_manual, 3, 0);
        form.Controls.Add(new Label { Text = "Destino:", AutoSize = true }, 0, 1);
        form.Controls.Add(_output, 1, 1);
        form.SetColumnSpan(_output, 2);
        form.Controls.Add(_browseOutput, 3, 1);
        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        flow.Controls.Add(new Label { Text = "Nome da caixa:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        flow.Controls.Add(_mailbox);
        flow.Controls.Add(new Label { Text = "Timeout (min):", AutoSize = true, Margin = new Padding(12, 7, 3, 3) });
        flow.Controls.Add(_timeout);
        flow.Controls.Add(_automation);
        flow.Controls.Add(_keepProfile);
        form.Controls.Add(flow, 0, 2);
        form.SetColumnSpan(flow, 4);
        root.Controls.Add(form, 0, 1);
        root.Controls.Add(_run, 0, 2);
        root.Controls.Add(CreateOperationFooter(), 0, 3);
        Controls.Add(root);

        _detect.Click += (_, _) => Detect();
        _manual.Click += (_, _) => SelectManual();
        _browseOutput.Click += (_, _) => { var selected = SelectFolder(this, "Selecione a pasta de entrega do índice", _output.Text); if (selected.Length > 0) _output.Text = selected; };
        _run.Click += async (_, _) => await IndexAsync();
        _source.Leave += (_, _) => { if (string.IsNullOrWhiteSpace(_mailbox.Text)) _mailbox.Text = _source.MailboxName; };
        Detect();
    }

    private void Detect()
    {
        var installations = ThunderbirdLocator.FindInstallations().ToList();
        _installations.DisplayMember = nameof(ThunderbirdInstallation.DisplayText);
        _installations.DataSource = installations;
        AppendLog(installations.Count == 0 ? "Nenhuma instalação do Thunderbird foi detectada." : $"{installations.Count} instalação(ões) detectada(s).");
    }

    private void SelectManual()
    {
        using var dialog = new OpenFileDialog { Filter = "Thunderbird|thunderbird.exe|Executáveis|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var installation = ThunderbirdLocator.FromExecutable(dialog.FileName);
        var list = (_installations.DataSource as List<ThunderbirdInstallation>) ?? [];
        list = list.Where(item => !item.ExecutablePath.Equals(installation.ExecutablePath, StringComparison.OrdinalIgnoreCase)).ToList();
        list.Insert(0, installation);
        _installations.DataSource = list;
        _installations.SelectedIndex = 0;
    }

    private async Task IndexAsync()
    {
        await RunOperationAsync(_run, async token =>
        {
            if (_installations.SelectedItem is not ThunderbirdInstallation installation)
                throw new InvalidOperationException("Selecione uma instalação do Thunderbird.");
            var source = await _source.GetSelectionAsync(token);
            var mailbox = MailboxNameResolver.FromSource(string.IsNullOrWhiteSpace(_mailbox.Text) ? _source.MailboxName : _mailbox.Text);
            var options = new ThunderbirdIndexOptions
            {
                Source = source,
                Installation = installation,
                OutputDirectory = _output.Text,
                MailboxName = mailbox,
                Timeout = TimeSpan.FromMinutes((double)_timeout.Value),
                StablePeriod = TimeSpan.FromSeconds(30),
                TryUiAutomation = _automation.Checked,
                KeepTemporaryProfile = _keepProfile.Checked,
                CloseThunderbirdAfterIndex = true
            };
            var progress = new Progress<ThunderbirdIndexProgress>(value =>
            {
                ReportProgress(value.ProcessedBytes, value.TotalBytes, value.Detail);
                if (value.ProcessedBytes == 0) AppendLog(value.Stage + ": " + value.Detail);
            });
            var result = await ThunderbirdIndexService.CreateIndexAsync(options, progress, token);
            StatusLabel.Text = result.MsfValidated ? "Índice MSF validado." : "Indexação concluída em modo Panorama/SQLite.";
            AppendLog($"MBOX: {result.MboxPath}");
            AppendLog($"MSF: {result.MsfPath ?? "não produzido"}; mensagens índice: {result.IndexedMessages?.ToString("N0") ?? "não disponível"}.");
            if (!string.IsNullOrWhiteSpace(result.Warning)) AppendLog("AVISO: " + result.Warning);
            MessageBox.Show(this, $"Indexação concluída em:\n{result.OutputDirectory}\n\n{result.Warning}", "Indexação concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }
}
