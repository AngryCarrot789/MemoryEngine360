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

using PFXToolKitUI.Themes;
using PFXToolKitUI.Themes.Configurations;

namespace MemEngine360.BaseFrontEnd.Themes;

public static class MemoryEngineBrushLoader {
    public static void Init() {
        if (ThemeManager.Instance.Themes.Count(x => x.IsBuiltIn) < 2) {
            throw new InvalidOperationException("Called too early; expected at least two built-in themes: dark and light");
        }
        
        ThemeManager manager = ThemeManager.Instance;
        ThemeConfigurationPage p = manager.ThemeConfigurationPage;
        
        // MAKE SURE TO UPDATE ANY INHERITANCE IN MemoryEngineThemes.axaml TOO! Otherwise, the app won't look the same at runtime compared to design time | This is the inheritance column |
        List<(string, string, string)> items = [
            ("Memory Engine/Engine/Background",                                 "ABrush.MemEngine.MainView.Background",                                 "ABrush.Tone3.Background.Static"),
            ("Memory Engine/Engine/Status bar",                                 "ABrush.PFX.StatusBar.Background",                                      "ABrush.Tone6.Background.Static"),
            
            ("Memory Engine/Engine/Scan Options/TextBox/Background",            "ABrush.MemEngine.MainView.ScanOptions.TextBox.Background",             "ABrush.Tone2.Background.Static"),
            ("Memory Engine/Engine/Scan Options/TextBox/Border",                "ABrush.MemEngine.MainView.ScanOptions.TextBox.Border",                 "ABrush.Tone6.Border.Static"),
            ("Memory Engine/Engine/Scan Options/TextBox/Foreground",            "ABrush.MemEngine.MainView.ScanOptions.TextBox.Foreground",             "ABrush.Foreground.Static"),
            ("Memory Engine/Engine/Scan Options/Combo/Scan Type Background",    "ABrush.MemEngine.MainView.ScanOptions.ScanTypeComboBox.Background",    "ABrush.Tone2.Background.Static"),
            ("Memory Engine/Engine/Scan Options/Combo/Scan Type Border",        "ABrush.MemEngine.MainView.ScanOptions.ScanTypeComboBox.Border",        "ABrush.Tone6.Border.Static"),
            ("Memory Engine/Engine/Scan Options/Combo/Compare Mode Background", "ABrush.MemEngine.MainView.ScanOptions.CompareModeComboBox.Background", "ABrush.Tone2.Background.Static"),
            ("Memory Engine/Engine/Scan Options/Combo/Compare Mode Border",     "ABrush.MemEngine.MainView.ScanOptions.CompareModeComboBox.Border",     "ABrush.Tone6.Border.Static"),
            
            ("Memory Engine/Engine/Scan Results/Background",                    "ABrush.MemEngine.MainView.ScanResults.Background",                     "ABrush.Tone2.Background.Static"),
            ("Memory Engine/Engine/Scan Results/Header",                        "ABrush.MemEngine.MainView.ScanResults.Header",                         "ABrush.Tone5.Background.Static"),
            ("Memory Engine/Engine/Scan Results/Border",                        "ABrush.MemEngine.MainView.ScanResults.Border",                         "ABrush.Tone1.Border.Static"),
            
            ("Memory Engine/Engine/Saved Addresses/Background",                 "ABrush.MemEngine.MainView.SavedAddresses.Background",                  "ABrush.Tone2.Background.Static"),
            ("Memory Engine/Engine/Saved Addresses/Header",                     "ABrush.MemEngine.MainView.SavedAddresses.Header",                      "ABrush.Tone5.Background.Static"),
            ("Memory Engine/Engine/Saved Addresses/Border",                     "ABrush.MemEngine.MainView.SavedAddresses.Border",                      "ABrush.Tone1.Border.Static"),
            ("Memory Engine/Engine/Saved Addresses/Column Separator",           "ABrush.MemEngine.MainView.SavedAddresses.ColumnSeparator",             "ABrush.Tone6.Border.Static"),
            ("Memory Engine/Engine/Saved Addresses/Button/Background",          "ABrush.MemEngine.MainView.SavedAddresses.Button.Background",           "ABrush.Tone6.Background.Static"),
            ("Memory Engine/Engine/Saved Addresses/Button/Border",              "ABrush.MemEngine.MainView.SavedAddresses.Button.Border",               "ABrush.Tone6.Border.Static"),
            ("Memory Engine/Engine/Saved Addresses/Button/Foreground",          "ABrush.MemEngine.MainView.SavedAddresses.Button.Foreground",           "ABrush.Foreground.Static"),
            
            ("Memory Engine/Engine/Additional Options/Background",              "ABrush.MemEngine.MainView.AdditionOptions.Background",                 "ABrush.Tone3.Background.Static"),
            ("Memory Engine/Engine/Additional Options/Header",                  "ABrush.MemEngine.MainView.AdditionOptions.Header",                     "ABrush.Tone5.Background.Static"),
            ("Memory Engine/Engine/Additional Options/Border",                  "ABrush.MemEngine.MainView.AdditionOptions.Border",                     "ABrush.Tone1.Border.Static"),
            ("Memory Engine/Engine/Additional Options/TextBox/Background",      "ABrush.MemEngine.MainView.AdditionOptions.TextBox.Background",         "ABrush.Tone2.Background.Static"),
            ("Memory Engine/Engine/Additional Options/TextBox/Border",          "ABrush.MemEngine.MainView.AdditionOptions.TextBox.Border",             "ABrush.Tone3.Border.Static"),
            ("Memory Engine/Engine/Additional Options/TextBox/Foreground",      "ABrush.MemEngine.MainView.AdditionOptions.TextBox.Foreground",         "ABrush.Foreground.Static"),
            ("Memory Engine/Engine/Additional Options/Button/Background",       "ABrush.MemEngine.MainView.AdditionOptions.Button.Background",          "ABrush.Tone7.Background.Static"),
            ("Memory Engine/Engine/Additional Options/Button/Border",           "ABrush.MemEngine.MainView.AdditionOptions.Button.Border",              "ABrush.Tone7.Border.Static"),
            ("Memory Engine/Engine/Additional Options/Button/Foreground",       "ABrush.MemEngine.MainView.AdditionOptions.Button.Foreground",          "ABrush.Foreground.Static"),
        ];
        
        Dictionary<string, string?> inheritMap = new Dictionary<string, string?>();
        foreach ((string path, string theme, string inherit) item in items) {
            inheritMap[item.theme] = item.inherit;
        }
        
        foreach (Theme theme in ThemeManager.Instance.GetBuiltInThemes()) {
            theme.SetInheritance(inheritMap);
        }
        
        foreach ((string path, string theme, string inherit) item in items) {
            p.AssignMapping(item.path, item.theme);
        }
    }
}