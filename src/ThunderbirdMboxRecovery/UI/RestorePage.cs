using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class RestorePage : OperationPageBase
{
    private readonly TextBox _backup = CreatePathTextBox();
    private readonly Button _browseBackup = CreateBrowseButton();
    private readonly TextBox _password = new() { Width = 180, UseSystemPasswordChar = true };
    private readonly Label _backupInfo = new() { AutoSize = true, ForeColor = Color.DarkSlateBlue, MaximumSize = new Size(1160, 0) };
    private readonly ComboBox _targetMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 390 };
    private readonly ComboBox _dataRoots = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _refresh = new() { Text = "Detectar novamente", AutoSize = true };
    private readonly Button _manualRoot = new() { Text = "Diretório de dados", AutoSize = true };
    private readonly ComboBox _profiles = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _profileName = new() { Width = 260, Text = $"Restaurado_{DateTime.Now:yyyyMMdd_HHmmss}" };
    private readonly TextBox _destination = CreatePathTextBox();
    private readonly Button _browseDestination = CreateBrowseButton();
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
    private readonly CheckBox _mail = new() { Text = "Mail", Checked = true, AutoSize = true };
    private readonly CheckBox _imap = new() { Text = "ImapMail/News", Checked = true, AutoSize = true };
    private readonly CheckBox _preferences = new() { Text = "Preferências", Checked = true, AutoSize = true };
    private readonly CheckBox _addresses = new() { Text = "Catálogos", Checked = true, AutoSize = true };
    private readonly CheckBox _calendar = new() { Text = "Calendários", Checked = true, AutoSize = true };
    private readonly CheckBox _credentials = new() { Text = "Senhas/certificados", Checked = true, AutoSize = true };
    private readonly CheckBox _extensions = new() { Text = "Extensões", Checked = true, AutoSize = true };
    private readonly CheckBox _indexes = new() { Text = "Índices MSF/SQLite", Checked = false, AutoSize = true };
    private readonly CheckBox _cache = new() { Text = "Caches internos", Checked = false, AutoSize = true };
    private readonly CheckBox _restoreLocalCache = new() { Text = "Restaurar também AppData\\Local", Checked = false, AutoSize = true };
    private readonly CheckBox _safety = new() { Text = "Criar backup de segurança antes de substituir", Checked = true, AutoSize = true };
    private readonly ComboBox _safetyFormat = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly CheckBox _overwrite = new() { Text = "Sobrescrever arquivos existentes", Checked = false, AutoSize = true };
    private readonly CheckBox _verify = new() { Text = "Validar SHA-256", Checked = true, AutoSize = true };
    private readonly CheckBox _makeDefault = new() { Text = "Definir novo perfil como padrão", Checked = false, AutoSize = true };
    private readonly CheckBox _understandRisk = new() { Text = "Compreendo que a substituição pode causar perda de dados", AutoSize = true };
    private readonly TextBox _confirmation = new() { Width = 210 };
    private readonly Label _confirmationLabel = new() { AutoSize = true, Margin = new Padding(12, 7, 3, 3) };
    private readonly Label _destinationInfo = new() { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(1160, 0) };
    private readonly Button _run = new() { Text = "Restaurar", AutoSize = true };

    private ProfileBackupInspection? _inspection;
    private bool _destinationWasManuallySelected;
    private readonly string _newProfileToken = Guid.NewGuid().ToString("N")[..8];

    public RestorePage()
    {
        Dock = DockStyle.Fill;
        _targetMode.DataSource = new[]
        {
            new TargetItem("Criar um novo perfil — recomendado", ProfileRestoreTargetMode.CreateNewProfile),
            new TargetItem("Substituir um perfil existente", ProfileRestoreTargetMode.ReplaceExistingProfile),
            new TargetItem("Restaurar somente mensagens em um perfil existente", ProfileRestoreTargetMode.RestoreMessagesToExisting),
            new TargetItem("Restaurar o Thunderbird completo", ProfileRestoreTargetMode.RestoreThunderbirdDataRoot),
            new TargetItem("Pasta manual — modo avançado", ProfileRestoreTargetMode.ManualFolder)
        };
        _targetMode.DisplayMember = nameof(TargetItem.Text);
        _mode.DataSource = new[]
        {
            new ModeItem("Completo", ProfileBackupMode.Complete),
            new ModeItem("Somente mensagens", ProfileBackupMode.MessagesOnly),
            new ModeItem("Seletivo", ProfileBackupMode.Selective)
        };
        _mode.DisplayMember = nameof(ModeItem.Text);
        _safetyFormat.DataSource = new[]
        {
            new FormatItem("ZIP — compatibilidade", ProfileBackupArchiveFormat.Zip),
            new FormatItem("7Z — maior compressão", ProfileBackupArchiveFormat.SevenZip)
        };
        _safetyFormat.DisplayMember = nameof(FormatItem.Text);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, AutoScroll = true };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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

        form.Controls.Add(new Label { Text = "Backup:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 0);
        form.Controls.Add(_backup, 1, 0);
        form.SetColumnSpan(_backup, 2);
        form.Controls.Add(_browseBackup, 3, 0);
        form.Controls.Add(new Label { Text = "Senha do arquivo:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 1);
        form.Controls.Add(_password, 1, 1);
        form.Controls.Add(_backupInfo, 0, 2);
        form.SetColumnSpan(_backupInfo, 4);
        form.Controls.Add(new Label { Text = "Destino da restauração:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 3);
        form.Controls.Add(_targetMode, 1, 3);
        form.SetColumnSpan(_targetMode, 3);
        form.Controls.Add(new Label { Text = "Dados do Thunderbird:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 4);
        form.Controls.Add(_dataRoots, 1, 4);
        form.Controls.Add(_refresh, 2, 4);
        form.Controls.Add(_manualRoot, 3, 4);
        form.Controls.Add(new Label { Text = "Perfil existente:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 5);
        form.Controls.Add(_profiles, 1, 5);
        form.SetColumnSpan(_profiles, 3);
        form.Controls.Add(new Label { Text = "Nome do novo perfil/pasta:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 6);
        form.Controls.Add(_profileName, 1, 6);
        form.SetColumnSpan(_profileName, 3);
        form.Controls.Add(new Label { Text = "Local calculado:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 7);
        form.Controls.Add(_destination, 1, 7);
        form.SetColumnSpan(_destination, 2);
        form.Controls.Add(_browseDestination, 3, 7);
        form.Controls.Add(_destinationInfo, 0, 8);
        form.SetColumnSpan(_destinationInfo, 4);

        var selection = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 6, 0, 0) };
        selection.Controls.Add(new Label { Text = "Conteúdo:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        selection.Controls.Add(_mode);
        selection.Controls.AddRange([_mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes, _cache, _restoreLocalCache]);
        form.Controls.Add(selection, 0, 9);
        form.SetColumnSpan(selection, 4);

        var operational = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 6, 0, 0) };
        operational.Controls.Add(_safety);
        operational.Controls.Add(new Label { Text = "Formato:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        operational.Controls.Add(_safetyFormat);
        operational.Controls.AddRange([_overwrite, _verify, _makeDefault]);
        form.Controls.Add(operational, 0, 10);
        form.SetColumnSpan(operational, 4);

        var confirmations = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(0, 6, 0, 0) };
        confirmations.Controls.Add(_understandRisk);
        confirmations.Controls.Add(_confirmationLabel);
        confirmations.Controls.Add(_confirmation);
        form.Controls.Add(confirmations, 0, 11);
        form.SetColumnSpan(confirmations, 4);

        root.Controls.Add(form, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Criar novo perfil é o padrão mais seguro. A restauração de mensagens em perfil existente cria uma pasta separada em Pastas Locais e não substitui Inbox, Sent ou outras caixas atuais.",
            AutoSize = true,
            MaximumSize = new Size(1160, 0),
            ForeColor = Color.DarkSlateBlue
        }, 0, 1);
        root.Controls.Add(new Label
        {
            Text = "Substituir perfil ou Thunderbird completo exige o programa fechado, backup de segurança obrigatório e confirmação textual. AppData\\Local é cache opcional e não é restaurado por padrão.",
            AutoSize = true,
            MaximumSize = new Size(1160, 0),
            ForeColor = Color.DarkRed,
            Font = new Font(Font, FontStyle.Bold)
        }, 0, 2);
        root.Controls.Add(_run, 0, 3);
        root.Controls.Add(CreateOperationFooter(), 0, 5);
        Controls.Add(root);

        _browseBackup.Click += (_, _) => BrowseBackup();
        _refresh.Click += (_, _) => RefreshSources();
        _manualRoot.Click += (_, _) => SelectDataRoot();
        _browseDestination.Click += (_, _) => BrowseDestination();
        _backup.TextChanged += (_, _) => InspectSelectedBackup();
        _password.TextChanged += (_, _) => { if (File.Exists(_backup.Text)) InspectSelectedBackup(); };
        _targetMode.SelectedIndexChanged += (_, _) => UpdateTargetState();
        _dataRoots.SelectedIndexChanged += (_, _) => RefreshProfilesForRoot();
        _profiles.SelectedIndexChanged += (_, _) => UpdateAutomaticDestination();
        _profileName.TextChanged += (_, _) => UpdateAutomaticDestination();
        _mode.SelectedIndexChanged += (_, _) => UpdateSelectionState();
        _safety.CheckedChanged += (_, _) => _safetyFormat.Enabled = _safety.Checked;
        _run.Click += async (_, _) => await RestoreAsync();

        RefreshSources();
        UpdateTargetState();
    }

    private ProfileRestoreTargetMode SelectedTargetMode =>
        (_targetMode.SelectedItem as TargetItem)?.Mode ?? ProfileRestoreTargetMode.CreateNewProfile;

    private ProfileBackupArchiveFormat SelectedSafetyFormat =>
        (_safetyFormat.SelectedItem as FormatItem)?.Format ?? ProfileBackupArchiveFormat.Zip;

    private ThunderbirdDataRootInfo? SelectedDataRoot => _dataRoots.SelectedItem as ThunderbirdDataRootInfo;

    private void BrowseBackup()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Backups|*.zip;*.7z;*.rar;*.tar;*.gz;*.bz2;*.xz|Todos os arquivos|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _backup.Text = dialog.FileName;
    }

    private void InspectSelectedBackup()
    {
        _inspection = null;
        if (!File.Exists(_backup.Text))
        {
            _backupInfo.Text = "Selecione um arquivo de backup para identificar automaticamente o tipo de restauração.";
            return;
        }

        try
        {
            _inspection = ProfileRestoreService.InspectBackup(
                _backup.Text,
                string.IsNullOrWhiteSpace(_password.Text) ? null : _password.Text);
            var manifest = _inspection.Manifest;
            _backupInfo.Text = manifest is null
                ? $"Backup sem manifesto da suíte. Escopo estimado: {_inspection.Scope}."
                : $"Backup da suíte {manifest.Version}; escopo: {manifest.Scope}; perfil: {manifest.ProfileName}; arquivos: {manifest.Files.Count:N0}; criado em: {manifest.CreatedAt:dd/MM/yyyy HH:mm}.";

            SelectBestDataRootForInspection(manifest);
            if (_inspection.Scope == ProfileBackupScope.ThunderbirdDataRoot)
                SelectTargetMode(ProfileRestoreTargetMode.RestoreThunderbirdDataRoot);
            else if (SelectedTargetMode == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot)
                SelectTargetMode(ProfileRestoreTargetMode.CreateNewProfile);
            _restoreLocalCache.Enabled = manifest?.IncludedLocalCache == true && SelectedTargetMode == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot;
        }
        catch (Exception exception)
        {
            _backupInfo.Text = "Não foi possível inspecionar o backup: " + exception.Message;
        }
    }


    private void SelectBestDataRootForInspection(ProfileBackupManifest? manifest)
    {
        var dataRootType = manifest?.DataRootType;
        if (!dataRootType.HasValue || _dataRoots.Items.Count == 0) return;
        for (var index = 0; index < _dataRoots.Items.Count; index++)
        {
            if (_dataRoots.Items[index] is ThunderbirdDataRootInfo root && root.Type == dataRootType.Value)
            {
                _dataRoots.SelectedIndex = index;
                return;
            }
        }
    }

    private void RefreshSources()
    {
        var roots = ThunderbirdProfileService.FindDataRoots().ToList();
        _dataRoots.DisplayMember = nameof(ThunderbirdDataRootInfo.DisplayText);
        _dataRoots.DataSource = roots;
        if (roots.Count > 0)
        {
            var preferred = roots.FindIndex(root => root.IsPreferred);
            _dataRoots.SelectedIndex = preferred >= 0 ? preferred : 0;
        }
        AppendLog($"{roots.Count} diretório(s) de dados e {roots.Sum(root => ThunderbirdProfileService.FindProfiles(root).Count)} perfil(is) localizado(s).");
        RefreshProfilesForRoot();
    }

    private void RefreshProfilesForRoot()
    {
        var profiles = SelectedDataRoot is null
            ? new List<ThunderbirdProfileInfo>()
            : ThunderbirdProfileService.FindProfiles(SelectedDataRoot).ToList();
        _profiles.DisplayMember = nameof(ThunderbirdProfileInfo.DisplayText);
        _profiles.DataSource = profiles;
        if (profiles.Count > 0)
        {
            var preferred = profiles.FindIndex(profile => profile.IsDefault);
            _profiles.SelectedIndex = preferred >= 0 ? preferred : 0;
        }
        _destinationWasManuallySelected = false;
        UpdateAutomaticDestination();
    }

    private void SelectDataRoot()
    {
        var selected = SelectFolder(this, "Selecione a pasta Thunderbird que contém profiles.ini e Profiles");
        if (selected.Length == 0) return;
        var custom = ThunderbirdProfileService.CreateCustomDataRoot(selected);
        var roots = (_dataRoots.DataSource as List<ThunderbirdDataRootInfo>) ?? [];
        roots = roots.Where(item => !item.Path.Equals(custom.Path, StringComparison.OrdinalIgnoreCase)).ToList();
        roots.Insert(0, custom);
        _dataRoots.DataSource = roots;
        _dataRoots.SelectedIndex = 0;
    }

    private void BrowseDestination()
    {
        var selected = SelectFolder(this, "Selecione ou crie a pasta de destino", _destination.Text);
        if (selected.Length == 0) return;
        _destinationWasManuallySelected = true;
        _destination.Text = selected;
    }

    private void SelectTargetMode(ProfileRestoreTargetMode targetMode)
    {
        for (var index = 0; index < _targetMode.Items.Count; index++)
        {
            if (_targetMode.Items[index] is TargetItem item && item.Mode == targetMode)
            {
                _targetMode.SelectedIndex = index;
                break;
            }
        }
    }

    private void UpdateTargetState()
    {
        var target = SelectedTargetMode;
        var existingProfileRequired = target is ProfileRestoreTargetMode.ReplaceExistingProfile or ProfileRestoreTargetMode.RestoreMessagesToExisting;
        _profiles.Enabled = existingProfileRequired;
        _profileName.Enabled = target is ProfileRestoreTargetMode.CreateNewProfile or ProfileRestoreTargetMode.RestoreMessagesToExisting;
        _browseDestination.Enabled = target == ProfileRestoreTargetMode.ManualFolder || target == ProfileRestoreTargetMode.CreateNewProfile;
        _makeDefault.Enabled = target == ProfileRestoreTargetMode.CreateNewProfile;
        if (target != ProfileRestoreTargetMode.CreateNewProfile) _makeDefault.Checked = false;

        var destructive = target is ProfileRestoreTargetMode.ReplaceExistingProfile or ProfileRestoreTargetMode.RestoreThunderbirdDataRoot;
        _safety.Checked = destructive;
        _safety.Enabled = target == ProfileRestoreTargetMode.ManualFolder;
        _safetyFormat.Enabled = _safety.Checked;
        _overwrite.Checked = destructive || target == ProfileRestoreTargetMode.CreateNewProfile;
        _overwrite.Enabled = target == ProfileRestoreTargetMode.ManualFolder;
        _understandRisk.Enabled = destructive || target == ProfileRestoreTargetMode.ManualFolder;
        _confirmation.Enabled = destructive || target == ProfileRestoreTargetMode.ManualFolder;
        if (!destructive && target != ProfileRestoreTargetMode.ManualFolder)
        {
            _understandRisk.Checked = false;
            _confirmation.Clear();
        }

        _confirmationLabel.Text = target switch
        {
            ProfileRestoreTargetMode.ReplaceExistingProfile => "Digite SUBSTITUIR PERFIL:",
            ProfileRestoreTargetMode.RestoreThunderbirdDataRoot => "Digite SUBSTITUIR THUNDERBIRD:",
            ProfileRestoreTargetMode.ManualFolder => "Se a pasta já contém dados, digite RESTAURAR:",
            _ => "Nenhuma confirmação destrutiva necessária."
        };

        if (target == ProfileRestoreTargetMode.RestoreMessagesToExisting)
        {
            SelectMode(ProfileBackupMode.MessagesOnly);
            _mode.Enabled = false;
        }
        else if (target == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot)
        {
            SelectMode(ProfileBackupMode.Complete);
            _mode.Enabled = false;
        }
        else
        {
            _mode.Enabled = true;
        }

        _restoreLocalCache.Enabled = target == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot && _inspection?.Manifest?.IncludedLocalCache == true;
        if (!_restoreLocalCache.Enabled) _restoreLocalCache.Checked = false;
        _destinationWasManuallySelected = false;
        UpdateSelectionState();
        UpdateAutomaticDestination();
    }

    private void SelectMode(ProfileBackupMode mode)
    {
        for (var index = 0; index < _mode.Items.Count; index++)
        {
            if (_mode.Items[index] is ModeItem item && item.Mode == mode)
            {
                _mode.SelectedIndex = index;
                break;
            }
        }
    }

    private void UpdateAutomaticDestination()
    {
        if (_destinationWasManuallySelected || SelectedTargetMode == ProfileRestoreTargetMode.ManualFolder) return;
        var root = SelectedDataRoot;
        var profile = _profiles.SelectedItem as ThunderbirdProfileInfo;
        switch (SelectedTargetMode)
        {
            case ProfileRestoreTargetMode.CreateNewProfile when root is not null:
                _destination.Text = ThunderbirdProfileService.CreateNewProfileDestination(root, _profileName.Text, _newProfileToken);
                _destinationInfo.Text = "Será criado um novo perfil isolado e registrado automaticamente no profiles.ini. O perfil atual não será alterado.";
                break;
            case ProfileRestoreTargetMode.ReplaceExistingProfile when profile is not null:
                _destination.Text = profile.Path;
                _destinationInfo.Text = $"Perfil selecionado para substituição: {profile.DisplayText}. Backup de segurança obrigatório.";
                break;
            case ProfileRestoreTargetMode.RestoreMessagesToExisting when profile is not null:
                _destination.Text = profile.Path;
                _destinationInfo.Text = $"As mensagens serão colocadas em Mail\\Local Folders\\{_profileName.Text}.sbd, sem sobrescrever as caixas atuais.";
                break;
            case ProfileRestoreTargetMode.RestoreThunderbirdDataRoot when root is not null:
                _destination.Text = root.Path;
                _destinationInfo.Text = $"O diretório completo será restaurado em {root.Path}. Cache local: {root.LocalCachePath ?? "não detectado"}.";
                break;
        }
    }

    private void UpdateSelectionState()
    {
        var selective = _mode.Enabled && (_mode.SelectedItem as ModeItem)?.Mode == ProfileBackupMode.Selective;
        foreach (var check in new[] { _mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes, _cache })
            check.Enabled = selective;
    }

    private void ConfirmOperation(string destination)
    {
        var target = SelectedTargetMode;
        if (target == ProfileRestoreTargetMode.RestoreMessagesToExisting)
        {
            var response = MessageBox.Show(
                this,
                $"As mensagens serão importadas em uma pasta separada dentro do perfil:\n{destination}\n\nNenhuma caixa atual será sobrescrita. Continuar?",
                "Confirmar restauração de mensagens",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1);
            if (response != DialogResult.Yes) throw new OperationCanceledException("Restauração cancelada.");
            return;
        }

        var expectedText = target switch
        {
            ProfileRestoreTargetMode.ReplaceExistingProfile => "SUBSTITUIR PERFIL",
            ProfileRestoreTargetMode.RestoreThunderbirdDataRoot => "SUBSTITUIR THUNDERBIRD",
            ProfileRestoreTargetMode.ManualFolder when Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any() => "RESTAURAR",
            _ => string.Empty
        };
        if (expectedText.Length == 0) return;

        if (!_understandRisk.Checked)
            throw new InvalidOperationException("Marque a confirmação de que compreende o risco de perda de dados.");
        if (!_confirmation.Text.Trim().Equals(expectedText, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Digite {expectedText} no campo de confirmação.");
        if (ThunderbirdProfileService.IsThunderbirdRunning())
            throw new IOException("Feche completamente o Thunderbird antes da substituição.");
        if ((target is ProfileRestoreTargetMode.ReplaceExistingProfile or ProfileRestoreTargetMode.RestoreThunderbirdDataRoot) && !_safety.Checked)
            throw new InvalidOperationException("O backup de segurança é obrigatório para esta operação.");

        var response = MessageBox.Show(
            this,
            $"A operação substituirá dados em:\n{destination}\n\nUm backup de segurança em {SelectedSafetyFormat} será criado antes da alteração. Mesmo assim, falhas de disco ou interrupções podem causar perda de dados. Continuar?",
            "Confirmação crítica",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (response != DialogResult.Yes) throw new OperationCanceledException("Restauração cancelada na confirmação crítica.");
    }

    private async Task RestoreAsync()
    {
        await RunOperationAsync(_run, async token =>
        {
            if (string.IsNullOrWhiteSpace(_backup.Text) || !File.Exists(_backup.Text))
                throw new FileNotFoundException("Selecione um arquivo de backup existente.", _backup.Text);
            if (string.IsNullOrWhiteSpace(_destination.Text))
                throw new InvalidOperationException("Não foi possível determinar o destino da restauração.");

            var dataRoot = SelectedDataRoot;
            var target = SelectedTargetMode;
            var destination = Path.GetFullPath(_destination.Text);
            ConfirmOperation(destination);

            var mode = target == ProfileRestoreTargetMode.RestoreMessagesToExisting
                ? ProfileBackupMode.MessagesOnly
                : target == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot
                    ? ProfileBackupMode.Complete
                    : (_mode.SelectedItem as ModeItem)?.Mode ?? ProfileBackupMode.Complete;

            var options = new ProfileRestoreOptions
            {
                BackupPath = _backup.Text,
                DestinationProfilePath = destination,
                DestinationDataRootPath = dataRoot?.Path,
                DestinationLocalCachePath = dataRoot?.LocalCachePath,
                ArchivePassword = string.IsNullOrWhiteSpace(_password.Text) ? null : _password.Text,
                TargetMode = target,
                CreateSafetyBackup = _safety.Checked,
                SafetyBackupFormat = SelectedSafetyFormat,
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
                RegisterProfile = target == ProfileRestoreTargetMode.CreateNewProfile,
                RegisteredProfileName = _profileName.Text,
                MakeRegisteredProfileDefault = _makeDefault.Checked,
                MessagesSubfolderName = _profileName.Text,
                RestoreLocalCache = _restoreLocalCache.Checked
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
            if (result.ProfileRegistered) AppendLog($"Novo perfil registrado no Thunderbird: {result.RegisteredProfileName}.");
            foreach (var warning in result.Warnings.Take(50)) AppendLog("AVISO: " + warning);
            MessageBox.Show(this, $"Restauração concluída em:\n{result.DestinationProfilePath}", "Restauração concluída", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshSources();
        });
    }

    private sealed record TargetItem(string Text, ProfileRestoreTargetMode Mode);
    private sealed record ModeItem(string Text, ProfileBackupMode Mode);
    private sealed record FormatItem(string Text, ProfileBackupArchiveFormat Format);
}
