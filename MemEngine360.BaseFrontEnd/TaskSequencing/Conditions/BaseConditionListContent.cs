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

using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using MemEngine360.Sequencing;
using MemEngine360.Sequencing.Conditions;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Conditions;

public abstract class BaseConditionListContent : UserControl {
    public static readonly ModelTypeControlRegistry<BaseConditionListContent> Registry = new ModelTypeControlRegistry<BaseConditionListContent>();
    public static readonly StyledProperty<BaseSequenceCondition?> ConditionProperty = AvaloniaProperty.Register<BaseConditionListContent, BaseSequenceCondition?>(nameof(Condition));

    public BaseSequenceCondition? Condition {
        get => this.GetValue(ConditionProperty);
        set => this.SetValue(ConditionProperty, value);
    }

    private Ellipse? isConditionMetEllipse;
    private CancellationTokenSource? animationCts;
    private SolidColorBrush? fillBrush;
    private bool hasAnimationStarted;

    protected BaseConditionListContent() {
    }

    static BaseConditionListContent() {
        ConditionProperty.Changed.AddClassHandler<BaseConditionListContent, BaseSequenceCondition?>((o, e) => o.OnConditionChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
        Registry.RegisterType(typeof(CompareMemoryCondition), () => new CompareMemoryConditionListContent());
    }

    protected void SetConditionMetIndicator(Ellipse ellipse) {
        this.isConditionMetEllipse = ellipse;
        this.isConditionMetEllipse.Fill = this.fillBrush = new SolidColorBrush(Colors.Orange, 0.0);
        this.hasAnimationStarted = false;
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        if (this.isConditionMetEllipse != null && this.Condition != null) {
            this.TransitionToMetState(this.Condition.IsCurrentlyMet, true);
        }
    }

    protected virtual void OnConditionChanged(BaseSequenceCondition? oldCondition, BaseSequenceCondition? newCondition) {
        if (oldCondition != null) {
            oldCondition.IsEnabledChanged -= this.OnIsEnabledChanged;
            oldCondition.IsCurrentlyMetChanged -= this.OnIsCurrentMetChanged;
        }

        this.hasAnimationStarted = false;
        if (newCondition != null) {
            newCondition.IsEnabledChanged += this.OnIsEnabledChanged;
            newCondition.IsCurrentlyMetChanged += this.OnIsCurrentMetChanged;
            this.TransitionToMetState(newCondition.IsCurrentlyMet, true);
        }

        this.UpdateOpacity();
    }

    private void OnIsEnabledChanged(BaseSequenceCondition condition) {
        this.UpdateOpacity();
    }

    private void OnIsCurrentMetChanged(BaseSequenceCondition sender) {
        ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => this.TransitionToMetState(sender.IsCurrentlyMet, false));
    }

    private void UpdateOpacity() {
        BaseSequenceCondition? operation = this.Condition;
        this.Opacity = operation == null || operation.IsEnabled ? 1.0 : 0.5;
    }

    private void TransitionToMetState(bool isMet, bool isInitial) {
        if (this.isConditionMetEllipse == null) {
            return;
        }

        if (isMet) {
            this.hasAnimationStarted = false;
            if (this.animationCts != null) {
                this.animationCts.Cancel();
                this.animationCts.Dispose();
                this.animationCts = null;
            }

            this.fillBrush!.Opacity = 1.0;
        }
        else if (isInitial) {
            this.hasAnimationStarted = false;
            this.fillBrush!.Opacity = 0.0;
        }
        else if (!this.hasAnimationStarted) {
            this.hasAnimationStarted = true;
            Animation animation = new Animation {
                Duration = TimeSpan.FromSeconds(0.5),
                Easing = new SineEaseOut(), FillMode = FillMode.Forward,
                Children = {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Brush.OpacityProperty, 1.0) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Brush.OpacityProperty, 0.0) } }
                }
            };

            this.animationCts = new CancellationTokenSource();
            animation.RunAsync(this.fillBrush!, this.animationCts.Token);
        }
    }
}