using Avalonia.Controls;
using AvaloniaHex.Base.Document;
using AvaloniaHex.Rendering;

namespace MemEngine360.Xbox360XBDM;

public partial class BinaryHexEditorView : UserControl {
    public BinaryHexEditorView() {
        this.InitializeComponent();
        
        HexView view = this.PART_HexEditor.HexView;
        view.BytesPerLine = 32;
        view.Columns.Add(new OffsetColumn());
        view.Columns.Add(new HexColumn());
        view.Columns.Add(new AsciiColumn());
    }

    public void SetBytes(byte[] array) {
        this.PART_HexEditor.Document = new MemoryBinaryDocument(array, true);
    }
}