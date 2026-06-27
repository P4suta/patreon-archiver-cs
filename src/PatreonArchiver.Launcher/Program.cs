using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PatreonArchiver.Launcher;

/// <summary>
/// Native (NativeAOT) launcher shipped at the bundle root as <c>PatreonArchiver.exe</c>
/// — the single file a user double-clicks. It spawns the real app at
/// <c>app\PatreonArchiver.App.exe</c>, forwards any arguments, then exits;
/// everything else in the bundle lives under <c>app\</c> so the folder root has
/// one obvious thing to run. Assembled into the bundle by <c>eng/pack.ps1</c>.
/// </summary>
internal static partial class Program
{
    private const string AppSubdir = "app";
    private const string AppExe = "PatreonArchiver.App.exe";

    private static int Main()
    {
        // The launcher sits at the bundle root; the app lives in app\ beside it.
        var appDir = Path.Combine(AppContext.BaseDirectory, AppSubdir);
        var target = Path.Combine(appDir, AppExe);

        if (!File.Exists(target))
        {
            Fail($"Could not find {AppSubdir}\\{AppExe} next to this launcher.\n\n" +
                 $"Keep PatreonArchiver.exe inside the extracted folder, beside the " +
                 $"{AppSubdir}\\ subfolder.");
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = target,
            WorkingDirectory = appDir,
            UseShellExecute = false,
        };
        // Forward our own arguments (skip [0] = this exe path) to the app.
        var args = Environment.GetCommandLineArgs();
        for (var i = 1; i < args.Length; i++)
        {
            psi.ArgumentList.Add(args[i]);
        }

        try
        {
            // Launch-and-exit: the app owns its own lifetime from here.
            using var app = Process.Start(psi);
            return app is null ? 1 : 0;
        }
        catch (Exception ex)
        {
            Fail($"Failed to start {AppSubdir}\\{AppExe}:\n\n{ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Surface a fatal launch error in a dialog — a GUI-subsystem exe has no
    /// console, so without this the user would see nothing happen at all.
    /// </summary>
    private static void Fail(string message)
    {
        const uint MB_ICONERROR = 0x10;
        _ = MessageBoxW(IntPtr.Zero, message, "PatreonArchiver", MB_ICONERROR);
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
