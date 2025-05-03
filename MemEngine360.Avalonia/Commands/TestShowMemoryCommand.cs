// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of MemEngine360.
// 
// MemEngine360 is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either
// version 3.0 of the License, or (at your option) any later version.
// 
// MemEngine360 is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MemEngine360. If not, see <https://www.gnu.org/licenses/>.
// 

using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using AvaloniaHex;
using AvaloniaHex.Core.Document;
using AvaloniaHex.Rendering;
using PFXToolKitUI.Avalonia.Services;
using PFXToolKitUI.Avalonia.Services.Windowing;
using PFXToolKitUI.CommandSystem;
using PFXToolKitUI.Services.FilePicking;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Avalonia.Commands;

public class TestShowMemoryCommand : Command {
    protected override async Task ExecuteCommandAsync(CommandEventArgs e) {
        string? filePath = await IFilePickDialogService.Instance.OpenFile("Open a file to show as hex");
        if (filePath != null && WindowingSystem.TryGetInstance(out WindowingSystem? system)) {
            byte[] buffer = await File.ReadAllBytesAsync(filePath);

            HexEditorWCC content = new HexEditorWCC {
                Editor = {
                    Document = new MemoryBinaryDocument(buffer, false)
                }
            };

            IWindow window = system.CreateWindow(content);
            window.Show(system.GetActiveWindowOrNull());
        }
    }

    private class HexEditorWCC : WindowingContentControl {
        public HexEditor Editor { get; }

        public HexEditorWCC() {
            this.Content = this.Editor = new HexEditor() {
                FontFamily = new FontFamily("Consolas"),
                Columns = {
                    new OffsetColumn(),
                    new HexColumn(),
                    new AsciiColumn()
                },
            };

            this.Editor.HexView.BytesPerLine = 32;
            // TODO: we can create a modified version of HexColumn, specifically a custom
            // HexTextSource impl, maybe we can return multiple TextCharacters with different
            // brushes for regular and CHANGED bytes?
            // this.Editor.HexView.Layers.Add(new TestZeroOutlineLayer() {
            //     Bits = { new BitLocation(0, 1), new BitLocation(2, 1), new BitLocation(8, 0) }
            // });
        }

        private class TestZeroOutlineLayer : Layer {
            /// <inheritdoc />
            public override LayerRenderMoments UpdateMoments => LayerRenderMoments.Always;

            public ObservableList<BitLocation> Bits { get; } = new ObservableList<BitLocation>();

            /// <summary>
            /// Creates a new caret layer.
            /// </summary>
            /// <param name="caret">The caret to render.</param>
            public TestZeroOutlineLayer() {
                this.IsHitTestVisible = false;
                ObservableItemProcessor.MakeSimple(this.Bits, (x) => this.InvalidateVisual(), (x) => this.InvalidateVisual());
            }

            /// <inheritdoc />
            public override void Render(DrawingContext context) {
                base.Render(context);

                if (this.HexView == null || !this.HexView.IsFocused)
                    return;

                foreach (BitLocation location in this.Bits) {
                    VisualBytesLine? line = this.HexView.GetVisualLineByLocation(location);
                    if (line == null) {
                        continue;
                    }

                    for (int i = 0; i < this.HexView.Columns.Count; i++) {
                        Column column = this.HexView.Columns[i];
                        if (column is CellBasedColumn cbc && cbc.IsVisible) {
                            Rect bounds = cbc.GetCellBounds(line, location);
                            context.DrawRectangle(Brushes.DarkRed, null, bounds);
                        }
                    }
                }
            }
        }

        protected override void OnWindowOpened() {
            base.OnWindowOpened();
            this.Window!.Control.MinWidth = 1000;
            this.Window!.Control.MinHeight = 640;
            this.Window.CanAutoSizeToContent = true;
        }
    }
}