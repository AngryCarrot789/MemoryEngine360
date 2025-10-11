using System.Runtime.Versioning;
using Avalonia.Controls;
using MemEngine360.BaseFrontEnd.Services.Connectivity;
using MemEngine360.Connections;
using PFXToolKitUI.Avalonia.Bindings;

namespace MemEngine360.PS3;

[SupportedOSPlatform("windows")]
public partial class OpenCCAPIConnectionView : UserControl, IConsoleConnectivityControl {
    private readonly IBinder<ConnectToCCAPIInfo> ipBinder =
        new AvaloniaPropertyToEventPropertyBinder<ConnectToCCAPIInfo>(
            TextBox.TextProperty,
            nameof(ConnectToCCAPIInfo.IpAddressChanged),
            b => ((TextBox) b.Control).Text = b.Model.IpAddress,
            b => b.Model.IpAddress = ((TextBox) b.Control).Text ?? "");

    public ConnectToCCAPIInfo? ConnectionInfo { get; private set; }

    public OpenCCAPIConnectionView() {
        this.InitializeComponent();
    }

    public void OnConnected(OpenConnectionView dialog, UserConnectionInfo info) {
        this.ipBinder.Attach(this.PART_IpAddressTextBox, this.ConnectionInfo = (ConnectToCCAPIInfo) info);
    }

    public void OnDisconnected() {
        this.ipBinder.Detach();
        this.ConnectionInfo = null;
    }
}