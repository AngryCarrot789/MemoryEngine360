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
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaHex.Base.Document;
using AvaloniaHex.Async.Editing;
using AvaloniaHex.Async.Rendering;
using PFXToolKitUI;
using PFXToolKitUI.Utils.Debouncing;

namespace MemEngine360.BaseFrontEnd.Services.HexEditing;

// 8303A000, 4000

public class ChangedRegionLayer : Layer {
    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IPen?> PrimarySelectionBorderProperty = AvaloniaProperty.Register<ChangedRegionLayer, IPen?>(nameof(PrimarySelectionBorder), new Pen(Brushes.Orange));

    /// <summary>
    /// Defines the <see cref="PrimarySelectionBorder"/> property.
    /// </summary>
    public static readonly StyledProperty<IPen?> SecondarySelectionBorderProperty = AvaloniaProperty.Register<ChangedRegionLayer, IPen?>(nameof(PrimarySelectionBorder), new Pen(Brushes.Orange));

    /// <summary>
    /// Gets or sets the pen used for drawing the border of the selection in the active column.
    /// </summary>
    public IPen? PrimarySelectionBorder {
        get => this.GetValue(PrimarySelectionBorderProperty);
        set => this.SetValue(PrimarySelectionBorderProperty, value);
    }

    /// <summary>
    /// Gets or sets the pen used for drawing the border of the selection in non-active columns.
    /// </summary>
    public IPen? SecondarySelectionBorder {
        get => this.GetValue(SecondarySelectionBorderProperty);
        set => this.SetValue(SecondarySelectionBorderProperty, value);
    }

    /// <summary>
    /// Gets our range
    /// </summary>
    public BitRange Range => this.theRange;

    public HexEditorChangeManager Manager { get; }

    public DateTime LastUpdatedTime { get; private set; }

    /// <inheritdoc />
    public override LayerRenderMoments UpdateMoments => LayerRenderMoments.NoResizeRearrange;

    private CancellationTokenSource? animCts;
    private readonly Caret theCaret;
    private BitRange theRange;
    private Animation? animation;

    // We use a debouncer to partially delay the animation for a short period that is barely noticeable to the user.
    // This also massively reduces GC activity
    private readonly TimerDispatcherDebouncer animDelayDebouncer;

    public ChangedRegionLayer(HexEditorChangeManager manager) {
        this.Manager = manager;
        this.theCaret = manager.Editor.Caret;
        this.animDelayDebouncer = new TimerDispatcherDebouncer(
            TimeSpan.FromMilliseconds(HexEditorChangeManager.AnimDebounceDelay),
            static s => ((ChangedRegionLayer) s!).OnDebounceAnimation(),
            this);
    }

    static ChangedRegionLayer() {
        AffectsRender<ChangedRegionLayer>(PrimarySelectionBorderProperty, SecondarySelectionBorderProperty);
    }

    public void SetRange(BitRange newRange) {
        this.LastUpdatedTime = DateTime.Now;
        if (this.theRange != newRange) {
            this.theRange = newRange;
            this.InvalidateVisual();
        }

        this.TryCancelAnimation();
        if (this.Opacity < 1.0) {
            this.Opacity = 1.0;
        }

        this.animDelayDebouncer.Reset();
        this.animDelayDebouncer.InvokeOrPostpone();
    }

    private void OnDebounceAnimation() {
        _ = this.RunAnimationAsync();
    }

    private async Task RunAnimationAsync() {
        Debug.Assert(this.animCts == null);
        this.animation ??= new Animation {
            Duration = TimeSpan.FromMilliseconds(HexEditorChangeManager.AnimDurationMillis),
            Easing = new SineEaseOut(), FillMode = FillMode.Forward,
            Children = {
                new KeyFrame {
                    Cue = new Cue(0),
                    Setters = {
                        new Setter(OpacityProperty, 1.0)
                    }
                },
                new KeyFrame {
                    Cue = new Cue(1),
                    Setters = {
                        new Setter(OpacityProperty, 0.0)
                    }
                }
            }
        };

        this.animCts = new CancellationTokenSource();

        try {
            await this.animation.RunAsync(this, this.animCts.Token);
            if (!this.animCts.IsCancellationRequested) {
                this.Manager.OnFadeOutCompleted(this);
            }
        }
        finally {
            this.animCts.Dispose();
            this.animCts = null;
        }
    }

    private void TryCancelAnimation() {
        this.animCts?.Cancel();
    }

    public override void Render(DrawingContext context) {
        base.Render(context);

        if (this.HexView == null || this.GetVisibleSelectionRange() is not { } range)
            return;

        foreach (Column c in this.HexView.Columns) {
            if (c is CellBasedColumn { IsVisible: true } column)
                this.DrawSelection(context, column, range);
        }
    }

    private BitRange? GetVisibleSelectionRange() {
        if (this.HexView == null || !this.theRange.OverlapsWith(this.HexView.VisibleRange))
            return null;
        return new BitRange(this.theRange.Start.Max(this.HexView.VisibleRange.Start), this.theRange.End.Min(this.HexView.VisibleRange.End));
    }

    private void DrawSelection(DrawingContext context, CellBasedColumn column, BitRange range) {
        Geometry? geometry = CellGeometryBuilder.CreateBoundingGeometry(column, range);
        if (geometry != null) {
            IPen? pen = this.theCaret.PrimaryColumnIndex == column.Index ? this.PrimarySelectionBorder : this.SecondarySelectionBorder;
            context.DrawGeometry(null, pen, geometry);
        }
    }
}