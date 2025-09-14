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

using MemEngine360.Engine;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class TaskSequencerServiceImpl : ITaskSequencerService {
    private IWindow? currentWindow;

    public Task OpenOrFocusWindow(IEngineUI engine) {
        if (this.currentWindow != null) {
            this.currentWindow.Activate();
            return Task.CompletedTask;
        }

        if (IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            IWindow window = this.currentWindow = manager.CreateWindow(new WindowBuilder() {
                Title = "Task Sequencer",
                FocusPath = "SequencerWindow",
                Content = new TaskSequencerWindow(engine.MemoryEngine.TaskSequenceManager),
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.Sequencer.TitleBarBackground"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 640, MinHeight = 400,
                Width = 960, Height = 640
            });

            window.WindowOpened += (sender, args) => ((TaskSequencerWindow) sender.Content!).OnWindowOpened(sender);
            window.WindowClosed += (sender, args) => {
                ((TaskSequencerWindow) sender.Content!).OnWindowClosed();
                this.currentWindow = null;
            };
            
            return window.ShowAsync();
        }

        return Task.CompletedTask;
    }
}