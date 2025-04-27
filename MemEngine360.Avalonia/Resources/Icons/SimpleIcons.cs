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

using PFXToolKitUI.Icons;
using PFXToolKitUI.Themes;
using SkiaSharp;

namespace MemEngine360.Avalonia.Resources.Icons;

public static class SimpleIcons {
    // public static readonly Icon MemoryIcon = IconManager.Instance.RegisterIconByUri(nameof(SimpleIcons) + "#" + nameof(MemoryIcon), new Uri("avares://MemEngine360-DesktopUI/Resources/Icons/icons8-memory-48.png"));

    // 5 pins per side:
    // M 3.5 0 L 4.5 0 L 4.5 2 L 3.5 2 Z M 5.5 0 L 6.5 0 L 6.5 2 L 5.5 2 Z M 7.5 0 L 8.5 0 L 8.5 2 L 7.5 2 Z M 9.5 0 L 10.5 0 L 10.5 2 L 9.5 2 Z M 11.5 0 L 12.5 0 L 12.5 2 L 11.5 2 Z M 0 3.5 L 2 3.5 L 2 4.5 L 0 4.5 Z M 0 5.5 L 2 5.5 L 2 6.5 L 0 6.5 Z M 0 7.5 L 2 7.5 L 2 8.5 L 0 8.5 Z M 0 9.5 L 2 9.5 L 2 10.5 L 0 10.5 Z M 0 11.5 L 2 11.5 L 2 12.5 L 0 12.5 Z M 3.5 14 L 4.5 14 L 4.5 16 L 3.5 16 Z M 5.5 14 L 6.5 14 L 6.5 16 L 5.5 16 Z M 7.5 14 L 8.5 14 L 8.5 16 L 7.5 16 Z M 9.5 14 L 10.5 14 L 10.5 16 L 9.5 16 Z M 11.5 14 L 12.5 14 L 12.5 16 L 11.5 16 Z M 14 3.5 L 16 3.5 L 16 4.5 L 14 4.5 Z M 14 5.5 L 16 5.5 L 16 6.5 L 14 6.5 Z M 14 7.5 L 16 7.5 L 16 8.5 L 14 8.5 Z M 14 9.5 L 16 9.5 L 16 10.5 L 14 10.5 Z M 14 11.5 L 16 11.5 L 16 12.5 L 14 12.5 Z M 2 2 L 14 2 L 14 14 L 2 14 Z
    
    public static readonly Icon MemoryIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(MemoryIcon),
            [
                new GeometryEntry("M 0 2 L 16 2 L 16 14 L 0 14 Z", BrushManager.Instance.CreateConstant(SKColors.MediumSeaGreen)),
                new GeometryEntry("M 2 0 L 4 0 L 4 2 L 2 2 Z M 12 0 L 14 0 L 14 2 L 12 2 Z Z Z Z Z M 2 14 L 4 14 L 4 16 L 2 16 Z M 12 14 L 14 14 L 14 16 L 12 16 Z Z M 7 2 L 7 0 L 9 0 L 9 2 L 7 2 M 7 14 L 7 16 L 9 16 L 9 14 L 7 14", BrushManager.Instance.CreateConstant(SKColors.Gold))
            ], stretch: StretchMode.Uniform);
    
    public static readonly Icon ResetIcon =
        IconManager.Instance.RegisterGeometryIcon(
            nameof(ResetIcon),
            [
                new GeometryEntry("M 8 1.4615 C 10.3018 1.4615 12.3774 2.4304 13.842 3.9828 L 13.842 0 L 16.0327 2.1923 L 16.0328 8.0385 L 10.1907 8.0385 L 8 5.8461 L 12.0823 5.8461 C 11.0794 4.7249 9.622 4.0192 8 4.0192 C 5.2229 4.0192 2.9284 6.0878 2.5714 8.7693 L 0 8.7693 C 0.369 4.6721 3.8098 1.4615 8 1.4615 Z M 8 14.9808 C 10.7772 14.9808 13.0716 12.9122 13.4285 10.2308 L 16 10.2308 C 15.6311 14.3279 12.1902 17.5385 8 17.5385 C 5.6982 17.5385 3.6225 16.5697 2.158 15.0172 L 2.158 19 L -0.0328 16.8077 L -0.0328 10.9615 L 5.8092 10.9615 L 8 13.1539 L 3.9177 13.1539 C 4.9206 14.2752 6.378 14.9808 8 14.9808", 
                    BrushManager.Instance.GetDynamicThemeBrush("ABrush.Glyph.Static"))
            ], 
            stretch: StretchMode.Uniform);
}

/*
    M 4 0 L 5 0 L 5 2 L 4 2 Z M 11 0 L 12 0 L 12 2 L 11 2 Z M 0 4 L 2 4 L 2 5 L 0 5 Z M 0 11 L 2 11 L 2 12 L 0 12 Z M 14 4 L 16 4 L 16 5 L 14 5 Z M 14 11 L 16 11 L 16 12 L 14 12 Z M 4 14 L 5 14 L 5 16 L 4 16 Z M 11 14 L 12 14 L 12 16 L 11 16 Z M 2 2 L 14 2 L 14 14 L 2 14 Z

    
*/