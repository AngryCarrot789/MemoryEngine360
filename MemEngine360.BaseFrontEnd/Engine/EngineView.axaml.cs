// 
// Copyright (c) 2026-2026 REghZy
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
using PFXToolKitUI.Utils.Events;
using PFXToolKitUI.Utils.Reactive;

namespace MemEngine360.BaseFrontEnd.Engine;

public partial class EngineView : UserControl {
    public MemoryEngine MemoryEngine => this.ViewState.Engine;

    public MemoryEngineViewState ViewState { get; }

    internal IDesktopWindow? myWindow;
    private readonly DataGridSelectionModelBinder<ScanResultViewModel> scanResultSelectionBinder;
    private readonly TreeViewSelectionModelBinder<BaseAddressTableEntry> addressTableSelectionBinder;

    private readonly ColourBrushHandler titleBarToMenuBackgroundBrushHandler;
    private readonly ScanOptionsPresenter myScanOptionsPresenter;
    private readonly EngineMenuController menuController;
    private ConnectionNotificationHelper? myConnectionNotificationHelper;

    public EngineView() : this(MemoryEngineViewState.GetInstance(new MemoryEngine())) {
    }

    public EngineView(MemoryEngineViewState viewState) {
        this.InitializeComponent();

        this.ViewState = viewState;
        this.menuController = new EngineMenuController(viewState);
        this.PART_TopLevelMenu.TopLevelMenuRegistry = this.menuController.Menu;

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
        }, RoutingStrategies.Bubble);

        NotificationManager notificationManager = new NotificationManager();
        ((IComponentManager) this.MemoryEngine).ComponentStorage.AddComponent(notificationManager);
        this.PART_NotificationListBox.NotificationManager = notificationManager;

        notificationManager.IsAlertActiveChanged += this.NotificationManagerOnIsAlertActiveChanged;
    }

    private void NotificationManagerOnIsAlertActiveChanged(object? sender, EventArgs e) {
        if (this.myWindow != null && this.myWindow.TryGetFeature(out IWindowFeatureUserAlert? alert)) {
            alert.IsAlertEnabled = this.PART_NotificationListBox.NotificationManager!.IsAlertActive;
        }
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

        this.menuController.OnTopLevelLoaded();

        if (IWindowManager.TryGetWindow(this, out IDesktopWindow? window)) {
            this.myWindow = window;
            this.titleBarToMenuBackgroundBrushHandler.SetTarget(this.PART_TopLevelMenu);
            this.titleBarToMenuBackgroundBrushHandler.Brush = this.myWindow.TitleBarBrush;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e) {
        base.OnUnloaded(e);
        this.myConnectionNotificationHelper?.OnDetach();
        
        MemoryEngineViewState vs = this.ViewState;
        vs.RequestWindowFocus -= this.OnRequestWindowFocus;
        vs.RequestFocusOnSavedAddress -= this.OnRequestFocusOnSavedAddress;
        this.menuController.OnTopLevelUnloaded();

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
}