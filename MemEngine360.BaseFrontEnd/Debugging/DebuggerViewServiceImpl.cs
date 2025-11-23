using System.Diagnostics;
using MemEngine360.Engine.Debugging;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.Debugging;

public class DebuggerViewServiceImpl : IDebuggerViewService {
    private static readonly DataKey<IDesktopWindow> OpenedWindowKey = DataKeys.Create<IDesktopWindow>(nameof(IDebuggerViewService) + "_OpenedDebuggerWindow");
    
    public async Task<ITopLevel?> OpenOrFocusWindow(ConsoleDebugger debugger) {
        if (OpenedWindowKey.TryGetContext(debugger.Engine.UserContext, out IDesktopWindow? debuggerWindow)) {
            Debug.Assert(debuggerWindow.OpenState.IsOpenOrTryingToClose());
            
            debuggerWindow.Activate();
            return debuggerWindow;
        }

        if (!WindowContextUtils.TryGetWindowManagerWithUsefulWindow(out IWindowManager? manager, out _)) {
            return null;
        }

        DebuggerView control = new DebuggerView() {
            ConsoleDebugger = debugger
        };

        IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
            Title = "Console Debugger",
            FocusPath = "DebuggerWindow",
            Content = control,
            TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone4.Background.Static"),
            BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
            MinWidth = 400, MinHeight = 400,
            Width = 1280, Height = 720
        });

        window.Opened += (s, args) => ((DebuggerView) ((IDesktopWindow) s!).Content!).OnWindowOpened((IDesktopWindow) s!);
        window.ClosingAsync += (s, args) => {
            return ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                // prevent memory leak
                DebuggerView view = (DebuggerView) ((IDesktopWindow) s!).Content!;
                view.ConsoleDebugger!.Engine.UserContext.Remove(OpenedWindowKey);
                
                return view.OnClosingAsync(args.Reason);
            }).Unwrap();
        };
        
        window.Closed += (s, args) => ((DebuggerView) ((IDesktopWindow) s!).Content!).OnWindowClosed();
        
        debugger.Engine.UserContext.Set(OpenedWindowKey, window);
        await window.ShowAsync();

        return window;
    }
}