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

using System.Diagnostics;
using MemEngine360.Scripting;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.Scripting;

public class ScriptingServiceImpl : IScriptingService {
    public Task OpenOrFocusWindow(ScriptingManager scriptingManager) {
        if (ITopLevel.TryGetFromContext(scriptingManager.UserContext, out ITopLevel? sequencerTopLevel)) {
            // Currently showing the sequencer is only supported on desktop
            IDesktopWindow window = (IDesktopWindow) sequencerTopLevel;

            Debug.Assert(window.OpenState.IsOpenOrTryingToClose());
            window.Activate();
            return Task.CompletedTask;
        }

        if (IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
                Title = "Scripting",
                FocusPath = "ScriptingWindow",
                Content = new ScriptingView() {
                    ScriptingManager = scriptingManager,
                },
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.Sequencer.TitleBarBackground"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 640, MinHeight = 400,
                Width = 960, Height = 640
            });

            window.Opened += (sender, args) => ((ScriptingView) sender.Content!).OnWindowOpened(sender);
            window.Closing += (sender, args) => {
                ScriptingManager tsm = ((ScriptingView) sender.Content!).ScriptingManager!;
                tsm.UserContext.Remove(ITopLevel.TopLevelDataKey);
            };

            window.Closed += (sender, args) => ((ScriptingView) sender.Content!).OnWindowClosed();

            scriptingManager.UserContext.Set(ITopLevel.TopLevelDataKey, window);
            return window.ShowAsync();
        }

        return Task.CompletedTask;
    }
}