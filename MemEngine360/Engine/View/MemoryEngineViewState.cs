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

using MemEngine360.Engine.SavedAddressing;
using MemEngine360.ModTools;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Selections;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Shortcuts;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Engine.View;

public sealed class MemoryEngineViewState {
    public static readonly DataKey<MemoryEngineViewState> DataKey = DataKeys.Create<MemoryEngineViewState>("MemoryEngineViewState");
    
    /// <summary>
    /// Gets the engine model instance
    /// </summary>
    public MemoryEngine Engine { get; }

    /// <summary>
    /// Gets the top level identifier that identifies which top level this view state is associated with
    /// </summary>
    public TopLevelIdentifier TopLevelIdentifier { get; }

    /// <summary>
    /// Gets the menu registry for the engine window
    /// </summary>
    public TopLevelMenuRegistry TopLevelMenuRegistry { get; } = new TopLevelMenuRegistry();

    /// <summary>
    /// Returns the selection model for the scan results
    /// </summary>
    public ListSelectionModel<ScanResultViewModel> SelectedScanResults { get; }

    /// <summary>
    /// Returns the selection model for the saved address table
    /// </summary>
    public TreeSelectionModel<BaseAddressTableEntry> AddressTableSelectionManager { get; }

    /// <summary>
    /// Gets or sets if the activity list is visible or not
    /// </summary>
    public bool IsActivityListVisible {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.IsActivityListVisibleChanged);
    }
    
    /// <summary>
    /// Gets the tools menu for memory engine
    /// </summary>
    public MenuEntryGroup ToolsMenu { get; }
    
    /// <summary>
    /// Gets the Remote Controls menu for memory engine
    /// </summary>
    public MenuEntryGroup RemoteControlsMenu { get; }

    /// <summary>
    /// Fired when someone requests for the engine window to be focused
    /// </summary>
    public event EventHandler? RequestWindowFocus;

    /// <summary>
    /// Requests a saved address to be focused in the UI. This will also select the item
    /// </summary>
    public event EventHandler<BaseAddressTableEntry>? RequestFocusOnSavedAddress;

    public event EventHandler? IsActivityListVisibleChanged;
    
    private MemoryEngineViewState(MemoryEngine engine, TopLevelIdentifier topLevelIdentifier) {
        this.Engine = engine;
        this.TopLevelIdentifier = topLevelIdentifier;
        this.SelectedScanResults = new ListSelectionModel<ScanResultViewModel>(this.Engine.ScanningProcessor.ScanResults);

        AddressTableManager atm = engine.AddressTableManager;
        this.AddressTableSelectionManager = new TreeSelectionModel<BaseAddressTableEntry>(
            atm.RootEntry,
            static arg => arg.AddressTableManager != null,
            static arg => arg.Parent,
            static arg => arg is AddressTableGroupEntry g ? g.Items : null);
        
        this.ToolsMenu = new MenuEntryGroup("_Tools") {
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
                ModToolManagerViewState.GetInstance(engine.ModToolManager).ModToolMenu
            }
        };

        // update all tools when connection changes, since most if not all tools rely on a connection
        this.ToolsMenu.AddCanExecuteChangeUpdaterForEventsEx(DataKey, vs => vs.Engine, nameof(MemoryEngine.ConnectionChanged));
        
        this.RemoteControlsMenu = new MenuEntryGroup("_Remote Controls") {
            ProvideDisabledHint = static (ctx, registry) => {
                if (!MemoryEngineViewState.DataKey.TryGetContext(ctx, out MemoryEngineViewState? engineVs))
                    return null;

                if (engineVs.Engine.Connection == null) {
                    IReadOnlyCollection<ShortcutEntry> scList = ShortcutManager.Instance.GetShortcutsByCommandId("commands.memengine.OpenConsoleConnectionDialogCommand");
                    string shortcuts = scList.Select(x => x.Shortcut.ToString()!).JoinString(", ", " or ");
                    if (!string.IsNullOrEmpty(shortcuts))
                        shortcuts = ". Use the shortcut(s) to connect: " + shortcuts;
                    return new SimpleDisabledHintInfo("Not connected", "Connect to a console to use remote commands" + shortcuts);
                }

                return null;
            }
        };

        engine.ConnectionChanged += this.OnConnectionChanged;
    }

    private void OnConnectionChanged(object? sender, ConnectionChangedEventArgs e) {
        this.RemoteControlsMenu.Items.Clear();
        if (e.NewConnection != null) {
            this.RemoteControlsMenu.Items.AddRange(e.NewConnection.ConnectionType.GetRemoteContextOptions());
        }
    }

    public void RaiseRequestWindowFocus() => this.RequestWindowFocus?.Invoke(this, EventArgs.Empty);

    public void RaiseRequestFocusOnSavedAddress(BaseAddressTableEntry entry) {
        ArgumentNullException.ThrowIfNull(entry);
        this.AddressTableSelectionManager.Select(entry);
        this.RequestFocusOnSavedAddress?.Invoke(this, entry);
    }

    /// <summary>
    /// Gets the singleton view state associated with the memory engine model. Only one view per model is supported
    /// </summary>
    public static MemoryEngineViewState GetInstance(MemoryEngine engine, TopLevelIdentifier topLevelIdentifier) {
        return TopLevelDataMap.GetInstance(engine).GetOrCreate(topLevelIdentifier, engine, static (s, i) => new MemoryEngineViewState((MemoryEngine) s!, i));
    }
}