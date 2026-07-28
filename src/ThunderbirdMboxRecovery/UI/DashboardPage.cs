using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery.UI;

public sealed class DashboardPage : UserControl
{
    private readonly ListView _installations = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
    private readonly ListView _profiles = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
    private readonly Button _refresh = new() { Text = "Atualizar diagnóstico do ambiente", AutoSize = true };
    private readonly Label _summary = new() { AutoSize = true };

    public DashboardPage()
    {
        Dock = DockStyle.Fill;
        _installations.Columns.Add("Versão", 120);
        _installations.Columns.Add("Arquitetura", 100);
        _installations.Columns.Add("Executável", 650);
        _profiles.Columns.Add("Perfil", 220);
        _profiles.Columns.Add("Padrão", 80);
        _profiles.Columns.Add("Tamanho estimado", 140);
        _profiles.Columns.Add("Pasta", 650);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.Controls.Add(new Label
        {
            Text = $"Thunderbird Recovery Suite {ApplicationVersion.Current}",
            Font = new Font(Font, FontStyle.Bold),
            AutoSize = true
        }, 0, 0);
        root.Controls.Add(new Label
        {
            Text = "Suíte integrada para explorar, testar, reparar, extrair, indexar, fazer backup e restaurar caixas e perfis do Thunderbird.",
            AutoSize = true
        }, 0, 1);
        var top = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        top.Controls.Add(_refresh);
        top.Controls.Add(_summary);
        root.Controls.Add(top, 0, 2);
        root.Controls.Add(_installations, 0, 3);
        root.Controls.Add(new Label { Text = "Perfis detectados", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 4);
        root.Controls.Add(_profiles, 0, 5);
        Controls.Add(root);

        _refresh.Click += (_, _) => RefreshEnvironment();
        RefreshEnvironment();
    }

    private void RefreshEnvironment()
    {
        _installations.Items.Clear();
        var installations = ThunderbirdLocator.FindInstallations();
        foreach (var installation in installations)
        {
            var item = new ListViewItem(installation.Version);
            item.SubItems.Add(installation.Architecture.ToString());
            item.SubItems.Add(installation.ExecutablePath);
            _installations.Items.Add(item);
        }

        _profiles.Items.Clear();
        var profiles = ThunderbirdProfileService.FindProfiles();
        foreach (var profile in profiles)
        {
            var item = new ListViewItem(profile.Name);
            item.SubItems.Add(profile.IsDefault ? "Sim" : "Não");
            item.SubItems.Add(SizeFormatter.Format(profile.EstimatedBytes));
            item.SubItems.Add(profile.Path);
            _profiles.Items.Add(item);
        }

        _summary.Text = $"Instalações: {installations.Count}; perfis: {profiles.Count}.";
    }
}
