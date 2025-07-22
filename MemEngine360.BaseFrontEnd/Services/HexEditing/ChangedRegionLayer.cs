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

    public HexEditorChangeManager Manager => this.manager;

    public DateTime LastUpdatedTime => this.lastUpdatedTime;

    /// <inheritdoc />
    public override LayerRenderMoments UpdateMoments => LayerRenderMoments.NoResizeRearrange;

    private readonly Lock ctsLock = new Lock();
    private CancellationTokenSource? cts;
    private readonly HexEditorChangeManager manager;
    private readonly Caret theCaret;
    private BitRange theRange;
    private DateTime lastUpdatedTime;
    private Animation? animation;

    public ChangedRegionLayer(HexEditorChangeManager manager) {
        this.manager = manager;
        this.theCaret = manager.Editor.Caret;
    }

    static ChangedRegionLayer() {
        AffectsRender<ChangedRegionLayer>(PrimarySelectionBorderProperty, SecondarySelectionBorderProperty);
    }

    public void SetRange(BitRange newRange) {
        this.lastUpdatedTime = DateTime.Now;
        this.theRange = newRange;
        this.InvalidateVisual();
        using (this.ctsLock.EnterScope()) {
            if (Interlocked.Exchange(ref this.cts, null) is CancellationTokenSource oldCts) {
                try {
                    oldCts.Cancel();
                }
                catch (ObjectDisposedException) {
                    Debug.Fail("Impossible");
                }
                finally {
                    oldCts.Dispose();
                }
            }
        }

        this.cts = new CancellationTokenSource();
        this.animation ??= new Animation {
            Duration = TimeSpan.FromSeconds(1.5),
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

        CancellationToken token = this.cts.Token;
        this.animation.RunAsync(this, token).ContinueWith(async (t) => {
            try {
                if (!token.IsCancellationRequested) {
                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        if (!token.IsCancellationRequested)
                            this.manager.OnFadeOutCompleted(this);
                    });
                }
            }
            finally {
                using (this.ctsLock.EnterScope()) {
                    if (Interlocked.Exchange(ref this.cts, null) is CancellationTokenSource oldCts) {
                        try {
                            oldCts.Cancel();
                        }
                        catch (ObjectDisposedException) {
                            Debug.Fail("Impossible");
                        }
                        finally {
                            oldCts.Dispose();
                        }
                    }
                }
            }
        }, TaskContinuationOptions.ExecuteSynchronously);
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
        return new BitRange(this.theRange.Start.Max(this.HexView.VisibleRange.Start), this.theRange.End.Min(this.HexView.VisibleRange.End));
    }

    private void DrawSelection(DrawingContext context, CellBasedColumn column, BitRange range) {
        Geometry? geometry = CellGeometryBuilder.CreateBoundingGeometry(column, range);
        if (geometry == null)
            return;

        if (this.theCaret.PrimaryColumnIndex == column.Index)
            context.DrawGeometry(null, this.PrimarySelectionBorder, geometry);
        else
            context.DrawGeometry(null, this.SecondarySelectionBorder, geometry);
    }
}