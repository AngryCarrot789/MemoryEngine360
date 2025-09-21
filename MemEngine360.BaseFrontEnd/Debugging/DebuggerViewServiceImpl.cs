using MemEngine360.Engine.Debugging;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

// using PFXToolKitUI.Avalonia.Utils;
// using PFXToolKitUI.Interactivity.Contexts;

namespace MemEngine360.BaseFrontEnd.Debugging;

public class DebuggerViewServiceImpl : IDebuggerViewService {
    // private static readonly DataKey<SingletonWindow> DebuggerWindowKey = DataKey<SingletonWindow>.Create("DebuggerWindow");
    //
    // public Task ShowDebugger(ConsoleDebugger debugger) {
    //     if (!WindowingSystem.TryGetInstance(out WindowingSystem? instance))
    //         return Task.CompletedTask;
    //
    //     if (!DebuggerWindowKey.TryGetContext(debugger.Engine.UserData, out SingletonWindow? window)) {
    //         debugger.Engine.UserData.Set(DebuggerWindowKey, window = new SingletonWindow(() => new DebuggerWindow() {
    //             ConsoleDebugger = debugger
    //         }));
    //     }
    //
    //     window.ShowOrActivate();
    //     return Task.CompletedTask;
    // }

    public async Task<ITopLevel?> ShowDebugger(ConsoleDebugger debugger) {
        if (!WindowContextUtils.TryGetWindowManagerWithUsefulWindow(out IWindowManager? manager, out _)) {
            if (!IWindowManager.TryGetInstance(out manager)) {
                return null;
            }
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
        window.WindowClosingAsync += (sender, args) => ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => ((DebuggerView) sender.Content!).OnClosingAsync(args.Reason)).Unwrap();
        window.WindowClosed += (sender, args) => ((DebuggerView) sender.Content!).OnWindowClosed();
        await window.ShowAsync();

        return window;
    }
}