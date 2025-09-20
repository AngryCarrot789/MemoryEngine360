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

using MemEngine360.Sequencing;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia.Interactivity.Windowing;
using PFXToolKitUI.Avalonia.Utils;

namespace MemEngine360.BaseFrontEnd.TaskSequencing.Conditions;

public class EditConditionOutputModeServiceImpl : IEditConditionOutputModeService {
    public async Task<ConditionOutputMode?> EditTriggerMode(ConditionOutputMode initialMode) {
        return await await ApplicationPFX.Instance.Dispatcher.InvokeAsync(async () => {
            IWindow? parentWindow = WindowContextUtils.GetUsefulWindow();
            if (parentWindow != null) {
                EditConditionOutputModeView view = new EditConditionOutputModeView(initialMode);
                IWindow window = parentWindow.WindowManager.CreateWindow(new WindowBuilder() {
                    Title = "Edit output mode",
                    Content = view,
                    Width = 260, Height = 290,
                    Parent = parentWindow
                });

                view.Window = window;
                bool? result = await window.ShowDialogAsync() as bool?;
                view.Window = null;
                if (result == true) {
                    return view.OutputMode;
                }
            }

            return (ConditionOutputMode?) null;
        });
    }
}