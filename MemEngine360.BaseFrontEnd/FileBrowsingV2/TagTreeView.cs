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
// using System.Collections.ObjectModel;
// using Avalonia;
// using Avalonia.Controls;
// using MemEngine360.Engine.FileBrowsing;
//
// namespace MemEngine360.BaseFrontEnd.FileBrowsingV2;
//
// public class TagTreeView : ItemsControl {
//     public static readonly StyledProperty<FileTreeExplorer?> FileTreeExplorerProperty = AvaloniaProperty.Register<TagTreeView, FileTreeExplorer?>(nameof(FileTreeExplorer));
//
//     public FileTreeExplorer? FileTreeExplorer {
//         get => this.GetValue(FileTreeExplorerProperty);
//         set => this.SetValue(FileTreeExplorerProperty, value);
//     }
//
//     private readonly ObservableCollection<FlatNode> nodes;
//     private readonly Dictionary<BaseFileTreeNode, FlatNode> tagToNode;
//
//     internal sealed class FlatNode {
//         public readonly BaseFileTreeNode Node;
//         public int Depth;
//         public bool IsExpanded;
//
//         public FlatNode(BaseFileTreeNode node) {
//             this.Node = node;
//         }
//     }
//
//     public TagTreeView() {
//         this.tagToNode = new Dictionary<BaseFileTreeNode, FlatNode>();
//         this.ItemsSource = this.nodes = new ObservableCollection<FlatNode>();
//     }
//
//     static TagTreeView() {
//         FileTreeExplorerProperty.Changed.AddClassHandler<TagTreeView, FileTreeExplorer?>((s, e) => s.OnTagSourceChanged(e.OldValue.GetValueOrDefault(), e.NewValue.GetValueOrDefault()));
//     }
//
//     // Do we need a container?
//     protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) {
//         return this.NeedsContainer<TagTreeViewItem>(item, out recycleKey);
//     }
//
//     // Create container
//     protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) {
//         return new TagTreeViewItem();
//     }
//
//     // Prepare container
//     protected override void PrepareContainerForItemOverride(Control container, object? item, int index) {
//         base.PrepareContainerForItemOverride(container, item, index);
//         ((TagTreeViewItem) container).InternalOnUse(this, (FlatNode) item!);
//     }
//
//     // Container is prepared and ready
//     protected override void ContainerForItemPreparedOverride(Control container, object? item, int index) {
//         base.ContainerForItemPreparedOverride(container, item, index);
//     }
//
//     protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex) {
//         base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
//     }
//
//     // The container is no longer in use but may be used later; virtualization is happening
//     protected override void ClearContainerForItemOverride(Control container) {
//         base.ClearContainerForItemOverride(container);
//         ((TagTreeViewItem) container).InternalOnRecycle();
//     }
//
//     private void OnTagSourceChanged(FileTreeExplorer? oldValue, FileTreeExplorer? newValue) {
//         this.nodes.Clear();
//         this.tagToNode.Clear();
//
//         if (oldValue != null) {
//             oldValue.NodeAdded -= this.OnNodeAdded;
//             oldValue.NodeRemoved -= this.OnNodeRemoved;
//         }
//
//         if (newValue != null) {
//             newValue.NodeAdded += this.OnNodeAdded;
//             newValue.NodeRemoved += this.OnNodeRemoved;
//
//             int i = 0;
//             foreach (BaseFileTreeNode child in newValue.Children) {
//                 this.OnNodeAdded(newValue, new FileTreeNodeDirectory.InsertOrRemoveEventArgs(i++, child));
//             }
//         }
//     }
//
//     private void OnNodeAdded(object? _sender, FileTreeNodeDirectory.InsertOrRemoveEventArgs e) {
//         if (_sender is not FileTreeNodeDirectory sender)
//             return;
//
//         FlatNode? parentFlat = null;
//         int insertIndex;
//         if (ReferenceEquals(sender, this.FileTreeExplorer)) {
//             insertIndex = GetInsertion(sender, 0, e.Index);
//         }
//         else {
//             parentFlat = this.GetOrCreateFlatNode(sender);
//             if (!parentFlat.IsExpanded) {
//                 FlatNode hiddenFlat = this.GetOrCreateFlatNode(e.Child);
//                 hiddenFlat.Depth = parentFlat.Depth + 1;
//                 this.AttachEvents(hiddenFlat.Node);
//                 return;
//             }
//
//             insertIndex = GetInsertion(sender, this.GetInsertionIndex(parentFlat), e.Index);
//         }
//
//         FlatNode childFlat = this.GetOrCreateFlatNode(e.Child);
//         childFlat.Depth = ReferenceEquals(sender, this.FileTreeExplorer) ? 0 : parentFlat!.Depth + 1;
//         this.AttachEvents(childFlat.Node);
//
//         this.nodes.Insert(insertIndex, childFlat);
//         if (childFlat.IsExpanded)
//             this.Expand(childFlat);
//
//         return;
//         
//         int GetInsertion(FileTreeNodeDirectory parent, int initialIndex, int countTo) {
//             for (int i = 0; i < countTo; i++) {
//                 BaseFileTreeNode sibling = parent.Children[i];
//                 if (this.tagToNode.TryGetValue(sibling, out FlatNode? siblingFlat))
//                     initialIndex += this.CountVisibleSubtree(siblingFlat);
//             }
//
//             return initialIndex;
//         }
//     }
//
//     private void OnNodeRemoved(object? sender, FileTreeNodeDirectory.InsertOrRemoveEventArgs e) {
//         if (!this.tagToNode.TryGetValue(e.Child, out FlatNode? childFlat))
//             return;
//
//         int startIndex = this.nodes.IndexOf(childFlat);
//         if (startIndex >= 0) {
//             int count = this.CountVisibleSubtree(childFlat);
//             for (int i = 0; i < count; i++) {
//                 this.DetachEvents(this.nodes[startIndex].Node, false);
//                 this.nodes.RemoveAt(startIndex);
//             }
//         }
//
//         this.tagToNode.Remove(e.Child);
//     }
//
//     private void AttachEvents(BaseFileTreeNode node) {
//         if (node is FileTreeNodeDirectory col) {
//             col.NodeAdded += this.OnNodeAdded;
//             col.NodeRemoved += this.OnNodeRemoved;
//         }
//     }
//
//     private void DetachEvents(BaseFileTreeNode node, bool recursive) {
//         if (recursive) {
//             DetachEventsRecursive(node);
//         }
//         else if (node is FileTreeNodeDirectory col) {
//             col.NodeAdded -= this.OnNodeAdded;
//             col.NodeRemoved -= this.OnNodeRemoved;
//         }
//
//         return;
//
//         void DetachEventsRecursive(BaseFileTreeNode n) {
//             if (n is FileTreeNodeDirectory c) {
//                 c.NodeAdded -= this.OnNodeAdded;
//                 c.NodeRemoved -= this.OnNodeRemoved;
//                 foreach (BaseFileTreeNode child in c.Children) {
//                     DetachEventsRecursive(child);
//                 }
//             }
//         }
//     }
//
//     private FlatNode GetOrCreateFlatNode(BaseFileTreeNode node) {
//         if (!this.tagToNode.TryGetValue(node, out FlatNode? flat)) {
//             flat = new FlatNode(node);
//             this.tagToNode[node] = flat;
//         }
//
//         return flat;
//     }
//
//     private int GetInsertionIndex(FlatNode node) {
//         int depth = node.Depth;
//
//         int i = this.nodes.IndexOf(node) + 1;
//         while (i < this.nodes.Count && this.nodes[i].Depth > depth)
//             i++;
//
//         return i;
//     }
//
//     private int CountVisibleSubtree(FlatNode node) {
//         int count = 1; // the node itself
//         int index = this.nodes.IndexOf(node) + 1;
//         while (index < this.nodes.Count && this.nodes[index].Depth > node.Depth) {
//             count++;
//             index++;
//         }
//
//         return count;
//     }
//
//     internal void Expand(FlatNode node) {
//         if (!node.IsExpanded) {
//             node.IsExpanded = true;
//             if (node.Node is FileTreeNodeDirectory collection) {
//                 int insertIndex = this.GetInsertionIndex(node);
//                 foreach (BaseFileTreeNode child in collection.Items) {
//                     FlatNode childFlat = this.GetOrCreateFlatNode(child);
//                     childFlat.Depth = node.Depth + 1;
//
//                     this.AttachEvents(childFlat.Node);
//                     this.nodes.Insert(insertIndex++, childFlat);
//
//                     if (childFlat.IsExpanded)
//                         this.Expand(childFlat);
//                 }
//             }
//         }
//     }
//
//     internal void Collapse(FlatNode node) {
//         if (node.IsExpanded) {
//             node.IsExpanded = false;
//
//             int index = this.nodes.IndexOf(node) + 1;
//             while (index < this.nodes.Count && this.nodes[index].Depth > node.Depth) {
//                 this.DetachEvents(node.Node, false);
//                 this.nodes.RemoveAt(index);
//             }
//         }
//     }
//
//     // Optional helper for UI toggle
//     internal void Toggle(FlatNode node) {
//         if (node.IsExpanded)
//             this.Collapse(node);
//         else
//             this.Expand(node);
//     }
// }