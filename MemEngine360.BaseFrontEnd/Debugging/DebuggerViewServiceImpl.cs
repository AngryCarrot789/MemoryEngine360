using System.Diagnostics;
using MemEngine360.Engine.Debugging;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.Debugging;

public class DebuggerViewServiceImpl : IDebuggerViewService {
    private static readonly DataKey<IWindow> OpenedWindowKey = DataKey<IWindow>.Create(nameof(IDebuggerViewService) + "_OpenedDebuggerWindow");
    
    public async Task<ITopLevel?> OpenOrFocusWindow(ConsoleDebugger debugger) {
        if (OpenedWindowKey.TryGetContext(debugger.Engine.UserData, out IWindow? debuggerWindow)) {
            Debug.Assert(debuggerWindow.OpenState == OpenState.Open || debuggerWindow.OpenState == OpenState.TryingToClose);
            
            debuggerWindow.Activate();
            return debuggerWindow;
        }

        if (!WindowContextUtils.TryGetWindowManagerWithUsefulWindow(out IWindowManager? manager, out _)) {
            return null;
        }

        DebuggerView control = new DebuggerView() {
            ConsoleDebugger = debugger
        };

        IWindow window = manager.CreateWindow(new WindowBuilder() {
            Title = "Console Debugger",
            FocusPath = "DebuggerWindow",
            Content = control,
            TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone4.Background.Static"),
            BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
            MinWidth = 400, MinHeight = 400,
            Width = 1280, Height = 720
        });

        window.WindowOpened += (sender, args) => ((DebuggerView) sender.Content!).OnWindowOpened(sender);
        window.WindowClosingAsync += (sender, args) => {
            return ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                // prevent memory leak
                DebuggerView view = (DebuggerView) sender.Content!;
                view.ConsoleDebugger!.Engine.UserData.Set(OpenedWindowKey, null);
                
                return view.OnClosingAsync(args.Reason);
            }).Unwrap();
        };
        
        window.WindowClosed += (sender, args) => ((DebuggerView) sender.Content!).OnWindowClosed();
        
        debugger.Engine.UserData.Set(OpenedWindowKey, window);
        await window.ShowAsync();

        return window;
    }
}