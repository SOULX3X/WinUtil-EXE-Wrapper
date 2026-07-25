using System.Diagnostics;
using System.Security.Principal;

namespace WinUtilWrapper;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // The App.manifest requests requireAdministrator, so launching
        // via double-click or Start menu triggers UAC automatically.
        // But we also self-elevate for programmatic invocations where
        // the manifest might not apply (e.g. CMD running as standard user).
        if (!IsRunAsAdmin())
        {
            return RelaunchAsAdmin(args) ? 0 : 1;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(args));
        return 0;
    }

    // ---- elevation ------------------------------------------------------

    private static bool IsRunAsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool RelaunchAsAdmin(string[] args)
    {
        var exe = Environment.ProcessPath
                  ?? Process.GetCurrentProcess().MainModule?.FileName;

        if (exe is null)
        {
            MessageBox.Show("Cannot determine executable path for elevation.",
                "WinUtil Wrapper", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                UseShellExecute = true,
                Verb = "runas",               // triggers UAC
                FileName = exe,
                Arguments = QuoteArgs(args),
            };
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Administrator privileges are required:\n\n" + ex.Message,
                "WinUtil Wrapper", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private static string QuoteArgs(string[] args)
    {
        return string.Join(" ", args.Select(a =>
            a.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0
                ? "\"" + a.Replace("\"", "\\\"") + "\""
                : a));
    }
}