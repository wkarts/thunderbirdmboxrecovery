using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class RestorePage : OperationPageBase
{
    private readonly TextBox _backup = CreatePathTextBox();
    private readonly Button _browseBackup = CreateBrowseButton();
    private readonly TextBox _destination = CreatePathTextBox();
    private readonly TextBox _password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly Button _browseDestination = CreateBrowseButton();
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly CheckBox _mail = new() { Text = "Mail", Checked = true, AutoSize = true };
    private readonly CheckBox _imap = new() { Text = "ImapMail/News", Checked = true, AutoSize = true };
    private readonly CheckBox _preferences = new() { Text = "Preferências", Checked = true, AutoSize = true };
    private readonly CheckBox _addresses = new() { Text = "Catálogos", Checked = true, AutoSize = true };
    private readonly CheckBox _calendar = new() { Text = "Calendários", Checked = true, AutoSize = true };
    private readonly CheckBox _credentials = new() { Text = "Senhas/certificados", Checked = true, AutoSize = true };
    private readonly CheckBox _extensions = new() { Text = "Extensões", Checked = true, AutoSize = true };
    private readonly CheckBox _indexes = new() { Text = "Índices MSF/SQLite", Checked = false, AutoSize = true };
    private readonly CheckBox _cache = new() { Text = "Caches", Checked = false, AutoSize = true };
    private readonly CheckBox _safety = new() { Text = "Criar backup de segurança do destino", Checked = true, AutoSize = true };
    private readonly CheckBox _overwrite = new() { Text = "Sobrescrever arquivos existentes", Checked = false, AutoSize = true };
    private readonly CheckBox _verify = new() { Text = "Validar SHA-256 pelo manifesto", Checked = true, AutoSize = true };
    private readonly CheckBox _register = new() { Text = "Registrar perfil restaurado no Thunderbird", Checked = true, AutoSize = true };
    private readonly CheckBox _makeDefault = new() { Text = "Marcar como padrão no profiles.ini", Checked = false, AutoSize = true };
    private readonly TextBox _profileName = new() { Width = 220, Text = "Perfil restaurado" };
    private readonly Button _run = new() { Text = "Restaurar perfil", AutoSize = true };

    public RestorePage()
    {
        Dock = DockStyle.Fill;
        _mode.DataSource = new[]
        {
            new ModeItem("Completo", ProfileBackupMode.Complete),
            new ModeItem("Somente mensagens", ProfileBackupMode.MessagesOnly),
            new ModeItem("Seletivo", ProfileBackupMode.Selective)
        };
        _mode.DisplayMember = nameof(ModeItem.Text);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 5 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        form.Controls.Add(new Label { Text = "Backup:", AutoSize = true }, 0, 0);
        form.Controls.Add(_backup, 1, 0);
        form.Controls.Add(_browseBackup, 2, 0);
        form.Controls.Add(new Label { Text = "Senha do backup:", AutoSize = true }, 0, 1);
        form.Controls.Add(_password, 1, 1);
        form.SetColumnSpan(_password, 2);
        form.Controls.Add(new Label { Text = "Perfil de destino:", AutoSize = true }, 0, 2);
        form.Controls.Add(_destination, 1, 2);
        form.Controls.Add(_browseDestination, 2, 2);

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        options.Controls.Add(new Label { Text = "Modo:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        options.Controls.Add(_mode);
        options.Controls.AddRange([_mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes, _cache]);
        form.Controls.Add(options, 0, 3);
        form.SetColumnSpan(options, 3);

        var operational = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        operational.Controls.AddRange([_safety, _overwrite, _verify, _register]);
        operational.Controls.Add(new Label { Text = "Nome do perfil:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) });
        operational.Controls.Add(_profileName);
        operational.Controls.Add(_makeDefault);
        form.Controls.Add(operational, 0, 4);
        form.SetColumnSpan(operational, 3);

        root.Controls.Add(form, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "A restauração bloqueia travessia de diretórios, usa arquivos parciais, pode restaurar seletivamente e exige o Thunderbird fechado para registrar o perfil.",
            AutoSize = true
        }, 0, 1);
        root.Controls.Add(_run, 0, 2);
        root.Controls.Add(CreateOperationFooter(), 0, 3);
        Controls.Add(root);

        _browseBackup.Click += (_, _) => BrowseBackup();
        _browseDestination.Click += (_, _) =>
        {
            var selected = SelectFolder(this, "Selecione ou crie o perfil de destino", _destination.Text);
            if (selected.Length > 0)
            {
                _destination.Text = selected;
                if (string.IsNullOrWhiteSpace(_profileName.Text) || _profileName.Text == "Perfil restaurado")
                    _profileName.Text = Path.GetFileName(selected) ?? "Perfil restaurado";
            }
        };
        _mode.SelectedIndexChanged += (_, _) => UpdateSelectionState();
        _register.CheckedChanged += (_, _) => UpdateRegistrationState();
        _run.Click += async (_, _) => await RestoreAsync();
        UpdateSelectionState();
        UpdateRegistrationState();
    }

    private void BrowseBackup()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Backups|*.zip;*.7z;*.rar;*.tar;*.gz;*.bz2;*.xz|Todos os arquivos|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _backup.Text = dialog.FileName;
    }

    private void UpdateSelectionState()
    {
        var selective = (_mode.SelectedItem as ModeItem)?.Mode == ProfileBackupMode.Selective;
        foreach (var check in new[] { _mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes })
            check.Enabled = selective;
    }

    private void UpdateRegistrationState()
    {
        _profileName.Enabled = _register.Checked;
        _makeDefault.Enabled = _register.Checked;
    }

    private async Task RestoreAsync()
    {
        await RunOperationAsync(_run, async token =>
        {
            if (string.IsNullOrWhiteSpace(_destination.Text))
                throw new InvalidOperationException("Selecione o perfil de destino.");
            if (_register.Checked && ThunderbirdProfileService.IsThunderbirdRunning())
                throw new IOException("Feche completamente o Thunderbird antes de restaurar e registrar o perfil.");

            var mode = (_mode.SelectedItem as ModeItem)?.Mode ?? ProfileBackupMode.Complete;
            var options = new ProfileRestoreOptions
            {
                BackupPath = _backup.Text,
                DestinationProfilePath = _destination.Text,
                ArchivePassword = string.IsNullOrWhiteSpace(_password.Text) ? null : _password.Text,
                CreateSafetyBackup = _safety.Checked,
                OverwriteExistingFiles = _overwrite.Checked,
                VerifyHashes = _verify.Checked,
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
                RegisterProfile = _register.Checked,
                RegisteredProfileName = _profileName.Text,
                MakeRegisteredProfileDefault = _makeDefault.Checked
            };
            var progress = new Progress<(long Files, long Bytes, string Current)>(value =>
            {
                StatusLabel.Text = $"Restaurando {value.Files:N0} arquivos; {SizeFormatter.Format(value.Bytes)}.";
                if (value.Files % 100 == 0) AppendLog(value.Current);
            });
            var result = await ProfileRestoreService.RestoreAsync(options, progress, token);
            StatusLabel.Text = "Restauração concluída.";
            AppendLog($"Restaurados: {result.RestoredFiles:N0}; ignorados: {result.SkippedFiles:N0}; validados: {result.VerifiedFiles:N0}; bytes: {SizeFormatter.Format(result.RestoredBytes)}.");
            if (result.SafetyBackupPath is not null) AppendLog("Backup de segurança: " + result.SafetyBackupPath);
            if (result.ProfileRegistered) AppendLog($"Perfil registrado no Thunderbird: {result.RegisteredProfileName}.");
            foreach (var warning in result.Warnings.Take(50)) AppendLog("AVISO: " + warning);
            MessageBox.Show(this, $"Restauração concluída em:\n{result.DestinationProfilePath}", "Restauração concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private sealed record ModeItem(string Text, ProfileBackupMode Mode);
}
