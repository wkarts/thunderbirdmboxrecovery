using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class ExplorePage : OperationPageBase
{
    private readonly MboxSourceControl _source = new();
    private readonly Button _analyze = new() { Text = "Listar mensagens do MBOX", AutoSize = true };
    private readonly Button _exportSelectedEml = new() { Text = "Extrair selecionada(s) para EML", AutoSize = true, Enabled = false };
    private readonly Button _exportAllEml = new() { Text = "Extrair todas para EML", AutoSize = true, Enabled = false };
    private readonly Button _exportJson = new() { Text = "Exportar JSON", AutoSize = true, Enabled = false };
    private readonly Button _exportCsv = new() { Text = "Exportar CSV", AutoSize = true, Enabled = false };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        MultiSelect = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
    };
    private readonly Label _summary = new() { AutoSize = true };
    private readonly Label _selectionSummary = new() { AutoSize = true, ForeColor = Color.DimGray };
    private MboxDiagnosisReport? _report;
    private MboxSourceSelection? _selection;

    public ExplorePage()
    {
        Dock = DockStyle.Fill;
        ConfigureGrid();
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        root.Controls.Add(_source, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        buttons.Controls.AddRange([_analyze, _exportSelectedEml, _exportAllEml, _exportJson, _exportCsv]);
        root.Controls.Add(buttons, 0, 1);
        root.Controls.Add(_summary, 0, 2);
        root.Controls.Add(_selectionSummary, 0, 3);
        root.Controls.Add(_grid, 0, 4);
        root.Controls.Add(CreateOperationFooter(), 0, 5);
        Controls.Add(root);

        _analyze.Click += async (_, _) => await AnalyzeAsync();
        _exportJson.Click += (_, _) => ExportReport("json");
        _exportCsv.Click += (_, _) => ExportReport("csv");
        _exportSelectedEml.Click += async (_, _) => await ExportEmlAsync(selectedOnly: true);
        _exportAllEml.Click += async (_, _) => await ExportEmlAsync(selectedOnly: false);
        _grid.SelectionChanged += (_, _) => UpdateSelectionSummary();
    }

    private void ConfigureGrid()
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MboxMessageInfo.Number), HeaderText = "#", Width = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MboxMessageInfo.Date), HeaderText = "Data", Width = 145, DefaultCellStyle = new DataGridViewCellStyle { Format = "g" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MboxMessageInfo.From), HeaderText = "De", Width = 240 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MboxMessageInfo.To), HeaderText = "Para", Width = 240 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MboxMessageInfo.Subject), HeaderText = "Assunto", Width = 340 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(MboxMessageInfo.IsDeleted), HeaderText = "Excluída", Width = 70 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(MboxMessageInfo.HasAttachment), HeaderText = "Anexo", Width = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MboxMessageInfo.SizeBytes), HeaderText = "Bytes", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MboxMessageInfo.MessageId), HeaderText = "Message-ID", Width = 260 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MboxMessageInfo.StartOffset), HeaderText = "Offset inicial", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
    }

    private async Task AnalyzeAsync()
    {
        await RunOperationAsync(_analyze, async token =>
        {
            _selection = await _source.GetSelectionAsync(token);
            AppendLog("Explorando " + _selection.DisplayName);
            var progress = new Progress<MboxParseProgress>(value =>
                ReportProgress(value.ProcessedBytes, value.TotalBytes, $"Lidas {value.Messages:N0} mensagens."));
            _report = await MboxAnalyzer.AnalyzeAsync(
                _selection,
                new MboxAnalysisOptions { MaxMessagesInMemory = Environment.Is64BitProcess ? 100_000 : 25_000 },
                progress,
                token);
            _grid.DataSource = _report.Messages.ToList();
            _summary.Text = $"Mensagens: {_report.TotalMessages:N0}; excluídas: {_report.DeletedMessages:N0}; anexos: {_report.MessagesWithAttachments:N0}; tamanho: {SizeFormatter.Format(_report.InputBytes)}; SHA-256: {_report.Sha256}.";
            _exportJson.Enabled = true;
            _exportCsv.Enabled = true;
            _exportSelectedEml.Enabled = _report.Messages.Count > 0;
            _exportAllEml.Enabled = _report.TotalMessages > 0;
            StatusLabel.Text = "Exploração concluída.";
            AppendLog(_report.MessageListTruncated
                ? "A grade foi limitada para proteger a memória; a extração de todas continua processando o MBOX completo."
                : "Grade completa de mensagens carregada.");
            UpdateSelectionSummary();
        });
    }

    private void UpdateSelectionSummary()
    {
        var selected = _grid.SelectedRows.Count;
        _selectionSummary.Text = selected == 0
            ? "Selecione uma ou mais linhas para extrair emails específicos para EML."
            : $"{selected:N0} mensagem(ns) selecionada(s) para extração individual.";
    }

    private async Task ExportEmlAsync(bool selectedOnly)
    {
        var trigger = selectedOnly ? _exportSelectedEml : _exportAllEml;
        await RunOperationAsync(trigger, async token =>
        {
            if (_selection is null || _report is null)
                throw new InvalidOperationException("Liste primeiro as mensagens do MBOX.");

            HashSet<long>? messageNumbers = null;
            if (selectedOnly)
            {
                messageNumbers = _grid.SelectedRows
                    .Cast<DataGridViewRow>()
                    .Select(row => row.DataBoundItem)
                    .OfType<MboxMessageInfo>()
                    .Select(message => message.Number)
                    .ToHashSet();
                if (messageNumbers.Count == 0)
                    throw new InvalidOperationException("Selecione pelo menos uma mensagem na grade.");
            }
            else
            {
                var confirmation = MessageBox.Show(
                    this,
                    $"A caixa contém aproximadamente {_report.TotalMessages:N0} mensagens. A extração de todas pode ocupar bastante espaço e demorar.\n\nDeseja continuar?",
                    "Extrair todas as mensagens",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);
                if (confirmation != DialogResult.Yes)
                    throw new OperationCanceledException("Extração de todas as mensagens cancelada.");
            }

            var root = SelectFolder(this, "Selecione a pasta de destino dos arquivos EML");
            if (root.Length == 0)
                throw new OperationCanceledException("A pasta de destino não foi selecionada.");

            var suffix = selectedOnly ? "Selecionadas" : "Todas";
            var destination = Path.Combine(root, $"EML_{_source.MailboxName}_{suffix}_{DateTime.Now:yyyyMMdd_HHmmss}");
            var options = new MessageExtractionOptions
            {
                Source = _selection,
                OutputDirectory = destination,
                Filter = new MessageExtractionFilter
                {
                    IncludeDeleted = true,
                    MessageNumbers = messageNumbers
                },
                GenerateCsvIndex = true,
                PreserveMozillaStatusHeaders = true
            };
            var progress = new Progress<MboxParseProgress>(value =>
                ReportProgress(value.ProcessedBytes, value.TotalBytes, $"Examinadas {value.Messages:N0} mensagens."));
            var result = await MboxExtractor.ExtractAsync(options, progress, token);
            StatusLabel.Text = "Extração EML concluída.";
            AppendLog($"Mensagens examinadas: {result.ScannedMessages:N0}; extraídas: {result.ExtractedMessages:N0}; destino: {result.OutputDirectory}.");
            MessageBox.Show(this, $"{result.ExtractedMessages:N0} mensagem(ns) extraída(s) para:\n{result.OutputDirectory}", "Extração concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void ExportReport(string format)
    {
        if (_report is null) return;
        using var dialog = new SaveFileDialog
        {
            Filter = format == "json" ? "JSON|*.json" : "CSV|*.csv",
            FileName = format == "json" ? "exploracao_mbox.json" : "exploracao_mbox.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (format == "json") MboxAnalyzer.SaveJson(_report, dialog.FileName);
        else MboxAnalyzer.SaveCsv(_report, dialog.FileName);
        AppendLog("Relatório exportado: " + dialog.FileName);
    }
}
