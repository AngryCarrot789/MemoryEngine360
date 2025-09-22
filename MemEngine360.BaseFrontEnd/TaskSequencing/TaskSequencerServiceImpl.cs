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
using MemEngine360.Engine;
using MemEngine360.Sequencing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class TaskSequencerServiceImpl : ITaskSequencerService {
    private static readonly DataKey<IWindow> OpenedWindowKey = DataKeys.Create<IWindow>(nameof(ITaskSequencerService) + "_OpenedSequencerWindow");

    public Task OpenOrFocusWindow(TaskSequenceManager sequencer) {
        if (OpenedWindowKey.TryGetContext(sequencer.UserContext, out IWindow? sequencerWindow)) {
            Debug.Assert(sequencerWindow.OpenState == OpenState.Open || sequencerWindow.OpenState == OpenState.TryingToClose);
            
            sequencerWindow.Activate();
            return Task.CompletedTask;
        }

        if (IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            IWindow window = manager.CreateWindow(new WindowBuilder() {
                Title = "Task Sequencer",
                FocusPath = "SequencerWindow",
                Content = new TaskSequencerView(sequencer),
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.Sequencer.TitleBarBackground"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 640, MinHeight = 400,
                Width = 960, Height = 640
            });

            window.WindowOpened += (sender, args) => ((TaskSequencerView) sender.Content!).OnWindowOpened(sender);
            window.WindowClosing += (sender, args) => {
                // prevent memory leak
                TaskSequenceManager tsm = ((TaskSequencerView) sender.Content!).TaskSequenceManager;
                tsm.UserContext.Set(OpenedWindowKey, null);
            };
            
            window.WindowClosed += (sender, args) => {
                ((TaskSequencerView) sender.Content!).OnWindowClosed();
            };
            
            sequencer.UserContext.Set(OpenedWindowKey, window);
            return window.ShowAsync();
        }

        return Task.CompletedTask;
    }
}