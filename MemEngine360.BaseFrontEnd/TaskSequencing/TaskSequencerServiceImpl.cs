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
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.View;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class TaskSequencerServiceImpl : ITaskSequencerService {
    public Task OpenOrFocusWindow(TaskSequenceManager sequencer) {
        if (ITopLevel.TryGetFromContext(sequencer.UserContext, out ITopLevel? sequencerTopLevel)) {
            // Currently showing the sequencer is only supported on desktop
            IDesktopWindow window = (IDesktopWindow) sequencerTopLevel;
            
            Debug.Assert(window.OpenState.IsOpenOrTryingToClose());
            window.Activate();
            return Task.CompletedTask;
        }
        
        if (IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
                Title = "Task Sequencer",
                FocusPath = "SequencerWindow",
                Content = new TaskSequencerView(new TaskSequenceManagerViewState(sequencer, TopLevelIdentifier.Single(TaskSequencerView.TopLevelId))),
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.Sequencer.TitleBarBackground"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 640, MinHeight = 400,
                Width = 960, Height = 640
            });

            window.Opened += (s, args) => ((TaskSequencerView) ((IDesktopWindow) s!).Content!).OnWindowOpened((IDesktopWindow) s!);
            window.Closing += (s, args) => {
                // prevent memory leak
                TaskSequenceManager tsm = ((TaskSequencerView) ((IDesktopWindow) s!).Content!).TaskSequenceManager;
                tsm.UserContext.Remove(ITopLevel.TopLevelDataKey);
            };
            
            window.Closed += (s, args) => {
                ((TaskSequencerView) ((IDesktopWindow) s!).Content!).OnWindowClosed();
            };
            
            sequencer.UserContext.Set(ITopLevel.TopLevelDataKey, window);
            return window.ShowAsync();
        }

        return Task.CompletedTask;
    }
}