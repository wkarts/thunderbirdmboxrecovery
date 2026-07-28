using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class BackupPage : OperationPageBase
{
    private readonly ComboBox _profiles = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _refresh = new() { Text = "Atualizar perfis", AutoSize = true };
    private readonly Button _manual = new() { Text = "Selecionar perfil", AutoSize = true };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
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
        _destination.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"Thunderbird_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
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
        form.Controls.Add(new Label { Text = "Destino ZIP:", AutoSize = true }, 0, 1);
        form.Controls.Add(_destination, 1, 1);
        form.SetColumnSpan(_destination, 2);
        form.Controls.Add(_browse, 3, 1);
        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        options.Controls.Add(new Label { Text = "Modo:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        options.Controls.Add(_mode);
        options.Controls.AddRange([_mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes, _cache, _hashes, _allowOpen]);
        form.Controls.Add(options, 0, 2);
        form.SetColumnSpan(options, 4);
        root.Controls.Add(form, 0, 0);
        root.Controls.Add(new Label { Text = "Recomendação: feche o Thunderbird para obter um snapshot consistente. A suíte rejeita arquivos de lock e grava o ZIP de forma atômica.", AutoSize = true }, 0, 1);
        root.Controls.Add(_run, 0, 2);
        root.Controls.Add(CreateOperationFooter(), 0, 3);
        Controls.Add(root);

        _refresh.Click += (_, _) => RefreshProfiles();
        _manual.Click += (_, _) => SelectProfile();
        _browse.Click += (_, _) => SelectDestination();
        _mode.SelectedIndexChanged += (_, _) => UpdateSelectionState();
        _run.Click += async (_, _) => await BackupAsync();
        RefreshProfiles();
        UpdateSelectionState();
    }

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
        using var dialog = new SaveFileDialog { Filter = "Backup ZIP|*.zip", FileName = Path.GetFileName(_destination.Text), InitialDirectory = Path.GetDirectoryName(_destination.Text) ?? string.Empty };
        if (dialog.ShowDialog(this) == DialogResult.OK) _destination.Text = dialog.FileName;
    }

    private void UpdateSelectionState()
    {
        var selective = (_mode.SelectedItem as ModeItem)?.Mode == ProfileBackupMode.Selective;
        foreach (var check in new[] { _mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes }) check.Enabled = selective;
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
                DestinationZipPath = _destination.Text,
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
            AppendLog($"Backup: {result.BackupPath}; arquivos: {result.Files:N0}; origem: {SizeFormatter.Format(result.SourceBytes)}; ZIP: {SizeFormatter.Format(result.BackupBytes)}; SHA-256: {result.Sha256}.");
            MessageBox.Show(this, $"Backup criado:\n{result.BackupPath}", "Backup concluído", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private sealed record ModeItem(string Text, ProfileBackupMode Mode);
}
