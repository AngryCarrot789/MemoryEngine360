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

using PFXToolKitUI.Themes;
using PFXToolKitUI.Themes.Configurations;

namespace MemEngine360.BaseFrontEnd.Themes;

public class MemEngineBrushLoader {
    public static void Init() {
        ThemeManager manager = ThemeManager.Instance;
        ThemeConfigurationPage p = manager.ThemeConfigurationPage;
        
        p.AssignMapping("Memory Engine/Engine/Background", "ABrush.MemEngine.MainView.Background", "The background of the engine window");

        p.AssignMapping("Memory Engine/Engine/Scan Options/TextBox/Background", "ABrush.MemEngine.MainView.ScanOptions.TextBox.Background");
        p.AssignMapping("Memory Engine/Engine/Scan Options/TextBox/Border", "ABrush.MemEngine.MainView.ScanOptions.TextBox.Border");
        p.AssignMapping("Memory Engine/Engine/Scan Options/Combo/Scan Type Background", "ABrush.MemEngine.MainView.ScanOptions.ScanTypeComboBox.Background");
        p.AssignMapping("Memory Engine/Engine/Scan Options/Combo/Scan Type Border", "ABrush.MemEngine.MainView.ScanOptions.ScanTypeComboBox.Border");
        p.AssignMapping("Memory Engine/Engine/Scan Options/Combo/Compare Mode Background", "ABrush.MemEngine.MainView.ScanOptions.CompareModeComboBox.Background");
        p.AssignMapping("Memory Engine/Engine/Scan Options/Combo/Compare Mode Border", "ABrush.MemEngine.MainView.ScanOptions.CompareModeComboBox.Border");
        
        p.AssignMapping("Memory Engine/Engine/Scan Results/Background", "ABrush.MemEngine.MainView.ScanResults.Background");
        p.AssignMapping("Memory Engine/Engine/Scan Results/Header", "ABrush.MemEngine.MainView.ScanResults.Header");
        p.AssignMapping("Memory Engine/Engine/Scan Results/Border", "ABrush.MemEngine.MainView.ScanResults.Border");
        
        p.AssignMapping("Memory Engine/Engine/Saved Addresses/Background", "ABrush.MemEngine.MainView.SavedAddresses.Background");
        p.AssignMapping("Memory Engine/Engine/Saved Addresses/Header", "ABrush.MemEngine.MainView.SavedAddresses.Header");
        p.AssignMapping("Memory Engine/Engine/Saved Addresses/Border", "ABrush.MemEngine.MainView.SavedAddresses.Border");
        p.AssignMapping("Memory Engine/Engine/Saved Addresses/Column Separator", "ABrush.MemEngine.MainView.SavedAddresses.ColumnSeparator");
        p.AssignMapping("Memory Engine/Engine/Saved Addresses/Button/Background", "ABrush.MemEngine.MainView.SavedAddresses.Button.Background");
        p.AssignMapping("Memory Engine/Engine/Saved Addresses/Button/Border", "ABrush.MemEngine.MainView.SavedAddresses.Button.Border");
        
        p.AssignMapping("Memory Engine/Engine/Additional Options/Background", "ABrush.MemEngine.MainView.AdditionOptions.Background");
        p.AssignMapping("Memory Engine/Engine/Additional Options/Header", "ABrush.MemEngine.MainView.AdditionOptions.Header");
        p.AssignMapping("Memory Engine/Engine/Additional Options/Border", "ABrush.MemEngine.MainView.AdditionOptions.Border");
        p.AssignMapping("Memory Engine/Engine/Additional Options/TextBox/Background", "ABrush.MemEngine.MainView.AdditionOptions.TextBox.Background");
        p.AssignMapping("Memory Engine/Engine/Additional Options/TextBox/Border", "ABrush.MemEngine.MainView.AdditionOptions.TextBox.Border");
        p.AssignMapping("Memory Engine/Engine/Additional Options/Button/Background", "ABrush.MemEngine.MainView.AdditionOptions.Button.Background");
        p.AssignMapping("Memory Engine/Engine/Additional Options/Button/Border", "ABrush.MemEngine.MainView.AdditionOptions.Button.Border");
    }
}