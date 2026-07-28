using ThunderbirdMboxRecovery.Core;
using ThunderbirdMboxRecovery.UI;

namespace ThunderbirdMboxRecovery;

public sealed class MainForm : Form
{
    public MainForm()
    {
        Text = $"Thunderbird Recovery Suite {ApplicationVersion.Current}";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        var applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (applicationIcon is not null) Icon = applicationIcon;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        AddTab(tabs, "Visão geral", new DashboardPage());
        AddTab(tabs, "Explorar", new ExplorePage());
        AddTab(tabs, "Testar", new TestPage());
        AddTab(tabs, "Reparar", new RepairPage());
        AddTab(tabs, "Extrair EML", new ExtractPage());
        AddTab(tabs, "Indexar MSF", new IndexPage());
        AddTab(tabs, "Backup", new BackupPage());
        AddTab(tabs, "Restaurar", new RestorePage());
        Controls.Add(tabs);
    }

    private static void AddTab(TabControl tabs, string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(8) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        tabs.TabPages.Add(page);
    }
}
