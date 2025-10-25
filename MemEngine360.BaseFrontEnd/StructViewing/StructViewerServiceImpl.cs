using System.Diagnostics;
using MemEngine360.BaseFrontEnd.Scripting;
using MemEngine360.Engine.StructViewing;
using MemEngine360.Scripting;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.StructViewing;

public class StructViewerServiceImpl : IStructViewerService {
    public void Show(StructViewerManager manager) {
        if (ITopLevel.TryGetFromContext(manager.UserContext, out ITopLevel? sequencerTopLevel)) {
            IDesktopWindow window = (IDesktopWindow) sequencerTopLevel;

            Debug.Assert(window.OpenState.IsOpenOrTryingToClose());
            window.Activate();
            return;
        }

        if (IWindowManager.TryGetInstance(out IWindowManager? winManager)) {
            IDesktopWindow window = winManager.CreateWindow(new WindowBuilder() {
                Title = "Scripting",
                FocusPath = "ScriptingWindow",
                Content = new StructViewerTreeView() {
                    StructViewerManager = manager,
                },
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.Sequencer.TitleBarBackground"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 640, MinHeight = 400,
                Width = 960, Height = 640
            });

            window.Closed += (sender, args) => ((StructViewerTreeView) sender.Content!).StructViewerManager = null;

            manager.UserContext.Set(ITopLevel.TopLevelDataKey, window);
            _ = window.ShowAsync();
        }
    }
}