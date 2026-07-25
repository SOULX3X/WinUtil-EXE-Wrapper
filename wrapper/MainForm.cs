using System.Diagnostics;

namespace WinUtilWrapper;

/// <summary>
/// Small splash window shown while the wrapper updates the script
/// and launches winutil's WPF UI. Stays hidden while winutil is
/// running and exits when the user closes winutil.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly string[] _args;
    private readonly Label _status;
    private readonly ProgressBar _progress;
    private CancellationTokenSource? _cts;

    public MainForm(string[] args)
    {
        _args = args;

        // ---- form appearance --------------------------------------------
        Text = "WinUtil Wrapper";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        ClientSize = new Size(460, 120);
        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        // ---- title label -----------------------------------------------
        var title = new Label
        {
            Text = "Chris Titus Tech's Windows Utility",
            Font = new Font("Segoe UI Semibold", 10.5F),
            ForeColor = Color.FromArgb(0, 120, 212),
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleCenter,
            Height = 32,
        };

        // ---- status label -----------------------------------------------
        _status = new Label
        {
            Text = "Starting...",
            Dock = DockStyle.Top,
            TextAlign = ContentAlignment.MiddleCenter,
            Height = 24,
            Padding = new Padding(16, 0, 16, 0),
        };

        // ---- progress bar -----------------------------------------------
        _progress = new ProgressBar
        {
            Dock = DockStyle.Bottom,
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30,
            Height = 8,
            Margin = new Padding(0, 8, 0, 16),
        };

        // ---- build-up order determines z-index --------------------------
        Controls.Add(_progress);
        Controls.Add(_status);
        Controls.Add(title);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RunAsync();
    }

    // ---- flow -----------------------------------------------------------

    private async Task RunAsync()
    {
        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(msg => _status.Text = msg);

        // 1 — Update check + download -------------------------------------
        Updater.UpdateResult update;
        try
        {
            update = await Updater.EnsureLatestAsync(progress, _cts.Token);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "WinUtil Wrapper — Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
            return;
        }

        if (update.Updated)
            _status.Text = $"Updated to winutil {update.Version} — launching...";
        else
            _status.Text = $"Launching winutil {update.Version}...";
        _progress.Value = 100;

        // 2 — Hide ourselves, launch, and wait for winutil to exit ---------
        Hide();
        try
        {
            Process? ps;
            try
            {
                ps = LaunchPowerShellAsync(update.ScriptPath, _args);
            }
            catch (Exception ex)
            {
                Show(); // pop back up to show user the error
                MessageBox.Show(this, ex.ToString(), "WinUtil Wrapper — Launch Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }

            // Wait for PowerShell to exit (i.e. user closed winutil's UI).
            // Run on thread-pool so the form's message pump stays alive.
            await Task.Run(() => ps!.WaitForExit(), CancellationToken.None);
        }
        finally
        {
            Application.Exit();
        }
    }

    // ---- PowerShell launch ----------------------------------------------

    private static Process LaunchPowerShellAsync(string scriptPath, string[]? args = null)
    {
        var psPath = Updater.PickPowerShell();
        var argsStr = args is { Length: > 0 }
            ? " " + string.Join(" ", args.Select(QuoteArg))
            : "";

        // The console host is kept invisible on both axes:
        //   - ProcessWindowStyle.Hidden (Win32 STARTUPINFO) for the process window, and
        //   - -WindowStyle Hidden passed to PowerShell itself as a backstop.
        // winutil's WPF UI opens its own independent top-level windows, so the
        // GUI still appears normally — only the empty blue console box is gone.
        var psi = new ProcessStartInfo
        {
            FileName = psPath,
            Arguments =
                $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"{argsStr}",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        return Process.Start(psi)
               ?? throw new InvalidOperationException($"Failed to start {psPath}.");
    }

    // ---- helpers --------------------------------------------------------

    private static string QuoteArg(string arg) =>
        arg.Contains(' ') || arg.Contains('"')
            ? $"\"{arg.Replace("\"", "\\\"")}\""
            : arg;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnFormClosing(e);
    }
}