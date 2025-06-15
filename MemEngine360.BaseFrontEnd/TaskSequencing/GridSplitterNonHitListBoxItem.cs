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
using Avalonia.Controls;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing;

public class GridSplitterNonHitListBoxItem : GridSplitter, ICustomHitTest {
    public bool HitTest(Point point) {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null)
            return false;

        Point? targetPoint = this.TranslatePoint(point, topLevel);
        if (!targetPoint.HasValue)
            return true;

        Visual? hit = topLevel.GetVisualAt(targetPoint.Value, visual => !(visual is GridSplitter));
        ListBoxItem? listItem = VisualTreeUtils.GetParent<ListBoxItem>(hit);
        return listItem == null;
    }
}