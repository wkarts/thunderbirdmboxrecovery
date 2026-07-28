namespace ThunderbirdMboxRecovery.UI;

public abstract class OperationPageBase : UserControl
{
    protected readonly Label StatusLabel = new() { AutoSize = true, Text = "Pronto." };
    protected readonly ProgressBar ProgressBar = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
    protected readonly TextBox LogBox = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font(FontFamily.GenericMonospace, 9f)
    };
    protected readonly Button CancelButton = new() { Text = "Cancelar", AutoSize = true, Enabled = false };
    private CancellationTokenSource? _cancellation;
    private bool _running;

    protected OperationPageBase()
    {
        CancelButton.Click += (_, _) => _cancellation?.Cancel();
    }

    protected Control CreateOperationFooter()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            AutoSize = false
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.Controls.Add(StatusLabel, 0, 0);
        table.Controls.Add(ProgressBar, 1, 0);
        table.Controls.Add(CancelButton, 2, 0);
        table.Controls.Add(LogBox, 0, 1);
        table.SetColumnSpan(LogBox, 3);
        return table;
    }

    protected async Task RunOperationAsync(Control trigger, Func<CancellationToken, Task> operation)
    {
        if (_running) return;
        _running = true;
        _cancellation = new CancellationTokenSource();
        trigger.Enabled = false;
        CancelButton.Enabled = true;
        UseWaitCursor = true;
        ProgressBar.Style = ProgressBarStyle.Marquee;
        ProgressBar.MarqueeAnimationSpeed = 25;
        try
        {
            await operation(_cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            AppendLog("Operação cancelada pelo usuário.");
            StatusLabel.Text = "Cancelado.";
        }
        catch (Exception exception)
        {
            AppendLog("ERRO: " + exception);
            StatusLabel.Text = "Falha.";
            MessageBox.Show(this, exception.Message, "Falha na operação", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ProgressBar.MarqueeAnimationSpeed = 0;
            ProgressBar.Style = ProgressBarStyle.Continuous;
            ProgressBar.Value = 0;
            UseWaitCursor = false;
            CancelButton.Enabled = false;
            trigger.Enabled = true;
            _cancellation.Dispose();
            _cancellation = null;
            _running = false;
        }
    }

    protected void ReportProgress(long processed, long total, string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke((Action)(() => ReportProgress(processed, total, status)));
            return;
        }

        StatusLabel.Text = status;
        if (total > 0)
        {
            ProgressBar.Style = ProgressBarStyle.Continuous;
            var percentage = (int)Math.Clamp(processed * 100L / total, 0, 100);
            ProgressBar.Value = percentage;
        }
    }

    protected void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke((Action)(() => AppendLog(message)));
            return;
        }

        LogBox.AppendText($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
    }

    protected static TextBox CreatePathTextBox() => new() { Dock = DockStyle.Fill };
    protected static Button CreateBrowseButton(string text = "Procurar...") => new() { Text = text, AutoSize = true };

    protected static string SelectFolder(IWin32Window owner, string description, string? initial = null)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = initial ?? string.Empty
        };
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.SelectedPath : string.Empty;
    }
}
