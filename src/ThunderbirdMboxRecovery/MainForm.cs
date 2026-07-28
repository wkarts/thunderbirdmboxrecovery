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

        var menu = BuildMenu();
        var tabs = new TabControl { Dock = DockStyle.Fill };
        AddTab(tabs, "Visão geral", new DashboardPage());
        AddTab(tabs, "Explorar", new ExplorePage());
        AddTab(tabs, "Testar", new TestPage());
        AddTab(tabs, "Reparar", new RepairPage());
        AddTab(tabs, "Extrair EML", new ExtractPage());
        AddTab(tabs, "Indexar MSF", new IndexPage());
        AddTab(tabs, "Backup", new BackupPage());
        AddTab(tabs, "Restaurar", new RestorePage());

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(menu, 0, 0);
        root.Controls.Add(tabs, 0, 1);
        Controls.Add(root);
        MainMenuStrip = menu;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { Dock = DockStyle.Fill };
        var file = new ToolStripMenuItem("Arquivo");
        var exit = new ToolStripMenuItem("Sair");
        exit.Click += (_, _) => Close();
        file.DropDownItems.Add(exit);

        var help = new ToolStripMenuItem("Ajuda");
        var about = new ToolStripMenuItem("Sobre o Thunderbird Recovery Suite");
        about.Click += (_, _) =>
        {
            using var dialog = new AboutForm();
            dialog.ShowDialog(this);
        };
        help.DropDownItems.Add(about);
        menu.Items.AddRange([file, help]);
        return menu;
    }

    private static void AddTab(TabControl tabs, string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(8) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        tabs.TabPages.Add(page);
    }
}
