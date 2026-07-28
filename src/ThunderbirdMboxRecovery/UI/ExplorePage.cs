using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class ExplorePage : OperationPageBase
{
    private readonly MboxSourceControl _source = new();
    private readonly Button _analyze = new() { Text = "Explorar mensagens", AutoSize = true };
    private readonly Button _exportJson = new() { Text = "Exportar JSON", AutoSize = true, Enabled = false };
    private readonly Button _exportCsv = new() { Text = "Exportar CSV", AutoSize = true, Enabled = false };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
    };
    private readonly Label _summary = new() { AutoSize = true };
    private MboxDiagnosisReport? _report;

    public ExplorePage()
    {
        Dock = DockStyle.Fill;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        root.Controls.Add(_source, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        buttons.Controls.AddRange([_analyze, _exportJson, _exportCsv]);
        root.Controls.Add(buttons, 0, 1);
        root.Controls.Add(_summary, 0, 2);
        root.Controls.Add(_grid, 0, 3);
        root.Controls.Add(CreateOperationFooter(), 0, 4);
        Controls.Add(root);

        _analyze.Click += async (_, _) => await AnalyzeAsync();
        _exportJson.Click += (_, _) => Export("json");
        _exportCsv.Click += (_, _) => Export("csv");
    }

    private async Task AnalyzeAsync()
    {
        await RunOperationAsync(_analyze, async token =>
        {
            var selection = await _source.GetSelectionAsync(token);
            AppendLog("Explorando " + selection.DisplayName);
            var progress = new Progress<MboxParseProgress>(value =>
                ReportProgress(value.ProcessedBytes, value.TotalBytes, $"Lidas {value.Messages:N0} mensagens."));
            _report = await MboxAnalyzer.AnalyzeAsync(selection, new MboxAnalysisOptions { MaxMessagesInMemory = Environment.Is64BitProcess ? 100_000 : 25_000 }, progress, token);
            _grid.DataSource = _report.Messages.ToList();
            _summary.Text = $"Mensagens: {_report.TotalMessages:N0}; excluídas: {_report.DeletedMessages:N0}; anexos: {_report.MessagesWithAttachments:N0}; tamanho: {SizeFormatter.Format(_report.InputBytes)}; SHA-256: {_report.Sha256}.";
            _exportJson.Enabled = true;
            _exportCsv.Enabled = true;
            StatusLabel.Text = "Exploração concluída.";
            AppendLog(_report.MessageListTruncated
                ? "A grade foi limitada; o relatório total permanece nos contadores."
                : "Grade de mensagens carregada.");
        });
    }

    private void Export(string format)
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
