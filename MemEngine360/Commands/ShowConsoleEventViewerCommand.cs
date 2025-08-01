﻿// 
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

using MemEngine360.Connections.Traits;
using MemEngine360.Engine;
using PFXToolKitUI;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class ShowConsoleEventViewerCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine engine, CommandEventArgs e) {
        return engine.Connection is IHaveSystemEvents ? Executability.Valid : Executability.ValidButCannotExecute;
    }

    protected override Task ExecuteCommandAsync(MemoryEngine engine, CommandEventArgs e) {
        if (engine.Connection is IHaveSystemEvents) {
            return ApplicationPFX.Instance.ServiceManager.GetService<IConsoleEventViewerService>().ShowOrFocus(engine);
        }

        return Task.CompletedTask;
    }
}