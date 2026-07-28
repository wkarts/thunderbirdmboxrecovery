using System.Diagnostics;
using System.Reflection;

namespace ThunderbirdMboxRecovery.Core;

public static class DevBranding
{
    public const string Company = "WWSoftware's Sistemas e Tecnologias";
    public const string Developer = "Wallace Kleiton";
    public const string GithubHandle = "@wkarts";
    public const string GithubUrl = "https://github.com/wkarts";
    public const string PhoneDisplay = "+55 75 98844-9231";
    public const string PhoneDigits = "5575988449231";
    public const string Email = "wkarts@gmail.com";

    public static Image LoadDeveloperLogo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("wwsoftwares-dev-logo.png", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return new Bitmap(1, 1);

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("O recurso da logomarca do desenvolvedor não foi encontrado.");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    public static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static void CopyToClipboard(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            Clipboard.SetText(value);
    }
}
