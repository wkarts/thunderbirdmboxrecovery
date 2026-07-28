using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class ExtractPage : OperationPageBase
{
    private readonly MboxSourceControl _source = new();
    private readonly TextBox _output = CreatePathTextBox();
    private readonly Button _browseOutput = CreateBrowseButton();
    private readonly TextBox _subject = new() { Width = 220 };
    private readonly TextBox _sender = new() { Width = 220 };
    private readonly TextBox _recipient = new() { Width = 220 };
    private readonly DateTimePicker _fromDate = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly DateTimePicker _toDate = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    private readonly CheckBox _onlyDeleted = new() { Text = "Somente excluídas", AutoSize = true };
    private readonly CheckBox _includeDeleted = new() { Text = "Incluir excluídas", AutoSize = true, Checked = true };
    private readonly CheckBox _attachments = new() { Text = "Somente com anexos", AutoSize = true };
    private readonly CheckBox _preserveStatus = new() { Text = "Preservar cabeçalhos Mozilla", AutoSize = true, Checked = true };
    private readonly Button _run = new() { Text = "Extrair mensagens para EML", AutoSize = true };

    public ExtractPage()
    {
        Dock = DockStyle.Fill;
        _output.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Thunderbird Recovery Suite", "EML");
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
        options.Controls.Add(new Label { Text = "Destino EML:", AutoSize = true }, 0, 0);
        options.Controls.Add(_output, 1, 0);
        options.Controls.Add(_browseOutput, 2, 0);
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        filters.Controls.AddRange([
            new Label { Text = "Assunto contém:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, _subject,
            new Label { Text = "Remetente:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }, _sender,
            new Label { Text = "Destinatário:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }, _recipient,
            new Label { Text = "De:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }, _fromDate,
            new Label { Text = "Até:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }, _toDate,
            _onlyDeleted, _includeDeleted, _attachments, _preserveStatus
        ]);
        options.Controls.Add(filters, 0, 1);
        options.SetColumnSpan(filters, 3);
        root.Controls.Add(options, 0, 1);
        root.Controls.Add(_run, 0, 2);
        root.Controls.Add(CreateOperationFooter(), 0, 3);
        Controls.Add(root);

        _browseOutput.Click += (_, _) => { var selected = SelectFolder(this, "Selecione a pasta para os arquivos EML", _output.Text); if (selected.Length > 0) _output.Text = selected; };
        _onlyDeleted.CheckedChanged += (_, _) => { if (_onlyDeleted.Checked) _includeDeleted.Checked = true; };
        _run.Click += async (_, _) => await ExtractAsync();
    }

    private async Task ExtractAsync()
    {
        await RunOperationAsync(_run, async token =>
        {
            var source = await _source.GetSelectionAsync(token);
            var destination = Path.Combine(_output.Text, $"Extracao_{_source.MailboxName}_{DateTime.Now:yyyyMMdd_HHmmss}");
            var options = new MessageExtractionOptions
            {
                Source = source,
                OutputDirectory = destination,
                Filter = new MessageExtractionFilter
                {
                    SubjectContains = _subject.Text,
                    SenderContains = _sender.Text,
                    RecipientContains = _recipient.Text,
                    DateFrom = _fromDate.Checked ? new DateTimeOffset(_fromDate.Value.Date) : null,
                    DateTo = _toDate.Checked ? new DateTimeOffset(_toDate.Value.Date.AddDays(1).AddTicks(-1)) : null,
                    OnlyDeleted = _onlyDeleted.Checked,
                    IncludeDeleted = _includeDeleted.Checked,
                    OnlyWithAttachment = _attachments.Checked
                },
                GenerateCsvIndex = true,
                PreserveMozillaStatusHeaders = _preserveStatus.Checked
            };
            var progress = new Progress<MboxParseProgress>(value =>
                ReportProgress(value.ProcessedBytes, value.TotalBytes, $"Examinadas {value.Messages:N0} mensagens."));
            var result = await MboxExtractor.ExtractAsync(options, progress, token);
            StatusLabel.Text = "Extração concluída.";
            AppendLog($"Mensagens examinadas: {result.ScannedMessages:N0}; extraídas: {result.ExtractedMessages:N0}; bytes: {SizeFormatter.Format(result.ExtractedBytes)}.");
            MessageBox.Show(this, $"{result.ExtractedMessages:N0} mensagens foram extraídas para:\n{result.OutputDirectory}", "Extração concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }
}
