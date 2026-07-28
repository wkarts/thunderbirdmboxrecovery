using ThunderbirdMboxRecovery.Core;

namespace ThunderbirdMboxRecovery;

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "Sobre o Thunderbird Recovery Suite";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(700, 510);
        var applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (applicationIcon is not null) Icon = applicationIcon;

        var logo = new PictureBox
        {
            Image = DevBranding.LoadDeveloperLogo(),
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(20)
        };

        var title = new Label
        {
            Text = $"Thunderbird Recovery Suite {ApplicationVersion.Current}",
            AutoSize = true,
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0x00, 0x1D, 0x47)
        };

        var description = new Label
        {
            Text = "Suíte técnica para explorar, diagnosticar, reparar, extrair, indexar, fazer backup e restaurar caixas MBOX e perfis do Mozilla Thunderbird.",
            AutoSize = true,
            MaximumSize = new Size(390, 0)
        };

        var contacts = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 6, Dock = DockStyle.Top };
        contacts.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        contacts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddContact(contacts, 0, "Empresa:", DevBranding.Company);
        AddContact(contacts, 1, "Desenvolvedor:", DevBranding.Developer);
        AddContact(contacts, 2, "GitHub:", DevBranding.GithubHandle);
        AddContact(contacts, 3, "WhatsApp:", DevBranding.PhoneDisplay);
        AddContact(contacts, 4, "E-mail:", DevBranding.Email);
        AddContact(contacts, 5, "Arquitetura:", Environment.Is64BitProcess ? "Windows x64" : "Windows x86");

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, WrapContents = true };
        var github = new Button { Text = "Abrir GitHub", AutoSize = true };
        var whatsapp = new Button { Text = "Abrir WhatsApp", AutoSize = true };
        var email = new Button { Text = "Enviar e-mail", AutoSize = true };
        var copy = new Button { Text = "Copiar contatos", AutoSize = true };
        var close = new Button { Text = "Fechar", AutoSize = true, DialogResult = DialogResult.OK };
        actions.Controls.AddRange([github, whatsapp, email, copy, close]);

        github.Click += (_, _) => DevBranding.OpenUrl(DevBranding.GithubUrl);
        whatsapp.Click += (_, _) => DevBranding.OpenUrl($"https://wa.me/{DevBranding.PhoneDigits}");
        email.Click += (_, _) => DevBranding.OpenUrl($"mailto:{DevBranding.Email}");
        copy.Click += (_, _) =>
        {
            DevBranding.CopyToClipboard($"{DevBranding.Company}\r\n{DevBranding.Developer}\r\nGitHub: {DevBranding.GithubHandle}\r\nWhatsApp: {DevBranding.PhoneDisplay}\r\nE-mail: {DevBranding.Email}");
            MessageBox.Show(this, "Contatos copiados para a área de transferência.", "Contatos", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var right = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(12, 28, 24, 20)
        };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.Controls.Add(title, 0, 0);
        right.Controls.Add(description, 0, 1);
        right.Controls.Add(contacts, 0, 2);
        right.Controls.Add(new Label
        {
            Text = "A logomarca exibida nesta janela identifica o desenvolvedor e não substitui a identidade visual própria do produto.",
            AutoSize = true,
            MaximumSize = new Size(390, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 18, 0, 0)
        }, 0, 3);
        right.Controls.Add(actions, 0, 4);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(logo, 0, 0);
        root.Controls.Add(right, 1, 0);
        Controls.Add(root);
        AcceptButton = close;
        CancelButton = close;
    }

    private static void AddContact(TableLayoutPanel table, int row, string label, string value)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 5, 8, 3) }, 0, row);
        table.Controls.Add(new Label { Text = value, AutoSize = true, Margin = new Padding(0, 5, 0, 3) }, 1, row);
    }
}
