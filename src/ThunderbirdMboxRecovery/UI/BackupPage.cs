using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class BackupPage : OperationPageBase
{
    private readonly ComboBox _profiles = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _refresh = new() { Text = "Atualizar perfis", AutoSize = true };
    private readonly Button _manual = new() { Text = "Selecionar perfil", AutoSize = true };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _format = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly TextBox _destination = CreatePathTextBox();
    private readonly Button _browse = CreateBrowseButton();
    private readonly CheckBox _mail = new() { Text = "Mail", Checked = true, AutoSize = true };
    private readonly CheckBox _imap = new() { Text = "ImapMail/News", Checked = true, AutoSize = true };
    private readonly CheckBox _preferences = new() { Text = "Preferências", Checked = true, AutoSize = true };
    private readonly CheckBox _addresses = new() { Text = "Catálogos", Checked = true, AutoSize = true };
    private readonly CheckBox _calendar = new() { Text = "Calendários", Checked = true, AutoSize = true };
    private readonly CheckBox _credentials = new() { Text = "Senhas/certificados", Checked = true, AutoSize = true };
    private readonly CheckBox _extensions = new() { Text = "Extensões", Checked = true, AutoSize = true };
    private readonly CheckBox _indexes = new() { Text = "Índices MSF/SQLite", Checked = false, AutoSize = true };
    private readonly CheckBox _cache = new() { Text = "Caches", Checked = false, AutoSize = true };
    private readonly CheckBox _hashes = new() { Text = "Calcular SHA-256 por arquivo", Checked = true, AutoSize = true };
    private readonly CheckBox _allowOpen = new() { Text = "Permitir perfil aberto (não recomendado)", Checked = false, AutoSize = true };
    private readonly Button _run = new() { Text = "Criar backup do perfil", AutoSize = true };
    private readonly Label _formatNote = new() { AutoSize = true, ForeColor = Color.DimGray };

    public BackupPage()
    {
        Dock = DockStyle.Fill;
        _mode.DataSource = new[]
        {
            new ModeItem("Completo", ProfileBackupMode.Complete),
            new ModeItem("Somente mensagens", ProfileBackupMode.MessagesOnly),
            new ModeItem("Seletivo", ProfileBackupMode.Selective)
        };
        _mode.DisplayMember = nameof(ModeItem.Text);
        _format.DataSource = new[]
        {
            new FormatItem("ZIP — maior compatibilidade", ProfileBackupArchiveFormat.Zip),
            new FormatItem("7Z — maior compressão", ProfileBackupArchiveFormat.SevenZip)
        };
        _format.DisplayMember = nameof(FormatItem.Text);
        SetDefaultDestination();

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.Controls.Add(new Label { Text = "Perfil:", AutoSize = true }, 0, 0);
        form.Controls.Add(_profiles, 1, 0);
        form.Controls.Add(_refresh, 2, 0);
        form.Controls.Add(_manual, 3, 0);
        form.Controls.Add(new Label { Text = "Destino:", AutoSize = true }, 0, 1);
        form.Controls.Add(_destination, 1, 1);
        form.SetColumnSpan(_destination, 2);
        form.Controls.Add(_browse, 3, 1);

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        options.Controls.Add(new Label { Text = "Modo:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        options.Controls.Add(_mode);
        options.Controls.Add(new Label { Text = "Formato:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) });
        options.Controls.Add(_format);
        options.Controls.AddRange([_mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes, _cache, _hashes, _allowOpen]);
        form.Controls.Add(options, 0, 2);
        form.SetColumnSpan(options, 4);
        form.Controls.Add(_formatNote, 0, 3);
        form.SetColumnSpan(_formatNote, 4);

        root.Controls.Add(form, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Recomendação: feche o Thunderbird para obter um snapshot consistente. A suíte rejeita arquivos de lock e grava o backup em arquivo parcial antes de confirmá-lo.",
            AutoSize = true,
            MaximumSize = new Size(1100, 0)
        }, 0, 1);
        root.Controls.Add(new Label
        {
            Text = "O formato 7Z usa LZMA2 e normalmente reduz mais o tamanho. O formato ZIP é indicado quando o backup precisa abrir sem ferramentas adicionais.",
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            ForeColor = Color.DimGray
        }, 0, 2);
        root.Controls.Add(_run, 0, 3);
        root.Controls.Add(CreateOperationFooter(), 0, 4);
        Controls.Add(root);

        _refresh.Click += (_, _) => RefreshProfiles();
        _manual.Click += (_, _) => SelectProfile();
        _browse.Click += (_, _) => SelectDestination();
        _mode.SelectedIndexChanged += (_, _) => UpdateSelectionState();
        _format.SelectedIndexChanged += (_, _) => UpdateFormat();
        _run.Click += async (_, _) => await BackupAsync();
        RefreshProfiles();
        UpdateSelectionState();
        UpdateFormat();
    }

    private ProfileBackupArchiveFormat SelectedFormat =>
        (_format.SelectedItem as FormatItem)?.Format ?? ProfileBackupArchiveFormat.Zip;

    private void RefreshProfiles()
    {
        var profiles = ThunderbirdProfileService.FindProfiles().ToList();
        _profiles.DisplayMember = nameof(ThunderbirdProfileInfo.DisplayText);
        _profiles.DataSource = profiles;
        AppendLog($"{profiles.Count} perfil(is) localizado(s).");
    }

    private void SelectProfile()
    {
        var selected = SelectFolder(this, "Selecione a pasta do perfil do Thunderbird");
        if (selected.Length == 0) return;
        ThunderbirdProfileService.ValidateProfile(selected);
        var profile = new ThunderbirdProfileInfo { Name = Path.GetFileName(selected), Path = selected, IsDefault = false, IsRelative = false, EstimatedBytes = 0 };
        var list = (_profiles.DataSource as List<ThunderbirdProfileInfo>) ?? [];
        list = list.Where(item => !item.Path.Equals(selected, StringComparison.OrdinalIgnoreCase)).ToList();
        list.Insert(0, profile);
        _profiles.DataSource = list;
        _profiles.SelectedIndex = 0;
    }

    private void SelectDestination()
    {
        var format = SelectedFormat;
        using var dialog = new SaveFileDialog
        {
            Filter = format == ProfileBackupArchiveFormat.SevenZip ? "Backup 7Z|*.7z" : "Backup ZIP|*.zip",
            DefaultExt = format == ProfileBackupArchiveFormat.SevenZip ? "7z" : "zip",
            AddExtension = true,
            FileName = Path.GetFileName(_destination.Text),
            InitialDirectory = Path.GetDirectoryName(_destination.Text) ?? string.Empty
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _destination.Text = dialog.FileName;
    }

    private void SetDefaultDestination()
    {
        var extension = SelectedFormat == ProfileBackupArchiveFormat.SevenZip ? ".7z" : ".zip";
        _destination.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"Thunderbird_Backup_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
    }

    private void UpdateFormat()
    {
        var extension = SelectedFormat == ProfileBackupArchiveFormat.SevenZip ? ".7z" : ".zip";
        if (string.IsNullOrWhiteSpace(_destination.Text))
            SetDefaultDestination();
        else
            _destination.Text = Path.ChangeExtension(_destination.Text, extension);

        _formatNote.Text = SelectedFormat == ProfileBackupArchiveFormat.SevenZip
            ? "7Z selecionado: compactação LZMA2 gerenciada pela própria aplicação."
            : "ZIP selecionado: formato amplamente compatível com o Windows e outras ferramentas.";
    }

    private void UpdateSelectionState()
    {
        var selective = (_mode.SelectedItem as ModeItem)?.Mode == ProfileBackupMode.Selective;
        foreach (var check in new[] { _mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes, _cache })
            check.Enabled = selective;
    }

    private async Task BackupAsync()
    {
        await RunOperationAsync(_run, async token =>
        {
            if (_profiles.SelectedItem is not ThunderbirdProfileInfo profile)
                throw new InvalidOperationException("Selecione um perfil.");
            var mode = (_mode.SelectedItem as ModeItem)?.Mode ?? ProfileBackupMode.Complete;
            if (ThunderbirdProfileService.IsProfileInUse(profile.Path))
                AppendLog(_allowOpen.Checked
                    ? "AVISO: o perfil está em uso; o backup pode representar arquivos em instantes diferentes."
                    : "O perfil está em uso e o backup será bloqueado até o Thunderbird ser fechado.");

            var options = new ProfileBackupOptions
            {
                Profile = profile,
                DestinationArchivePath = _destination.Text,
                ArchiveFormat = SelectedFormat,
                Mode = mode,
                Selection = new ProfileBackupSelection
                {
                    Mail = _mail.Checked,
                    ImapMail = _imap.Checked,
                    Preferences = _preferences.Checked,
                    AddressBooks = _addresses.Checked,
                    Calendars = _calendar.Checked,
                    PasswordsAndCertificates = _credentials.Checked,
                    Extensions = _extensions.Checked,
                    SearchIndexes = _indexes.Checked,
                    Cache = _cache.Checked
                },
                CalculateFileHashes = _hashes.Checked,
                AllowInUseProfile = _allowOpen.Checked
            };
            var progress = new Progress<(long Files, long Bytes, string Current)>(value =>
            {
                StatusLabel.Text = $"Backup: {value.Files:N0} arquivos; {SizeFormatter.Format(value.Bytes)}.";
                if (value.Files % 100 == 0) AppendLog(value.Current);
            });
            var result = await ProfileBackupService.CreateAsync(options, progress, token);
            StatusLabel.Text = "Backup concluído.";
            AppendLog($"Backup: {result.BackupPath}; formato: {result.ArchiveFormat}; arquivos: {result.Files:N0}; origem: {SizeFormatter.Format(result.SourceBytes)}; compactado: {SizeFormatter.Format(result.BackupBytes)}; SHA-256: {result.Sha256}.");
            MessageBox.Show(this, $"Backup criado:\n{result.BackupPath}", "Backup concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private sealed record ModeItem(string Text, ProfileBackupMode Mode);
    private sealed record FormatItem(string Text, ProfileBackupArchiveFormat Format);
}
