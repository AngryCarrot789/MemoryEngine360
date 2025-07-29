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

using MemEngine360.Configs;
using MemEngine360.Engine;
using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Commands;

public class ResetScanOptionsCommand : BaseMemoryEngineCommand {
    protected override Executability CanExecuteCore(MemoryEngine engine, CommandEventArgs e) {
        return engine.ScanningProcessor.IsScanning ? Executability.ValidButCannotExecute : Executability.Valid;
    }

    protected override Task ExecuteCommandAsync(MemoryEngine engine, CommandEventArgs e) {
        if (!engine.ScanningProcessor.IsScanning) {
            engine.ScanningProcessor.SetScanRange(BasicApplicationConfiguration.StartAddressProperty.DefaultValue, BasicApplicationConfiguration.ScanLengthProperty.DefaultValue);
            engine.ScanningProcessor.ScanMemoryPages = BasicApplicationConfiguration.ScanMemoryPagesProperty.DefaultValue;
        }

        return Task.CompletedTask;
    }
}