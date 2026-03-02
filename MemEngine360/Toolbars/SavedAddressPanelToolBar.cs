using PFXToolKitUI.Toolbars;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Toolbars;

public sealed class SavedAddressPanelToolBar : BaseToolBarManager {
    public ObservableList<ToolBarButton> WestButtons { get; }
    
    public ObservableList<ToolBarButton> EastButtons { get; }

    public SavedAddressPanelToolBar() {
        this.WestButtons = new ObservableList<ToolBarButton>();
        this.EastButtons = new ObservableList<ToolBarButton>();

        ToolbarButtonFactory service = ToolbarButtonFactory.Instance;
        this.WestButtons.Add(new SimpleCommandToolBarButton(service.CreateButton(), "commands.memengine.OpenSaveAddressFileCommand"));
        this.WestButtons.Add(new SimpleCommandToolBarButton(service.CreateButton(), "commands.memengine.SaveSavedAddressesToFileCommand"));
        this.WestButtons.Add(new SimpleCommandToolBarButton(service.CreateButton(), "commands.memengine.AddSavedAddressCommand"));
        this.WestButtons.Add(new SimpleCommandToolBarButton(service.CreateButton(), "commands.memengine.ClearSavedAddressesCommand") {
            Icon = SimpleIcons.DeleteAllRowsIcon
        });
    }
}