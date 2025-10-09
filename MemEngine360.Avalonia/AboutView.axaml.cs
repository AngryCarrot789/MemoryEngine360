﻿// 
// Copyright (c) 2024-2025 REghZy
// 
// This file is part of FramePFX.
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3 of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with FramePFX. If not, see <https://www.gnu.org/licenses/>.
// 

using Avalonia.Controls;
using Avalonia.Interactivity;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;

namespace MemEngine360.Avalonia;

public partial class AboutView : UserControl {
    public AboutView() {
        this.InitializeComponent();
    }
    
    private void Button_OnClick(object? sender, RoutedEventArgs e) {
        IWindowBase? window = IWindowBase.WindowFromVisual(this);
        if (window != null && window.OpenState == OpenState.Open) {
            window.RequestClose();
        }
    }
}