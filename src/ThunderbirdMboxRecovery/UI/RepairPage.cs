using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class RepairPage : OperationPageBase
{
    private readonly MboxSourceControl _source = new();
    private readonly TextBox _output = CreatePathTextBox();
    private readonly Button _browseOutput = CreateBrowseButton();
    private readonly CheckBox _split = new() { Text = "Fracionar saída", AutoSize = true, Checked = false };
    private readonly NumericUpDown _size = new() { Minimum = 1, Maximum = 64, DecimalPlaces = 1, Increment = 0.5m, Value = 1.5m, Enabled = false, Width = 90 };
    private readonly CheckBox _recoverDeleted = new() { Text = "Recuperar mensagens marcadas como excluídas/expurgadas", AutoSize = true, Checked = true };
    private readonly CheckBox _normalizeStatus = new() { Text = "Normalizar X-Mozilla-Status e X-Mozilla-Status2", AutoSize = true, Checked = true };
    private readonly Button _run = new() { Text = "Reparar e reconstruir MBOX", AutoSize = true };

    public RepairPage()
    {
        Dock = DockStyle.Fill;
        _output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Thunderbird Recovery Suite");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(_source, 0, 0);

        var options = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
        options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        options.Controls.Add(new Label { Text = "Destino:", AutoSize = true }, 0, 0);
        options.Controls.Add(_output, 1, 0);
        options.Controls.Add(_browseOutput, 2, 0);
        var flow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        flow.Controls.Add(_split);
        flow.Controls.Add(new Label { Text = "Tamanho por parte (GiB):", AutoSize = true, Margin = new Padding(12, 7, 3, 3) });
        flow.Controls.Add(_size);
        flow.Controls.Add(_recoverDeleted);
        flow.Controls.Add(_normalizeStatus);
        options.Controls.Add(flow, 0, 1);
        options.SetColumnSpan(flow, 3);
        root.Controls.Add(options, 0, 1);
        root.Controls.Add(_run, 0, 2);
        root.Controls.Add(CreateOperationFooter(), 0, 3);
        Controls.Add(root);

        _split.CheckedChanged += (_, _) => _size.Enabled = _split.Checked;
        _browseOutput.Click += (_, _) => { var selected = SelectFolder(this, "Selecione a pasta de destino", _output.Text); if (selected.Length > 0) _output.Text = selected; };
        _run.Click += async (_, _) => await RepairAsync();
    }

    private async Task RepairAsync()
    {
        await RunOperationAsync(_run, async token =>
        {
            var source = await _source.GetSelectionAsync(token);
            var expected = _source.ExpectedBytes > 0 ? _source.ExpectedBytes : new FileInfo(source.SourcePath).Length;
            var runOutput = RecoveryCoordinator.CreateOutputDirectory(_output.Text, _source.MailboxName);
            var chunk = checked((long)(_size.Value * 1024m * 1024m * 1024m));
            RecoveryCoordinator.ValidateDestination(runOutput, expected, _split.Checked, chunk);

            var options = new RecoveryOptions
            {
                SourcePath = source.SourcePath,
                ArchiveEntryKey = source.ArchiveEntryKey,
                ArchivePassword = source.ArchivePassword,
                OutputDirectory = runOutput,
                MailboxName = _source.MailboxName,
                SplitOutput = _split.Checked,
                RecoverDeletedMessages = _recoverDeleted.Checked,
                NormalizeMozillaStatusHeaders = _normalizeStatus.Checked,
                TargetChunkBytes = chunk,
                ExpectedInputBytes = expected
            };
            var progress = new Progress<RecoveryProgress>(value =>
                ReportProgress(value.ProcessedBytes, value.TotalBytes, $"{value.Stage}: {value.Messages:N0} mensagens; {value.CompletedParts:N0} arquivo(s)."));
            var result = await Task.Run(() => new MboxSplitter().Execute(options, progress, token), token);
            StatusLabel.Text = "Reparo concluído.";
            AppendLog($"Destino: {result.OutputDirectory}");
            AppendLog($"Mensagens: {result.TotalMessages:N0}; expurgadas recuperadas: {result.ExpungedMessagesRecovered:N0}; IMAP excluídas: {result.ImapDeletedMessagesRecovered:N0}.");
            MessageBox.Show(this, $"Recuperação concluída em:\n{result.OutputDirectory}", "Concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }
}
