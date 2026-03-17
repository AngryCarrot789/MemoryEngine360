// 
// Copyright (c) 2024-2026 REghZy
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

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MemEngine360.BaseFrontEnd.SavedAddressing;
using MemEngine360.Connections;
using MemEngine360.Engine;
using MemEngine360.Engine.SavedAddressing;
using MemEngine360.Engine.View;
using PFXToolKitUI.Activities;
using PFXToolKitUI.AdvancedMenuService;
using PFXToolKitUI.Avalonia.Interactivity.SelectingEx2;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Desktop;
using PFXToolKitUI.Avalonia.Interactivity.Windowing.Features;
using PFXToolKitUI.Avalonia.Utils;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Icons;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Interactivity.Windowing;
using PFXToolKitUI.Notifications;
using PFXToolKitUI.Themes;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.BaseFrontEnd.Engine;

public partial class EngineView : UserControl {
    public const string TopLevelId = "toplevels.MemoryEngine";

    public MemoryEngine MemoryEngine => this.ViewState.Engine;

    public MemoryEngineViewState ViewState { get; }

    public TopLevelMenuRegistry TopLevelMenuRegistry => this.ViewState.TopLevelMenuRegistry;

    private readonly MenuEntryGroup themesSubList;
    internal IDesktopWindow? myOwnerWindow_onLoaded;
    private ObservableItemProcessorIndexing<Theme>? themeListHandler;
    private readonly DataGridSelectionModelBinder<ScanResultViewModel> scanResultSelectionBinder;
    private readonly TreeViewSelectionModelBinder<BaseAddressTableEntry> addressTableSelectionBinder;

    private readonly ColourBrushHandler titleBarToMenuBackgroundBrushHandler;
    private readonly ScanOptionsPresenter myScanOptionsPresenter;
    private ConnectionNotificationHelper? myConnectionNotificationHelper;

    public EngineView() : this(MemoryEngineViewState.GetInstance(new MemoryEngine(), TopLevelIdentifier.Single(TopLevelId))) {
    }

    public EngineView(MemoryEngineViewState viewState) {
        this.InitializeComponent();

        this.themesSubList = new MenuEntryGroup("Themes");
        this.ViewState = viewState;
        this.SetupMainMenu();

        this.titleBarToMenuBackgroundBrushHandler = new ColourBrushHandler(BackgroundProperty);

        this.scanResultSelectionBinder = new DataGridSelectionModelBinder<ScanResultViewModel>(this.PART_ScanListResults, this.ViewState.SelectedScanResults);
        this.addressTableSelectionBinder = new TreeViewSelectionModelBinder<BaseAddressTableEntry>(
            this.PART_SavedAddressTree,
            this.ViewState.AddressTableSelectionManager,
            tvi => ((AddressTableTreeViewItem) tvi).EntryObject!,
            model => this.PART_SavedAddressTree.ItemMap.GetControl(model));

        this.PART_LatestActivity.Text = "Welcome to MemoryEngine360.";
        this.PART_ScanListResults.ItemsSource = this.MemoryEngine.ScanningProcessor.ScanResults;
        this.PART_SavedAddressTree.AddressTableManager = this.MemoryEngine.AddressTableManager;
        // this.PART_FileBrowser.FileTreeManager = this.MemoryEngine.FileTreeManager;

        this.MemoryEngine.ScanningProcessor.ScanResults.CollectionChanged += (sender, args) => {
            this.UpdateScanResultCounterText();
        };

        this.myScanOptionsPresenter = new ScanOptionsPresenter(this);
        
        // Close activity list when user presses ESC
        this.PART_ActivityListPanel.AddHandler(KeyDownEvent, (sender, e) => {
            if (e.Key == Key.Escape) {
                e.Handled = true;
                this.ViewState.IsActivityListVisible = false;
            }
        }, RoutingStrategies.Tunnel);

        NotificationManager notificationManager = new NotificationManager();
        ((IComponentManager) this.MemoryEngine).ComponentStorage.AddComponent(notificationManager);
        this.PART_NotificationListBox.NotificationManager = notificationManager;

        notificationManager.IsAlertActiveChanged += this.NotificationManagerOnIsAlertActiveChanged;
    }

    private void NotificationManagerOnIsAlertActiveChanged(object? sender, EventArgs e) {
        if (this.myOwnerWindow_onLoaded != null && this.myOwnerWindow_onLoaded.TryGetFeature(out IWindowFeatureUserAlert? alert)) {
            alert.IsAlertEnabled = this.PART_NotificationListBox.NotificationManager!.IsAlertActive;
        }
    }

    private void SetupMainMenu() {
        TopLevelMenuRegistry menu = this.TopLevelMenuRegistry;

        // ### File ###
        MenuEntryGroup fileEntry = new MenuEntryGroup("_File");
        fileEntry.Items.Add(new CommandMenuEntry("commands.memengine.OpenConsoleConnectionDialogCommand", "_Connect to console...", icon: SimpleIcons.ConnectToConsoleIcon));
        fileEntry.Items.Add(new CommandMenuEntry("commands.memengine.DumpMemoryCommand", "Memory _Dump...", icon: SimpleIcons.DownloadMemoryIcon));
        fileEntry.Items.Add(new SeparatorEntry());
        fileEntry.Items.Add(new CommandMenuEntry("commands.memengine.remote.SendCmdCommand", "Send Custom Command...", "This lets you send a completely custom Xbox Debug Monitor command. Please be careful with it."));
        fileEntry.Items.Add(
            new CommandMenuEntry("commands.memengine.remote.SendNotificationCommand", "Send Notification", icon: SimpleIcons.Xbox360Icon).
                AddCanExecuteChangeUpdaterForEventsEx(MemoryEngineViewState.DataKey, x => x.Engine, nameof(MemEngine360.Engine.MemoryEngine.ConnectionChanged)));
        fileEntry.Items.Add(new SeparatorEntry());
        fileEntry.Items.Add(new CommandMenuEntry("commands.mainWindow.OpenEditorSettings", "_Preferences"));
        menu.Items.Add(fileEntry);

        MemoryEngineViewState viewState = this.ViewState;

        // ### Remote Commands ###
        menu.Items.Add(viewState.RemoteControlsMenu);

        // ### Tools ###
        menu.Items.Add(viewState.ToolsMenu);

        // ### Themes ###
        menu.Items.Add(this.themesSubList);

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
        menu.Items.Add(helpEntry);

        this.PART_TopLevelMenu.TopLevelMenuRegistry = menu;
    }

    internal void UpdateScanResultCounterText() {
        ScanningProcessor processor = this.MemoryEngine.ScanningProcessor;

        int pending = processor.ActualScanResultCount;
        int count = processor.ScanResults.Count;
        pending -= count;
        this.PART_Run_CountResults.Text = $"{count} results{(pending > 0 ? $" ({pending} {(processor.IsScanning ? "pending" : "hidden")})" : "")}";
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        this.myConnectionNotificationHelper ??= new ConnectionNotificationHelper();
        this.myConnectionNotificationHelper.OnAttach(this);
        
        this.ViewState.RequestWindowFocus += this.OnRequestWindowFocus;
        this.ViewState.RequestFocusOnSavedAddress += this.OnRequestFocusOnSavedAddress;
        this.ViewState.IsActivityListVisibleChanged += this.OnIsActivityListVisibleChanged;

        this.myScanOptionsPresenter.OnViewLoaded();
        this.UpdateStatusBarConnectionText(this.MemoryEngine.Connection);

        this.themeListHandler = ObservableItemProcessor.MakeIndexable(ThemeManager.Instance.Themes, (args) => {
            this.themesSubList.Items.Insert(args.Index, new SetThemeMenuEntry(args.Item));
        }, (args) => {
            this.themesSubList.Items.RemoveAt(args.Index);
        }, (args) => {
            this.themesSubList.Items.Move(args.OldIndex, args.NewIndex);
        }).AddExistingItems();

        if (IWindowManager.TryGetWindow(this, out IDesktopWindow? window)) {
            this.myOwnerWindow_onLoaded = window;
            this.titleBarToMenuBackgroundBrushHandler.SetTarget(this.PART_TopLevelMenu);
            this.titleBarToMenuBackgroundBrushHandler.Brush = this.myOwnerWindow_onLoaded.TitleBarBrush;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        base.OnUnloaded(e);
        this.myConnectionNotificationHelper?.OnDetach();
        
        MemoryEngineViewState vs = this.ViewState;
        vs.RequestWindowFocus -= this.OnRequestWindowFocus;
        vs.RequestFocusOnSavedAddress -= this.OnRequestFocusOnSavedAddress;
        this.themeListHandler?.RemoveExistingItems();
        this.themeListHandler?.Dispose();
        this.themeListHandler = null;

        this.myScanOptionsPresenter.OnViewUnloaded();

        this.PART_SavedAddressTree.AddressTableManager = null;
        this.PART_TopLevelMenu.TopLevelMenuRegistry = null;
    }

    internal void UpdateStatusBarConnectionText(IConsoleConnection? console) {
        string text = console != null ? console.ConnectionType.GetStatusBarText(console) : "Disconnected";
        this.PART_ConnectedHostName.SetValue(TextBlock.TextProperty, text);
    }

    private void OnIsActivityListVisibleChanged(object? o, EventArgs e) {
        MemoryEngineViewState sender = (MemoryEngineViewState) o!;
        if (this.PART_ActivityListPanel.IsVisible != sender.IsActivityListVisible) {
            this.PART_ActivityListPanel.IsVisible = sender.IsActivityListVisible;
            this.PART_ActivityList.ActivityManager = sender.IsActivityListVisible ? ActivityManager.Instance : null;
            this.PART_ActivityListPanel.Focus();
        }
    }

    private void OnRequestFocusOnSavedAddress(object? sender, BaseAddressTableEntry address) {
        if (this.PART_SavedAddressTree.ItemMap.TryGetControl(address, out AddressTableTreeViewItem? item)) {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void OnRequestWindowFocus(object? sender, EventArgs e) {
        if (IWindowManager.TryGetWindow(this, out IDesktopWindow? window)) {
            window.Activate();
        }
    }

    private void PART_ScanOption_Alignment_OnDoubleTapped(object? sender, TappedEventArgs e) {
        this.myScanOptionsPresenter.EditAlignmentCommand.Execute(null);
    }

    private void CloseActivityListButtonClicked(object? sender, RoutedEventArgs e) {
        this.ViewState.IsActivityListVisible = false;
    }

    private void OnHeaderPointerClicked(object? sender, PointerPressedEventArgs e) {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed) {
            this.ViewState.IsActivityListVisible = false;
        }
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