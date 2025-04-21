using Avalonia;
using System;

namespace MemEngine360.Avalonia;

class Program {
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) {
#if !DEBUG
        try {
#endif
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
#if !DEBUG
        }
#endif
#if !DEBUG
        catch (Exception e) {
            string? filePath = args.Length > 0 ? args[0] : null;
            if (string.IsNullOrEmpty(filePath)) {
                string[] trueArgs = Environment.GetCommandLineArgs();
                if (trueArgs.Length > 0)
                    filePath = trueArgs[0];
            }

            string? dirPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dirPath) && Directory.Exists(dirPath)) {
                try {
                    File.WriteAllText(Path.Combine(dirPath, "VWeaponEditor_LastCrashError.txt"), e.ToString());
                }
                catch { /* ignored */ }
            }

            throw;
        }
#endif
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().
                      UsePlatformDetect().
                      WithInterFont().
                      With(new Win32PlatformOptions() { CompositionMode = [Win32CompositionMode.LowLatencyDxgiSwapChain], RenderingMode = [Win32RenderingMode.AngleEgl] }).
                      LogToTrace();
}