# WinUtil EXE Wrapper

A small [WinForms](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview) / .NET 8 launcher that auto-updates [`winutil.ps1`](https://github.com/ChrisTitusTech/winutil) on every launch, then runs the latest script as Administrator.

## What It Does

| Step | Details |
|------|---------|
| **Elevate** | App.manifest demands `requireAdministrator` so UAC triggers automatically. Falls back to `runas` self-elevation for programmatic spins. |
| **Check for updates** | Calls the GitHub Releases API once per hour. If a newer tag is found, downloads the bundled `winutil.ps1` from the latest release. Caches script + version stamp in `%LOCALAPPDATA%\WinUtilWrapper\`. |
| **Launch** | Runs the cached script with `pwsh.exe` (7+ if installed, otherwise `powershell.exe`) → `-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File`. The PowerShell console window is hidden; only winutil's WPF UI is visible. |
| **Args** | All CLI arguments (`-Preset Standard`, `-Config`, `-Offline`, …) are forwarded verbatim. |
| **Exit** | The wrapper stays hidden while winutil runs and exits cleanly when the user closes winutil's window. |

## Cache Location

```
%LOCALAPPDATA%\WinUtilWrapper\
    winutil.ps1      ← latest script body
    version.txt      ← tag name
    lastcheck.txt    ← prevents hammering the GitHub API
```

Delete any/all to force a fresh download.

## Building

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (Windows x64)
- PowerShell 5+ or `pwsh`

### Quick Build (default — standalone single-file EXE, ~70 MB)

```powershell
cd wrapper
.\build.ps1
```

Output: `wrapper\publish\WinUtil.exe` — one self-contained EXE, no DLLs, no .NET install needed.

The PowerShell console window is hidden at launch — the user sees only winutil's WPF UI.

### Framework-Dependent Build (~1 MB, needs .NET 8 runtime)

```powershell
.\build.ps1 -FrameworkDependent
```

Smaller output but requires the .NET 8 Desktop Runtime on the target machine.

### Manual `dotnet publish`

```powershell
dotnet publish -c Release -r win-x64 -o publish
```

To override to framework-dependent:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

---

If you want a signed or branded binary, just run `build.ps1` and sign the resulting `publish\WinUtil.exe` with your code-signing certificate.