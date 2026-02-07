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
using MemEngine360.Configs;
using MemEngine360.Scripting;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Logging;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Utils;
using SkiaSharp;

namespace MemEngine360.BaseFrontEnd.Scripting;

public class DesktopScriptingViewServiceImpl : IScriptingViewService {
    private static bool hasShownBefore;

    public async Task ShowOrFocusWindow(ScriptingManager scriptingManager) {
        if (ITopLevel.TryGetFromContext(scriptingManager.UserContext, out ITopLevel? sequencerTopLevel)) {
            IDesktopWindow window = (IDesktopWindow) sequencerTopLevel;

            Debug.Assert(window.OpenState.IsOpenOrTryingToClose());
            window.Activate();
            return;
        }

        if (IWindowManager.TryGetInstance(out IWindowManager? manager)) {
            IDesktopWindow window = manager.CreateWindow(new WindowBuilder() {
                Title = "Scripting",
                FocusPath = "ScriptingWindow",
                Content = new ScriptingView() {
                    ScriptingManager = ScriptingManagerViewState.GetInstance(scriptingManager, TopLevelIdentifier.Single(IScriptingViewService.TopLevelId)),
                },
                TitleBarBrush = BrushManager.Instance.GetDynamicThemeBrush("ABrush.MemEngine.Sequencer.TitleBarBackground"),
                BorderBrush = BrushManager.Instance.CreateConstant(SKColors.DodgerBlue),
                MinWidth = 640, MinHeight = 400,
                Width = 960, Height = 640
            });

            window.Opened += (s, args) => ((ScriptingView) ((IDesktopWindow) s!).Content!).OnWindowOpened((IDesktopWindow) s!);
            
            window.TryCloseAsync += static async (s, args) => {
                await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
                    ScriptingView view = (ScriptingView) ((IDesktopWindow) s!).Content!;
                    ScriptingView.CloseRequest result = await view.OnClosingAsync((IDesktopWindow) s!, !view.ScriptingManager!.ScriptingManager.MemoryEngine.IsShuttingDown);
                    if (result == ScriptingView.CloseRequest.Cancel) {
                        args.SetCancelled();
                    }
                });
            };
            
            window.Closing += (s, args) => {
                ScriptingManagerViewState tsm = ((ScriptingView) ((IDesktopWindow) s!).Content!).ScriptingManager!;
                tsm.ScriptingManager.UserContext.Remove(ITopLevel.TopLevelDataKey);
            };

            window.Closed += (s, args) => ((ScriptingView) ((IDesktopWindow) s!).Content!).OnWindowClosed();

            scriptingManager.UserContext.Set(ITopLevel.TopLevelDataKey, window);
            await window.ShowAsync();

            if (!hasShownBefore) {
                hasShownBefore = true;

                _ = ActivityManager.Instance.RunTask(async () => {
                    ActivityTask activity = ActivityTask.Current;
                    activity.Progress.SetCaptionAndText("Reload last scripts");
                    foreach (string path in BasicApplicationConfiguration.Instance.LoadedScriptPaths) {
                        activity.CancellationToken.ThrowIfCancellationRequested();
                        try {
                            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".lua") || !File.Exists(path)) {
                                continue;
                            }
                        }
                        catch {
                            continue; // Not sure if File.Exists() throws for invalid paths
                        }

                        string text;
                        try {
                            text = await File.ReadAllTextAsync(path, activity.CancellationToken);
                        }
                        catch (Exception e) {
                            AppLogger.Instance.WriteLine("Failed to reload script file from config: " + e.GetToString());
                            continue; // ignored
                        }

                        ApplicationPFX.Instance.Dispatcher.Post(() => {
                            Script script = new Script() {
                                Document = { Text = text },
                                HasUnsavedChanges = false
                            };

                            script.SetFilePath(path);
                            scriptingManager.Scripts.Add(script);
                        }, DispatchPriority.Background);
                    }
                }, true);
            }
        }
    }
    
    public Task CloseWindow(ScriptingManager manager) {
        if (ITopLevel.TryGetFromContext(manager.UserContext, out ITopLevel? sequencerTopLevel)) {
            IDesktopWindow window = (IDesktopWindow) sequencerTopLevel;
            if (window.OpenState == OpenState.Open) {
                return window.RequestCloseAsync();
            }
        }

        return Task.CompletedTask;
    }
}