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

using Avalonia;
using Avalonia.Media;
using AvaloniaHex.Core.Document;
using AvaloniaHex.Editing;
using AvaloniaHex.Rendering;

namespace MemEngine360.BaseFrontEnd.Services.HexEditing;

public class AutoRefreshLayer : Layer {
    private readonly Caret theCaret;
    private BitRange theRange;

    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IPen?> PrimarySelectionBorderProperty = AvaloniaProperty.Register<AutoRefreshLayer, IPen?>(nameof(PrimarySelectionBorder), new Pen(Brushes.DarkGreen));

    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> PrimarySelectionBackgroundProperty = AvaloniaProperty.Register<AutoRefreshLayer, IBrush?>(nameof(PrimarySelectionBackground), new SolidColorBrush(Colors.DarkGreen, 0.5D));

    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IPen?> SecondarySelectionBorderProperty = AvaloniaProperty.Register<AutoRefreshLayer, IPen?>(nameof(PrimarySelectionBorder), new Pen(Brushes.DarkGreen));

    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IBrush?> SecondarySelectionBackgroundProperty = AvaloniaProperty.Register<AutoRefreshLayer, IBrush?>(nameof(SecondarySelectionBackgroundProperty), new SolidColorBrush(Colors.DarkGreen, 0.25D));

    public static readonly StyledProperty<bool> IsActiveProperty = AvaloniaProperty.Register<AutoRefreshLayer, bool>(nameof(IsActive));
    
    /// <summary>
    /// Gets or sets the pen used for drawing the border of the selection in the active column.
    /// </summary>
    public IPen? PrimarySelectionBorder {
        get => this.GetValue(PrimarySelectionBorderProperty);
        set => this.SetValue(PrimarySelectionBorderProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for drawing the background of the selection in the active column.
    /// </summary>
    public IBrush? PrimarySelectionBackground {
        get => this.GetValue(PrimarySelectionBackgroundProperty);
        set => this.SetValue(PrimarySelectionBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the pen used for drawing the border of the selection in non-active columns.
    /// </summary>
    public IPen? SecondarySelectionBorder {
        get => this.GetValue(SecondarySelectionBorderProperty);
        set => this.SetValue(SecondarySelectionBorderProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush used for drawing the background of the selection in non-active columns.
    /// </summary>
    public IBrush? SecondarySelectionBackground {
        get => this.GetValue(SecondarySelectionBackgroundProperty);
        set => this.SetValue(SecondarySelectionBackgroundProperty, value);
    }

    public bool IsActive {
        get => this.GetValue(IsActiveProperty);
        set => this.SetValue(IsActiveProperty, value);
    }

    /// <inheritdoc />
    public override LayerRenderMoments UpdateMoments => LayerRenderMoments.NoResizeRearrange;

    public AutoRefreshLayer(Caret theCaret) {
        this.theCaret = theCaret;
    }

    static AutoRefreshLayer() {
        AffectsRender<AutoRefreshLayer>(
            PrimarySelectionBorderProperty, 
            PrimarySelectionBackgroundProperty, 
            SecondarySelectionBorderProperty, 
            SecondarySelectionBackgroundProperty, 
            IsActiveProperty);
    }

    public void SetRange(BitRange newRange) {
        this.theRange = newRange;
        this.InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) {
        base.Render(context);

        if (this.HexView == null || this.GetVisibleSelectionRange() is not { } range)
            return;

        for (int i = 0; i < this.HexView.Columns.Count; i++) {
            if (this.HexView.Columns[i] is CellBasedColumn { IsVisible: true } column)
                this.DrawSelection(context, column, range);
        }
    }

    private BitRange? GetVisibleSelectionRange() {
        if (this.HexView == null || !this.theRange.OverlapsWith(this.HexView.VisibleRange))
            return null;

        return new BitRange(this.theRange.Start.Max(this.HexView.VisibleRange.Start), this.theRange.End.Min(this.HexView.VisibleRange.End)
        );
    }

    private void DrawSelection(DrawingContext context, CellBasedColumn column, BitRange range) {
        Geometry? geometry = CellGeometryBuilder.CreateBoundingGeometry(column, range);
        if (geometry is null)
            return;

        if (this.theCaret.PrimaryColumnIndex == column.Index)
            context.DrawGeometry(this.IsActive ? this.PrimarySelectionBackground : null, this.PrimarySelectionBorder, geometry);
        else
            context.DrawGeometry(this.IsActive ? this.SecondarySelectionBackground : null, this.SecondarySelectionBorder, geometry);
    }
}