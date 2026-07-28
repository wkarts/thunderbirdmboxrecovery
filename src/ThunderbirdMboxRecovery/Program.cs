namespace ThunderbirdMboxRecovery;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) =>
            MessageBox.Show(args.Exception.ToString(), "Erro inesperado", MessageBoxButtons.OK, MessageBoxIcon.Error);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            MessageBox.Show(args.ExceptionObject?.ToString() ?? "Erro desconhecido.", "Erro inesperado", MessageBoxButtons.OK, MessageBoxIcon.Error);

        using var splash = new SplashForm();
        splash.Show();
        splash.UpdateStatus("Carregando módulos de recuperação...");
        Thread.Sleep(250);
        splash.UpdateStatus("Validando ambiente Windows...");
        Thread.Sleep(200);
        var mainForm = new MainForm();
        splash.UpdateStatus("Interface pronta.");
        Thread.Sleep(200);
        splash.Close();
        Application.Run(mainForm);
    }
}
