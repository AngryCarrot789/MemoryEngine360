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

using MemEngine360.Connections;
using MemEngine360.Engine;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.XboxBase.Modules;

public class ModuleViewer {
    private static readonly Dictionary<Type, IModuleManagerProcessor> processors = new Dictionary<Type, IModuleManagerProcessor>();
    
    public ObservableList<ConsoleModule> Modules { get; }

    public ModuleViewer() {
        this.Modules = new ObservableList<ConsoleModule>();
        ObservableItemProcessor.MakeSimple(this.Modules, module => module.Viewer = this, module => module.Viewer = null);
    }

    /// <summary>
    /// Registers a handler that fetches modules from a specific connection type.
    /// The callback is invoked within an activity and while under busy-lock
    /// </summary>
    /// <param name="processor">The callback</param>
    /// <typeparam name="T">The type of connection</typeparam>
    public static void RegisterHandlerForConnectionType<T>(IModuleManagerProcessor processor) where T : IConsoleConnection {
        ArgumentNullException.ThrowIfNull(processor);
        processors[typeof(T)] = processor;
    }

    public static async Task<bool> TryFillModuleManager(MemoryEngine engine, IConsoleConnection connection, ModuleViewer viewer) {
        for (Type? type = connection.GetType(); type != null; type = type.BaseType) {
            if (processors.TryGetValue(type, out IModuleManagerProcessor? processor)) {
                await processor.RefreshAll(viewer, engine, connection);
                return true;
            }
        }
        
        return false;
    }
    
    public static async Task<bool> TryRefreshModule(MemoryEngine engine, IConsoleConnection connection, ConsoleModule module) {
        for (Type? type = connection.GetType(); type != null; type = type.BaseType) {
            if (processors.TryGetValue(type, out IModuleManagerProcessor? processor)) {
                await processor.RefreshModule(module, engine, connection);
                return true;
            }
        }
        
        return false;
    }
}