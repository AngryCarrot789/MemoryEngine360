// // 
// // Copyright (c) 2024-2025 REghZy
// // 
// // This file is part of MemoryEngine360.
// // 
// // MemoryEngine360 is free software; you can redistribute it and/or
// // modify it under the terms of the GNU General Public License
// // as published by the Free Software Foundation; either
// // version 3.0 of the License, or (at your option) any later version.
// // 
// // MemoryEngine360 is distributed in the hope that it will be useful,
// // but WITHOUT ANY WARRANTY; without even the implied warranty of
// // MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// // Lesser General Public License for more details.
// // 
// // You should have received a copy of the GNU General Public License
// // along with MemoryEngine360. If not, see <https://www.gnu.org/licenses/>.
// // 
//
// using Avalonia;
// using Avalonia.Controls;
// using Avalonia.Controls.Metadata;
// using Avalonia.Controls.Mixins;
// using Avalonia.Controls.Primitives;
// using Avalonia.Data;
// using Avalonia.Input;
// using Avalonia.Interactivity;
//
// namespace MemEngine360.BaseFrontEnd.FileBrowsingV2;
//
// [TemplatePart("PART_Header", typeof(Control))]
// [PseudoClasses(":pressed", ":selected")]
// public class TagTreeViewItem : HeaderedContentControl {
//     public static readonly StyledProperty<BaseTagTreeNode?> TagNodeProperty = AvaloniaProperty.Register<TagTreeViewItem, BaseTagTreeNode?>(nameof(TagNode));
//     public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<TagTreeViewItem, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);
//     public static readonly StyledProperty<bool> IsSelectedProperty = SelectingItemsControl.IsSelectedProperty.AddOwner<TagTreeViewItem>();
//     public static readonly DirectProperty<TagTreeViewItem, int> LevelProperty = AvaloniaProperty.RegisterDirect<TagTreeViewItem, int>(nameof(Level), o => o.Level);
//
//     public BaseTagTreeNode? TagNode {
//         get => this.GetValue(TagNodeProperty);
//         set => this.SetValue(TagNodeProperty, value);
//     }
//
//     public bool IsExpanded {
//         get => this.GetValue(IsExpandedProperty);
//         set => this.SetValue(IsExpandedProperty, value);
//     }
//
//     public bool IsSelected {
//         get => this.GetValue(IsSelectedProperty);
//         set => this.SetValue(IsSelectedProperty, value);
//     }
//
//     public int Level {
//         get => field;
//         private set => this.SetAndRaise(LevelProperty, ref field, value);
//     }
//
//     public event EventHandler<RoutedEventArgs>? Expanded {
//         add => this.AddHandler(ExpandedEvent, value);
//         remove => this.RemoveHandler(ExpandedEvent, value);
//     }
//
//     public event EventHandler<RoutedEventArgs>? Collapsed {
//         add => this.AddHandler(CollapsedEvent, value);
//         remove => this.RemoveHandler(CollapsedEvent, value);
//     }
//
//     private TagTreeView? parentTreeView;
//     private TagTreeView.FlatNode? flatNode;
//
//     public static readonly RoutedEvent<RoutedEventArgs> ExpandedEvent = RoutedEvent.Register<TagTreeViewItem, RoutedEventArgs>(nameof(Expanded), RoutingStrategies.Bubble | RoutingStrategies.Tunnel);
//     public static readonly RoutedEvent<RoutedEventArgs> CollapsedEvent = RoutedEvent.Register<TagTreeViewItem, RoutedEventArgs>(nameof(Collapsed), RoutingStrategies.Bubble | RoutingStrategies.Tunnel);
//
//     public TagTreeViewItem() {
//     }
//
//     static TagTreeViewItem() {
//         SelectableMixin.Attach<TagTreeViewItem>(IsSelectedProperty);
//         TagNodeProperty.Changed.AddClassHandler<TagTreeViewItem, BaseTagTreeNode?>((s, e) => s.OnTagNodeChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
//         IsExpandedProperty.Changed.AddClassHandler<TagTreeViewItem, bool>((x, e) => x.OnIsExpandedChanged(e));
//     }
//
//     protected override void OnKeyDown(KeyEventArgs e) {
//         base.OnKeyDown(e);
//
//         if (e.Key == Key.Right) {
//             this.IsExpanded = true;
//         }
//         else if (e.Key == Key.Left) {
//             this.IsExpanded = false;
//         }
//     }
//
//     protected override void OnPointerPressed(PointerPressedEventArgs e) {
//         base.OnPointerPressed(e);
//         if (e.ClickCount % 2 == 0 && e.Properties.IsLeftButtonPressed) {
//             this.IsExpanded = !this.IsExpanded;
//         }
//     }
//
//     internal void InternalOnUse(TagTreeView parentTree, TagTreeView.FlatNode node) {
//         this.parentTreeView = parentTree;
//         this.flatNode = node;
//         this.TagNode = node.Node;
//         this.Level = node.Depth;
//
//         this.SetCurrentValue(IsSelectedProperty, false);
//         this.SetCurrentValue(IsExpandedProperty, node.IsExpanded);
//     }
//     
//     internal void InternalOnRecycle() {
//         this.parentTreeView = null;
//         this.flatNode = null;
//         this.TagNode = null;
//         this.Level = 0;
//         
//         this.ClearValue(IsSelectedProperty);
//         this.ClearValue(IsExpandedProperty);
//     }
//     
//     private void OnIsExpandedChanged(AvaloniaPropertyChangedEventArgs<bool> args) {
//         RoutedEvent<RoutedEventArgs> routedEvent = args.NewValue.Value ? ExpandedEvent : CollapsedEvent;
//         this.RaiseEvent(new RoutedEventArgs() { RoutedEvent = routedEvent, Source = this });
//
//         if (args.NewValue.Value) {
//             this.parentTreeView?.Expand(this.flatNode!);
//         }
//         else {
//             this.parentTreeView?.Collapse(this.flatNode!);
//         }
//     }
//
//     protected virtual void OnTagNodeChanged(BaseTagTreeNode? oldValue, BaseTagTreeNode? newValue) {
//         this.Header = newValue?.ToString();
//     }
// }