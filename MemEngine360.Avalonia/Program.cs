// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemoryEngine360.
// 
// MemoryEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemoryEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

#if !DEBUG
#define PROCESS_APP_EXCEPTION
using System.Text;
using PFXToolKitUI.Logging;
#endif

using System;
using Avalonia;

namespace MemEngine360.Avalonia;

class Program {
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) {
#if PROCESS_APP_EXCEPTION
        try {
#endif
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
#if PROCESS_APP_EXCEPTION
        }
#endif
#if PROCESS_APP_EXCEPTION
        catch (Exception e) {
            string? filePath = args.Length > 0 ? args[0] : null;
            if (string.IsNullOrEmpty(filePath)) {
                string[] trueArgs = Environment.GetCommandLineArgs();
                if (trueArgs.Length > 0)
                    filePath = trueArgs[0];
            }

            string? dirPath = System.IO.Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dirPath) && System.IO.Directory.Exists(dirPath)) {
                try {
                    StringBuilder sb = new StringBuilder(2048);
                    sb.AppendLine(PFXToolKitUI.Utils.ExceptionUtils.GetToString(e));
                    sb.AppendLine();
                    sb.AppendLine("App Logs");
                    foreach (LogEntry entry in AppLogger.Instance.Entries) {
                        sb.AppendLine(entry.ToString());
                    }
                    
                    System.IO.File.WriteAllText(System.IO.Path.Combine(dirPath, "MemoryEngine360_LastCrashError.txt"), sb.ToString());
                }
                catch { 
                    // ignored
                }
            }

            throw;
        }
#endif
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().
                      UsePlatformDetect().
                      WithInterFont().
                      // We use LowLatencyDxgiSwapChain so that we can use custom title bar close/minimize/etc buttons that allow mouse hit testing
                      With(new Win32PlatformOptions() {
                          CompositionMode = [Win32CompositionMode.LowLatencyDxgiSwapChain],
                          RenderingMode = [Win32RenderingMode.AngleEgl],
                          DpiAwareness = Win32DpiAwareness.PerMonitorDpiAware
                      }).
                      LogToTrace();
}