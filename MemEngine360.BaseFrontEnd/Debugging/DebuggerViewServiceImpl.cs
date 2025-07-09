using MemEngine360.Engine.Debugging;
using PFXToolKitUI.Avalonia.Services.Windowing;

namespace MemEngine360.BaseFrontEnd.Debugging;

public class DebuggerViewServiceImpl : IDebuggerViewService {
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