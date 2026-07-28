using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class MboxSourceControl : UserControl
{
    private readonly TextBox _path = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly ComboBox _entries = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label _summary = new() { Dock = DockStyle.Fill, AutoSize = true, Text = "Selecione um MBOX direto ou um backup compactado." };
    private readonly Button _browse = new() { Text = "Procurar...", AutoSize = true };
    private readonly Button _inspect = new() { Text = "Analisar origem", AutoSize = true };
    private long _expectedBytes;

    public MboxSourceControl()
    {
        Dock = DockStyle.Top;
        AutoSize = true;
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 4 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = "Origem:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        table.Controls.Add(_path, 1, 0);
        table.Controls.Add(_browse, 2, 0);
        table.Controls.Add(new Label { Text = "Senha do arquivo:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        table.Controls.Add(_password, 1, 1);
        table.Controls.Add(_inspect, 2, 1);
        table.Controls.Add(new Label { Text = "Caixa no backup:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        table.Controls.Add(_entries, 1, 2);
        table.SetColumnSpan(_entries, 2);
        table.Controls.Add(_summary, 0, 3);
        table.SetColumnSpan(_summary, 3);
        Controls.Add(table);

        _browse.Click += (_, _) => Browse();
        _inspect.Click += async (_, _) => await InspectAsync(CancellationToken.None);
        _path.TextChanged += (_, _) => ResetAnalysis();
        _entries.SelectedIndexChanged += (_, _) => UpdateEntrySummary();
    }

    public string SourcePath
    {
        get => _path.Text.Trim();
        set => _path.Text = value;
    }

    public string MailboxName
    {
        get
        {
            var selection = _entries.SelectedItem as ArchiveEntryInfo;
            return MailboxNameResolver.FromSource(selection?.Key ?? SourcePath);
        }
    }

    public long ExpectedBytes => _expectedBytes;

    public async Task<MboxSourceSelection> GetSelectionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SourcePath))
            throw new FileNotFoundException("Selecione um arquivo MBOX ou backup existente.", SourcePath);

        if (ArchiveService.IsArchive(SourcePath) && _entries.SelectedItem is not ArchiveEntryInfo)
            await InspectAsync(cancellationToken);

        return new MboxSourceSelection
        {
            SourcePath = SourcePath,
            ArchiveEntryKey = (_entries.SelectedItem as ArchiveEntryInfo)?.Key,
            ArchivePassword = string.IsNullOrWhiteSpace(_password.Text) ? null : _password.Text
        };
    }

    public async Task InspectAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SourcePath))
            throw new FileNotFoundException("A origem selecionada não existe.", SourcePath);

        _inspect.Enabled = false;
        try
        {
            if (!ArchiveService.IsArchive(SourcePath))
            {
                MboxSourceValidator.ValidateDirectFile(SourcePath);
                _entries.DataSource = null;
                _expectedBytes = new FileInfo(SourcePath).Length;
                _summary.Text = $"MBOX direto: {MailboxName} — {SizeFormatter.Format(_expectedBytes)}.";
                return;
            }

            var entries = await ArchiveService.InspectAsync(SourcePath, _password.Text, cancellationToken);
            if (entries.Count == 0)
                throw new InvalidDataException("Nenhuma caixa MBOX provável foi encontrada no arquivo compactado.");
            _entries.DisplayMember = nameof(ArchiveEntryInfo.DisplayText);
            _entries.DataSource = entries.ToList();
            var inbox = entries.ToList().FindIndex(entry => Path.GetFileName(entry.Key).Equals("Inbox", StringComparison.OrdinalIgnoreCase));
            _entries.SelectedIndex = inbox >= 0 ? inbox : 0;
            UpdateEntrySummary();
        }
        finally
        {
            _inspect.Enabled = true;
        }
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Selecione uma caixa MBOX ou backup do Thunderbird",
            Filter = "Caixas do Thunderbird e backups|*.*|MBOX exportado|*.mbox|Arquivos compactados|*.7z;*.zip;*.rar;*.tar;*.gz;*.bz2;*.xz",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _path.Text = dialog.FileName;
    }

    private void ResetAnalysis()
    {
        _entries.DataSource = null;
        _expectedBytes = 0;
        _summary.Text = "Clique em Analisar origem.";
    }

    private void UpdateEntrySummary()
    {
        if (_entries.SelectedItem is not ArchiveEntryInfo entry) return;
        _expectedBytes = entry.Size;
        _summary.Text = $"Entrada selecionada: {entry.Key} — {SizeFormatter.Format(entry.Size)}.";
    }
}
