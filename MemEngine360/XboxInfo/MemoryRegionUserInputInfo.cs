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

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using MemEngine360.Connections;
using PFXToolKitUI.Services.UserInputs;
using PFXToolKitUI.Utils;

namespace MemEngine360.XboxInfo;

public delegate void MemoryRegionUserInputInfoSelectedRegionChangedEventHandler(MemoryRegionUserInputInfo sender, MemoryRegionViewModel? oldSelectedRegion, MemoryRegionViewModel? newSelectedRegion);

public class MemoryRegionUserInputInfo : UserInputInfo {
    private MemoryRegionViewModel? selectedRegion;

    /// <summary>
    /// Gets the collection of memory regions presented to the user
    /// </summary>
    public ObservableCollection<MemoryRegionViewModel> MemoryRegions { get; }

    /// <summary>
    /// Gets or sets the selected region
    /// </summary>
    public MemoryRegionViewModel? SelectedRegion {
        get => this.selectedRegion;
        set {
            MemoryRegionViewModel? oldSelectedRegion = this.selectedRegion;
            if (oldSelectedRegion == value)
                return;

            this.selectedRegion = value;
            this.SelectedRegionChanged?.Invoke(this, oldSelectedRegion, value);
            if (oldSelectedRegion == null || value == null) {
                this.RaiseHasErrorsChanged();
            }
        }
    }

    public Func<uint, string>? RegionFlagsToTextConverter { get; set; }

    public event MemoryRegionUserInputInfoSelectedRegionChangedEventHandler? SelectedRegionChanged;

    public MemoryRegionUserInputInfo() : this(new ObservableCollection<MemoryRegionViewModel>()) {
    }

    public MemoryRegionUserInputInfo(IEnumerable<MemoryRegion> regions) : this(new ObservableCollection<MemoryRegionViewModel>(regions.Select(x => new MemoryRegionViewModel(x)))) {
    }

    private MemoryRegionUserInputInfo(ObservableCollection<MemoryRegionViewModel> items) {
        this.MemoryRegions = items;
        items.CollectionChanged += this.ItemsOnCollectionChanged;
    }

    private void ItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        if (this.MemoryRegions.Count < 2)
            this.RaiseHasErrorsChanged();
    }

    public override bool HasErrors() {
        return this.MemoryRegions.Count < 1 || this.SelectedRegion == null;
    }

    public override void UpdateAllErrors() {
    }

    public static string ConvertXboxFlagsToText(uint flags) {
        StringJoiner sj = new StringJoiner(", ");
        if ((flags & 1) != 0)
            sj.Append("NoAccess");
        if ((flags & 2) != 0)
            sj.Append("ReadOnly");
        if ((flags & 4) != 0)
            sj.Append("ReadWrite");
        if ((flags & 8) != 0)
            sj.Append("WriteCopy");
        if ((flags & 16) != 0)
            sj.Append("Execute");
        if ((flags & 32) != 0)
            sj.Append("ExecuteRead");
        if ((flags & 64) != 0)
            sj.Append("ExecuteReadWrite");
        if ((flags & 128) != 0)
            sj.Append("ExecuteWriteCopy");
        if ((flags & 256) != 0)
            sj.Append("Guard");
        if ((flags & 512) != 0)
            sj.Append("NoCache");
        if ((flags & 1024) != 0)
            sj.Append("WriteCombine");
        if ((flags & 4096) != 0)
            sj.Append("UserReadOnly");
        if ((flags & 8192) != 0)
            sj.Append("UserReadWrite");
        return sj.ToString();
    }
}