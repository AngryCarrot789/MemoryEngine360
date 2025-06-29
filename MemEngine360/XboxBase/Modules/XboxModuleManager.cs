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

public class XboxModuleManager {
    private static readonly Dictionary<Type, Func<MemoryEngine, IConsoleConnection, XboxModuleManager, Task>> processors = new Dictionary<Type, Func<MemoryEngine, IConsoleConnection, XboxModuleManager, Task>>();
    
    public ObservableList<XboxModule> Modules { get; }

    public XboxModuleManager() {
        this.Modules = new ObservableList<XboxModule>();
        ObservableItemProcessor.MakeSimple(this.Modules, module => module.Manager = this, module => module.Manager = null);
    }

    /// <summary>
    /// Registers a handler that fetches modules from a specific connection type.
    /// The callback is invoked within an activity and while under busy-lock
    /// </summary>
    /// <param name="function">The callback</param>
    /// <typeparam name="T">The type of connection</typeparam>
    public static void RegisterHandlerForConnectionType<T>(Func<MemoryEngine, T, XboxModuleManager, Task> function) where T : IConsoleConnection {
        ArgumentNullException.ThrowIfNull(function);
        processors[typeof(T)] = (engine, connection, manager) => function(engine, (T) connection, manager);
    }

    public static async Task<bool> TryFillModuleManager(MemoryEngine engine, IConsoleConnection connection, XboxModuleManager manager) {
        for (Type? type = connection.GetType(); type != null; type = type.BaseType) {
            if (processors.TryGetValue(type, out Func<MemoryEngine, IConsoleConnection, XboxModuleManager, Task>? processor)) {
                await processor(engine, connection, manager);
                return true;
            }
        }
        
        return false;
    }
}