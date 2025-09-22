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

using Avalonia;
using Avalonia.Controls;
using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Themes;

namespace MemEngine360.BaseFrontEnd.EventViewing;

public partial class ConsoleEventViewerViewEx : UserControl {
    public static readonly StyledProperty<MemoryEngine?> MemoryEngineProperty = AvaloniaProperty.Register<ConsoleEventViewerViewEx, MemoryEngine?>(nameof(MemoryEngine));

    public MemoryEngine? MemoryEngine {
        get => this.GetValue(MemoryEngineProperty);
        set => this.SetValue(MemoryEngineProperty, value);
    }

    public ConsoleEventViewerViewEx() {
        this.InitializeComponent();
    }

    static ConsoleEventViewerViewEx() {
        MemoryEngineProperty.Changed.AddClassHandler<ConsoleEventViewerViewEx, MemoryEngine?>((s, e) => s.OnMemoryEngineChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
    }

    private void OnMemoryEngineChanged(MemoryEngine? oldValue, MemoryEngine? newValue) {
        if (oldValue != null)
            oldValue.ConnectionChanged -= this.OnConsoleConnectionChanged;
        if (newValue != null)
            newValue.ConnectionChanged += this.OnConsoleConnectionChanged;
    }

    private void OnConsoleConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldconnection, IConsoleConnection? newconnection, ConnectionChangeCause cause) {
        this.PART_EventViewer.ConsoleConnection = newconnection;
    }
}

public class ConsoleEventViewerServiceImpl : IConsoleEventViewerService {
    private static readonly DataKey<SingletonWindow> EventViewerWindowKey = DataKeys.Create<SingletonWindow>("EventViewerWindow");

    public Task ShowOrFocus(MemoryEngine engine) {
        if (!EventViewerWindowKey.TryGetContext(engine.UserContext, out SingletonWindow? singletonWindow)) {
            engine.UserContext.Set(EventViewerWindowKey, singletonWindow = new SingletonWindow(manager => {
                IWindow win = manager.CreateWindow(new WindowBuilder() {
                    Title = "Event Viewer",
                    Content = new ConsoleEventViewerViewEx() {
                        MemoryEngine = engine
                    },
                    BorderBrush = BrushManager.Instance.GetDynamicThemeBrush("PanelBorderBrush"),
                    TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.Tone7.Background.Static"),
                });

                win.WindowOpened += static (sender, args) => {
                    ConsoleEventViewerViewEx view = (ConsoleEventViewerViewEx) sender.Content!;
                    view.PART_EventViewer.BusyLock = view.MemoryEngine!.BusyLocker;
                    view.PART_EventViewer.ConsoleConnection = view.MemoryEngine!.Connection;
                };
                win.WindowClosed += static (sender, args) => {
                    ConsoleEventViewerViewEx view = (ConsoleEventViewerViewEx) sender.Content!;
                    view.PART_EventViewer.BusyLock = null;
                    view.PART_EventViewer.ConsoleConnection = null;
                    view.MemoryEngine = null;
                };

                return win;
            }));
        }

        singletonWindow.ShowOrActivate();
        return Task.CompletedTask;
    }
}