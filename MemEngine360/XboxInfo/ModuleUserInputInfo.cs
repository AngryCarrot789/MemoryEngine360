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
using PFXToolKitUI.Services.UserInputs;

namespace MemEngine360.XboxInfo;

public delegate void ModuleUserInputInfoSelectedRegionChangedEventHandler(ModuleUserInputInfo sender, string? oldSelectedRegion, string? newSelectedRegion);

public class ModuleUserInputInfo : UserInputInfo {
    private string? selectedRegion;

    /// <summary>
    /// Gets the collection of memory regions presented to the user
    /// </summary>
    public ObservableCollection<string> MemoryRegions { get; }

    /// <summary>
    /// Gets or sets the selected region
    /// </summary>
    public string? SelectedRegion {
        get => this.selectedRegion;
        set {
            string? oldSelectedRegion = this.selectedRegion;
            if (oldSelectedRegion == value)
                return;

            this.selectedRegion = value;
            this.SelectedRegionChanged?.Invoke(this, oldSelectedRegion, value);
            if (oldSelectedRegion == null || value == null) {
                this.RaiseHasErrorsChanged();
            }
        }
    }

    public event ModuleUserInputInfoSelectedRegionChangedEventHandler? SelectedRegionChanged;

    public ModuleUserInputInfo() : this(new ObservableCollection<string>()) {
    }

    private ModuleUserInputInfo(ObservableCollection<string> items) {
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
}