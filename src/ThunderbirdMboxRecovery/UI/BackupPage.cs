using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class BackupPage : OperationPageBase
{
    private readonly ComboBox _scope = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 310 };
    private readonly ComboBox _dataRoots = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _profiles = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _refresh = new() { Text = "Detectar novamente", AutoSize = true };
    private readonly Button _manualRoot = new() { Text = "Diretório de dados", AutoSize = true };
    private readonly Button _manualProfile = new() { Text = "Selecionar perfil", AutoSize = true };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _format = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 185 };
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
    private readonly CheckBox _cache = new() { Text = "Caches internos do perfil", Checked = false, AutoSize = true };
    private readonly CheckBox _includeLocalCache = new() { Text = "Incluir AppData\\Local do Thunderbird", Checked = false, AutoSize = true };
    private readonly CheckBox _hashes = new() { Text = "Calcular SHA-256 por arquivo", Checked = true, AutoSize = true };
    private readonly CheckBox _allowOpen = new() { Text = "Permitir Thunderbird aberto (não recomendado)", Checked = false, AutoSize = true };
    private readonly Button _run = new() { Text = "Criar backup", AutoSize = true };
    private readonly Label _formatNote = new() { AutoSize = true, ForeColor = Color.DimGray };
    private readonly Label _sourceNote = new() { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(1160, 0) };

    public BackupPage()
    {
        Dock = DockStyle.Fill;
        _scope.DataSource = new[]
        {
            new ScopeItem("Perfil selecionado", ProfileBackupScope.SelectedProfile),
            new ScopeItem("Thunderbird completo — Roaming, perfis internos e configuração", ProfileBackupScope.ThunderbirdDataRoot)
        };
        _scope.DisplayMember = nameof(ScopeItem.Text);
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

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1 };
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

        form.Controls.Add(new Label { Text = "Escopo:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 0);
        form.Controls.Add(_scope, 1, 0);
        form.SetColumnSpan(_scope, 3);
        form.Controls.Add(new Label { Text = "Dados do Thunderbird:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 1);
        form.Controls.Add(_dataRoots, 1, 1);
        form.Controls.Add(_refresh, 2, 1);
        form.Controls.Add(_manualRoot, 3, 1);
        form.Controls.Add(new Label { Text = "Perfil:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 2);
        form.Controls.Add(_profiles, 1, 2);
        form.SetColumnSpan(_profiles, 2);
        form.Controls.Add(_manualProfile, 3, 2);
        form.Controls.Add(new Label { Text = "Destino:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, 3);
        form.Controls.Add(_destination, 1, 3);
        form.SetColumnSpan(_destination, 2);
        form.Controls.Add(_browse, 3, 3);

        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        options.Controls.Add(new Label { Text = "Modo:", AutoSize = true, Margin = new Padding(3, 7, 3, 3) });
        options.Controls.Add(_mode);
        options.Controls.Add(new Label { Text = "Formato:", AutoSize = true, Margin = new Padding(12, 7, 3, 3) });
        options.Controls.Add(_format);
        options.Controls.AddRange([_mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes, _cache, _includeLocalCache, _hashes, _allowOpen]);
        form.Controls.Add(options, 0, 4);
        form.SetColumnSpan(options, 4);
        form.Controls.Add(_sourceNote, 0, 5);
        form.SetColumnSpan(_sourceNote, 4);
        form.Controls.Add(_formatNote, 0, 6);
        form.SetColumnSpan(_formatNote, 4);

        root.Controls.Add(form, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "O backup completo usa o diretório Roaming do Thunderbird, incluindo profiles.ini, installs.ini e os perfis armazenados nessa raiz. Perfis absolutos externos devem ser copiados individualmente. O AppData\\Local contém principalmente caches e é opcional.",
            AutoSize = true,
            MaximumSize = new Size(1160, 0),
            ForeColor = Color.DarkSlateBlue
        }, 0, 1);
        root.Controls.Add(new Label
        {
            Text = "Feche o Thunderbird para obter uma cópia consistente. O arquivo é criado como .partial, validado e somente depois recebe o nome definitivo.",
            AutoSize = true,
            MaximumSize = new Size(1160, 0)
        }, 0, 2);
        root.Controls.Add(_run, 0, 3);
        root.Controls.Add(CreateOperationFooter(), 0, 5);
        Controls.Add(root);

        _refresh.Click += (_, _) => RefreshSources();
        _manualRoot.Click += (_, _) => SelectDataRoot();
        _manualProfile.Click += (_, _) => SelectProfile();
        _browse.Click += (_, _) => SelectDestination();
        _scope.SelectedIndexChanged += (_, _) => UpdateScopeState(resetDestination: true);
        _dataRoots.SelectedIndexChanged += (_, _) => RefreshProfilesForSelectedRoot();
        _mode.SelectedIndexChanged += (_, _) => UpdateSelectionState();
        _format.SelectedIndexChanged += (_, _) => UpdateFormat();
        _run.Click += async (_, _) => await BackupAsync();

        RefreshSources();
        SetDefaultDestination();
        UpdateScopeState(resetDestination: false);
        UpdateFormat();
    }

    private ProfileBackupScope SelectedScope =>
        (_scope.SelectedItem as ScopeItem)?.Scope ?? ProfileBackupScope.SelectedProfile;

    private ProfileBackupArchiveFormat SelectedFormat =>
        (_format.SelectedItem as FormatItem)?.Format ?? ProfileBackupArchiveFormat.Zip;

    private ThunderbirdDataRootInfo? SelectedDataRoot => _dataRoots.SelectedItem as ThunderbirdDataRootInfo;

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
        AppendLog($"{roots.Count} diretório(s) de dados do Thunderbird localizado(s).");
        RefreshProfilesForSelectedRoot();
    }

    private void RefreshProfilesForSelectedRoot()
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

        var root = SelectedDataRoot;
        var externalProfiles = root is null
            ? 0
            : profiles.Count(profile => !IsPathInside(root.Path, profile.Path));
        _sourceNote.Text = root is null
            ? "Nenhum diretório de dados foi detectado. Selecione-o manualmente."
            : $"Roaming/dados: {root.Path}\nCache local opcional: {root.LocalCachePath ?? "não detectado"}\nPerfis encontrados: {profiles.Count}." +
              (externalProfiles > 0
                  ? $"\nATENÇÃO: {externalProfiles} perfil(is) usa(m) caminho absoluto fora dessa raiz e deve(m) ser copiado(s) individualmente."
                  : string.Empty);
        UpdateScopeState(resetDestination: false);
    }

    private void SelectDataRoot()
    {
        var selected = SelectFolder(this, "Selecione a pasta Thunderbird que contém profiles.ini e a pasta Profiles");
        if (selected.Length == 0) return;
        var custom = ThunderbirdProfileService.CreateCustomDataRoot(selected);
        var roots = (_dataRoots.DataSource as List<ThunderbirdDataRootInfo>) ?? [];
        roots = roots.Where(item => !item.Path.Equals(custom.Path, StringComparison.OrdinalIgnoreCase)).ToList();
        roots.Insert(0, custom);
        _dataRoots.DataSource = roots;
        _dataRoots.SelectedIndex = 0;
    }

    private void SelectProfile()
    {
        var selected = SelectFolder(this, "Selecione a pasta do perfil do Thunderbird");
        if (selected.Length == 0) return;
        ThunderbirdProfileService.ValidateProfile(selected);
        var root = SelectedDataRoot;
        var profile = new ThunderbirdProfileInfo
        {
            Name = Path.GetFileName(selected) ?? "Perfil",
            Path = selected,
            IsDefault = false,
            IsRelative = false,
            EstimatedBytes = 0,
            DataRootPath = root?.Path,
            DataRootType = root?.Type ?? ThunderbirdDataRootType.Custom,
            IsInUse = ThunderbirdProfileService.IsProfileInUse(selected),
            HasMail = Directory.Exists(Path.Combine(selected, "Mail")),
            HasImapMail = Directory.Exists(Path.Combine(selected, "ImapMail"))
        };
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
            FileName = Path.GetFileName(_destination.Text) ?? string.Empty,
            InitialDirectory = Path.GetDirectoryName(_destination.Text) ?? string.Empty
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _destination.Text = dialog.FileName;
    }

    private void SetDefaultDestination()
    {
        var extension = SelectedFormat == ProfileBackupArchiveFormat.SevenZip ? ".7z" : ".zip";
        var kind = SelectedScope == ProfileBackupScope.ThunderbirdDataRoot ? "Completo" : "Perfil";
        _destination.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            $"Thunderbird_Backup_{kind}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
    }

    private void UpdateFormat()
    {
        var extension = SelectedFormat == ProfileBackupArchiveFormat.SevenZip ? ".7z" : ".zip";
        if (string.IsNullOrWhiteSpace(_destination.Text)) SetDefaultDestination();
        else _destination.Text = Path.ChangeExtension(_destination.Text, extension);
        _formatNote.Text = SelectedFormat == ProfileBackupArchiveFormat.SevenZip
            ? "7Z selecionado: compactação LZMA2, normalmente menor para perfis com muitos arquivos."
            : "ZIP selecionado: maior compatibilidade nativa com o Windows.";
    }

    private void UpdateScopeState(bool resetDestination)
    {
        var fullRoot = SelectedScope == ProfileBackupScope.ThunderbirdDataRoot;
        _profiles.Enabled = !fullRoot;
        _manualProfile.Enabled = !fullRoot;
        _mode.Enabled = !fullRoot;
        var localCachePath = SelectedDataRoot?.LocalCachePath;
        _includeLocalCache.Enabled = fullRoot && !string.IsNullOrWhiteSpace(localCachePath) && Directory.Exists(localCachePath);
        if (!_includeLocalCache.Enabled) _includeLocalCache.Checked = false;
        if (fullRoot)
        {
            _mode.SelectedIndex = 0;
            _run.Text = "Criar backup completo do Thunderbird";
        }
        else
        {
            _run.Text = "Criar backup do perfil";
        }
        UpdateSelectionState();
        if (resetDestination) SetDefaultDestination();
    }

    private void UpdateSelectionState()
    {
        var fullRoot = SelectedScope == ProfileBackupScope.ThunderbirdDataRoot;
        var selective = !fullRoot && (_mode.SelectedItem as ModeItem)?.Mode == ProfileBackupMode.Selective;
        foreach (var check in new[] { _mail, _imap, _preferences, _addresses, _calendar, _credentials, _extensions, _indexes, _cache })
            check.Enabled = selective;
    }

    private async Task BackupAsync()
    {
        await RunOperationAsync(_run, async token =>
        {
            var dataRoot = SelectedDataRoot ?? throw new InvalidOperationException("Selecione o diretório de dados do Thunderbird.");
            var profile = _profiles.SelectedItem as ThunderbirdProfileInfo ?? new ThunderbirdProfileInfo
            {
                Name = "Thunderbird completo",
                Path = dataRoot.Path,
                IsDefault = false,
                IsRelative = false,
                EstimatedBytes = 0,
                DataRootPath = dataRoot.Path,
                DataRootType = dataRoot.Type
            };
            if (SelectedScope == ProfileBackupScope.SelectedProfile && _profiles.SelectedItem is not ThunderbirdProfileInfo)
                throw new InvalidOperationException("Selecione o perfil que será copiado.");

            var mode = SelectedScope == ProfileBackupScope.ThunderbirdDataRoot
                ? ProfileBackupMode.Complete
                : (_mode.SelectedItem as ModeItem)?.Mode ?? ProfileBackupMode.Complete;
            var options = new ProfileBackupOptions
            {
                Profile = profile,
                DataRoot = dataRoot,
                Scope = SelectedScope,
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
                AllowInUseProfile = _allowOpen.Checked,
                IncludeLocalCache = _includeLocalCache.Checked
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

    private static bool IsPathInside(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ScopeItem(string Text, ProfileBackupScope Scope);
    private sealed record ModeItem(string Text, ProfileBackupMode Mode);
    private sealed record FormatItem(string Text, ProfileBackupArchiveFormat Format);
}
