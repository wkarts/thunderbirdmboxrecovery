using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery;

public sealed class SplashForm : Form
{
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 30,
        TextAlign = ContentAlignment.MiddleCenter,
        Text = "Inicializando..."
    };

    public SplashForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.White;
        ClientSize = new Size(620, 390);

        var logo = new PictureBox
        {
            Image = DevBranding.LoadDeveloperLogo(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = 150,
            Height = 170,
            Anchor = AnchorStyles.None
        };

        var title = new Label
        {
            Text = "Thunderbird Recovery Suite",
            AutoSize = true,
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0x00, 0x1D, 0x47),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var version = new Label
        {
            Text = $"Versão {ApplicationVersion.Current} • {(Environment.Is64BitProcess ? "x64" : "x86")}",
            AutoSize = true,
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var developer = new Label
        {
            Text = $"Desenvolvido por\n{DevBranding.Company}\n{DevBranding.Developer}",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(0x00, 0x1D, 0x47),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(30, 20, 30, 45)
        };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        content.Controls.Add(logo, 0, 0);
        content.Controls.Add(title, 0, 1);
        content.Controls.Add(version, 0, 2);
        content.Controls.Add(developer, 0, 3);
        foreach (Control control in content.Controls)
            control.Anchor = AnchorStyles.None;

        var border = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(2),
            BackColor = Color.FromArgb(0x00, 0x1D, 0x47)
        };
        var interior = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        interior.Controls.Add(content);
        interior.Controls.Add(_status);
        border.Controls.Add(interior);
        Controls.Add(border);
    }

    public void UpdateStatus(string status)
    {
        _status.Text = status;
        _status.Refresh();
        Application.DoEvents();
    }
}
