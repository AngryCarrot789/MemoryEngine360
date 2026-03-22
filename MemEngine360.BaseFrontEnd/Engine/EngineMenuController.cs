using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.View;
using MemEngine360.ModTools;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Shortcuts;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.Engine;

/// <summary>
/// Manages the main menu for the engine view
/// </summary>
public class EngineMenuController {
    private readonly MenuEntryGroup remoteControlsMenu;
    private readonly MenuEntryGroup toolsMenu;
    private readonly MenuEntryGroup themesMenu;
    private ObservableItemProcessorIndexing<Theme>? themeListHandler;
    
    /// <summary>
    /// Gets the top-level menu for the engine view
    /// </summary>
    public TopLevelMenuRegistry Menu { get; }

    public MemoryEngineViewState ViewState { get; }

    public EngineMenuController(MemoryEngineViewState viewState) {
        this.ViewState = viewState;
        this.Menu = new TopLevelMenuRegistry();

        // ### File ###
        MenuEntryGroup fileEntry = new MenuEntryGroup("_File");
        fileEntry.Items.Add(new CommandMenuEntry("commands.memengine.OpenConsoleConnectionDialogCommand", "_Connect to console...", icon: SimpleIcons.ConnectToConsoleIcon));
        fileEntry.Items.Add(new CommandMenuEntry("commands.memengine.DumpMemoryCommand", "Memory _Dump...", icon: SimpleIcons.DownloadMemoryIcon));
        fileEntry.Items.Add(new SeparatorEntry());
        fileEntry.Items.Add(new CommandMenuEntry("commands.memengine.remote.SendCmdCommand", "Send Custom Command...", "This lets you send a completely custom Xbox Debug Monitor command. Please be careful with it."));
        fileEntry.Items.Add(
            new CommandMenuEntry("commands.memengine.remote.SendNotificationCommand", "Send Notification", icon: SimpleIcons.Xbox360Icon).
                AddCanExecuteChangeUpdaterForEventsEx(CommonKeys.MemoryEngineViewStateDataKey, x => x.Engine, nameof(MemEngine360.Engine.MemoryEngine.ConnectionChanged)));
        fileEntry.Items.Add(new SeparatorEntry());
        fileEntry.Items.Add(new CommandMenuEntry("commands.mainWindow.OpenEditorSettings", "_Preferences"));
        this.Menu.Items.Add(fileEntry);

        // ### Remote Commands ###
        this.remoteControlsMenu = new MenuEntryGroup("_Remote Controls") {
            ProvideDisabledHint = static (ctx, registry) => {
                if (!CommonKeys.MemoryEngineViewStateDataKey.TryGetContext(ctx, out MemoryEngineViewState? engineVs))
                    return null;

                if (engineVs.Engine.Connection == null) {
                    string? text = KeymapUtils.GetStringForCommandId("commands.memengine.OpenConsoleConnectionDialogCommand");
                    if (text != null)
                        text = ". Use the shortcut(s) to connect: " + text;

                    return new SimpleDisabledHintInfo(null, "Connect to a console to use remote commands" + text);
                }

                return null;
            },
            Description = "Custom console commands"
        };
        
        this.Menu.Items.Add(this.remoteControlsMenu);

        // ### Tools ###
        this.toolsMenu = new MenuEntryGroup("_Tools") {
            UniqueID = "memoryengine.tools",
            Items = {
                new CommandMenuEntry("commands.memengine.ShowMemoryViewCommand", "_Memory View", "Opens the memory viewer/hex editor"),
                new CommandMenuEntry("commands.memengine.ShowTaskSequencerCommand", "_Task Sequencer", "Opens the task sequencer"),
                new CommandMenuEntry("commands.memengine.ShowDebuggerCommand", "_Debugger"),
                new CommandMenuEntry("commands.memengine.ShowPointerScannerCommand", "_Pointer Scanner"),
                new CommandMenuEntry("commands.memengine.ShowConsoleEventViewerCommand", "_Event Viewer", "Shows the event viewer window for viewing console system events"),
                new CommandMenuEntry("commands.scripting.ShowScriptingWindowCommand", "_Scripting"),
                // new CommandMenuEntry("commands.structviewer.ShowStructViewerWindowCommand", "Struct Viewer"),
                new SeparatorEntry(),
                new CommandMenuEntry("commands.memengine.ShowModulesCommand", "Module E_xplorer", "Opens a window which presents the modules"),
                new CommandMenuEntry("commands.memengine.remote.ShowMemoryRegionsCommand", "Memory Region Explorer", "Opens a window which presents all memory regions"),
                new CommandMenuEntry("commands.memengine.ShowFileBrowserCommand", "File Explorer"),
                new SeparatorEntry(),
                ModToolManagerViewState.GetInstance(viewState.Engine.ModToolManager).ModToolMenu
            }
        };

        // update all tools when connection changes, since most if not all tools rely on a connection
        this.toolsMenu.AddCanExecuteChangeUpdaterForEventsEx(CommonKeys.MemoryEngineViewStateDataKey, vs => vs.Engine, nameof(MemoryEngine.ConnectionChanged));
        
        this.Menu.Items.Add(this.toolsMenu);

        // ### Themes ###
        this.themesMenu = new MenuEntryGroup("Themes");
        this.Menu.Items.Add(this.themesMenu);

        // ### Help ###
        MenuEntryGroup helpEntry = new MenuEntryGroup("_Help");
        helpEntry.Items.Add(new CommandMenuEntry("commands.application.ShowLogsCommand", "Show _Logs"));
        helpEntry.Items.Add(new SeparatorEntry());
        helpEntry.Items.Add(new CustomLambdaMenuEntry("Open Wiki", (c) => {
            if (!ITopLevel.TopLevelDataKey.TryGetContext(c, out ITopLevel? topLevel))
                return Task.CompletedTask;
            if (!IWebLauncher.TryGet(topLevel, out IWebLauncher? webLauncher))
                return Task.CompletedTask;

            const string url = "https://github.com/AngryCarrot789/MemoryEngine360/wiki#quick-start";
            return webLauncher.LaunchUriAsync(new Uri(url));
        }, (c) => {
            if (!ITopLevel.TopLevelDataKey.TryGetContext(c, out ITopLevel? window))
                return false;
            if (!window.TryGetWebLauncher(out _))
                return false;
            return true;
        }));

        helpEntry.Items.Add(new CommandMenuEntry("commands.application.AboutApplicationCommand", "About MemoryEngine360"));
        this.Menu.Items.Add(helpEntry);
    }

    public void OnTopLevelLoaded() {
        this.themeListHandler = ObservableItemProcessor.MakeIndexable(ThemeManager.Instance.Themes, (args) => {
            this.themesMenu.Items.Insert(args.Index, new SetThemeMenuEntry(args.Item));
        }, args => {
            this.themesMenu.Items.RemoveAt(args.Index);
        }, args => {
            this.themesMenu.Items.Move(args.OldIndex, args.NewIndex);
        }).AddExistingItems();
        
        this.ViewState.Engine.ConnectionChanged += this.OnConnectionChanged;
        this.remoteControlsMenu.Items.Clear();
        if (this.ViewState.Engine.Connection is IConsoleConnection connection) {
            this.AddRemoteOptions(connection);
        }
    }

    public void OnTopLevelUnloaded() {
        this.themeListHandler?.RemoveExistingItems();
        this.themeListHandler?.Dispose();
        this.themeListHandler = null;
        
        this.ViewState.Engine.ConnectionChanged -= this.OnConnectionChanged;
        this.remoteControlsMenu.Items.Clear();
    }
    
    private void OnConnectionChanged(object? sender, ConnectionChangedEventArgs e) {
        this.remoteControlsMenu.Items.Clear();
        if (e.NewConnection != null) {
            this.AddRemoteOptions(e.NewConnection);
        }
    }

    private void AddRemoteOptions(IConsoleConnection connection) {
        this.remoteControlsMenu.Items.AddRange(connection.ConnectionType.GetRemoteContextOptions());
    }

    private class SetThemeMenuEntry : CustomMenuEntry {
        private readonly Theme theme;

        public SetThemeMenuEntry(Theme theme, Icon? icon = null) : base(theme.Name, $"Sets the application's theme to '{theme.Name}'", icon) {
            this.theme = theme;
        }

        public override Task OnExecute(IContextData context) {
            this.theme.ThemeManager.SetTheme(this.theme);
            return Task.CompletedTask;
        }
    }
}