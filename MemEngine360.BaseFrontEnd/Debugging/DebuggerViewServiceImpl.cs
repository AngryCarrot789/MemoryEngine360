using MemEngine360.Engine.Debugging;
using PFXToolKitUI.Avalonia.Services.Windowing;

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

    public Task ShowDebugger(ConsoleDebugger debugger) {
        if (!WindowingSystem.TryGetInstance(out WindowingSystem? instance))
            return Task.CompletedTask;

        DebuggerWindow window = new DebuggerWindow() {
            ConsoleDebugger = debugger
        };

        instance.Register(window).Show();
        return Task.CompletedTask;
    }
}