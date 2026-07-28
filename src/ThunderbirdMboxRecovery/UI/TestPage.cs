using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class TestPage : OperationPageBase
{
    private readonly MboxSourceControl _source = new();
    private readonly Button _run = new() { Text = "Testar e diagnosticar", AutoSize = true };
    private readonly Button _save = new() { Text = "Salvar diagnóstico", AutoSize = true, Enabled = false };
    private readonly DataGridView _issues = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AutoGenerateColumns = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };
    private readonly Label _summary = new() { AutoSize = true };
    private MboxDiagnosisReport? _report;

    public TestPage()
    {
        Dock = DockStyle.Fill;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        root.Controls.Add(_source, 0, 0);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        buttons.Controls.AddRange([_run, _save]);
        root.Controls.Add(buttons, 0, 1);
        root.Controls.Add(_summary, 0, 2);
        root.Controls.Add(_issues, 0, 3);
        root.Controls.Add(CreateOperationFooter(), 0, 4);
        Controls.Add(root);

        _run.Click += async (_, _) => await DiagnoseAsync();
        _save.Click += (_, _) => Save();
    }

    private async Task DiagnoseAsync()
    {
        await RunOperationAsync(_run, async token =>
        {
            var source = await _source.GetSelectionAsync(token);
            var progress = new Progress<MboxParseProgress>(value =>
                ReportProgress(value.ProcessedBytes, value.TotalBytes, $"Testando estrutura — {value.Messages:N0} mensagens."));
            _report = await MboxAnalyzer.AnalyzeAsync(source, new MboxAnalysisOptions { MaxMessagesInMemory = 50_000 }, progress, token);
            _issues.DataSource = _report.Issues.ToList();
            _summary.Text = $"Mensagens: {_report.TotalMessages:N0}; problemas: {_report.Issues.Count:N0}; críticos: {_report.Issues.Count(issue => issue.Severity.Equals("erro", StringComparison.OrdinalIgnoreCase)):N0}; duplicados: {_report.DuplicateMessageIds:N0}; sem terminador: {_report.MissingHeaderTerminator:N0}.";
            _save.Enabled = true;
            StatusLabel.Text = _report.HasCriticalIssues ? "Diagnóstico concluído com erros estruturais." : "Diagnóstico concluído.";
            AppendLog(StatusLabel.Text);
        });
    }

    private void Save()
    {
        if (_report is null) return;
        using var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = "diagnostico_mbox.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        MboxAnalyzer.SaveJson(_report, dialog.FileName);
        AppendLog("Diagnóstico salvo: " + dialog.FileName);
    }
}
