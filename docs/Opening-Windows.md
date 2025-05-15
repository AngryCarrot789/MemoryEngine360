# Opening windows

This describes how to open custom windows while supporting our window manager.

This is some example code:

```
if (WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
    var myCustomWindow = new MyCustomWindow() {
        Model = ...
    }

    system.Register(myCustomWindow).Show();
}
```

If you need access to the currently active window, you can use: 

`DesktopWindow? active = system.GetActiveWindowOrNull()` 

or the alternative 

`if (system.TryGetActiveWindow(out DesktopWindow? active))`

# Why?
It's important to call the windowing system's `Register()` method on all windows you want to open, so that
they can participate in things like automatic `MainWindow` assignment, tracking the last window to be Activated, 
and potentially more in the future.

