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

using System.Collections.ObjectModel;
using System.Diagnostics;
using MemEngine360.Engine;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Scripting;

public class ScriptingManager : IComponentManager, IUserLocalContext {
    ComponentStorage IComponentManager.ComponentStorage => field ??= new ComponentStorage(this);

    private readonly List<Script> myScripts;

    public IMutableContextData UserContext { get; } = new ContextData();

    /// <summary>
    /// Gets the list of scripts
    /// </summary>
    public ReadOnlyCollection<Script> Scripts { get; }

    public MemoryEngine MemoryEngine { get; }

    public event EventHandler<ItemAddOrRemoveEventArgs<Script>>? ScriptAdded, ScriptRemoved;
    public event EventHandler<ItemMoveEventArgs<Script>>? ScriptMoved;

    public ScriptingManager(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;
        this.myScripts = new List<Script>();
        this.Scripts = this.myScripts.AsReadOnly();

        Script testScript = new Script() {
            Document = {
                Text = "-- read BO2 ammo count of primary weapon\n" +
                       "local oldAmmo = engine.readnumber(\"0x83551E4C\", \"int\")\n" +
                       "print(\"Old ammo = \" .. oldAmmo)\n" +
                       "\n" +
                       "-- add 20 to the primary ammo, slowly\n" +
                       "local num = 0\n" +
                       "while true do\n" +
                       "    num = num + 1\n" +
                       "    engine.writenumber(\"0x83551E4C\", \"int\", oldAmmo + num)\n" +
                       "    sleep(0.1)\n" +
                       "    if (num == 20) then\n" +
                       "        local newAmmo = engine.readnumber(\"0x83551E4C\", \"int\")\n" +
                       "        print(\"New ammo = \" .. newAmmo)\n" +
                       "        return\n" +
                       "    end\n" +
                       "end"
            },
            HasUnsavedChanges = false // changing text sets this as true
        };
        
        testScript.SetCustomNameWithoutPath("Test ammo.lua");
        this.AddScript(testScript);
    }

    public void AddScript(Script script) {
        ArgumentNullException.ThrowIfNull(script);
        if (script.Manager == this)
            throw new InvalidOperationException("Script already added");
        if (script.Manager != null)
            throw new InvalidOperationException("Script already exists in another scripting manager. It must be removed first");
        if (script.IsRunning)
            throw new InvalidOperationException("Script cannot be running");
        if (script.IsCompiling)
            throw new InvalidOperationException("Script cannot be compiling");

        int index = this.Scripts.Count;
        script.myManager = this;

        this.myScripts.Insert(index, script);
        this.ScriptAdded?.Invoke(this, new ItemAddOrRemoveEventArgs<Script>(index, script));
    }

    public void RemoveScript(Script script) {
        ArgumentNullException.ThrowIfNull(script);
        if (script.myManager != this)
            throw new InvalidOperationException("Script does not exist in this manager");
        if (script.IsRunning)
            throw new InvalidOperationException("Script is running. It must be stopped first.");
        if (script.IsCompiling)
            throw new InvalidOperationException("Script is compiling. It must be cancelled first.");

        int index = this.myScripts.IndexOf(script);
        if (index == -1) {
            Debug.Fail("Impossible");
            return;
        }

        script.myManager = null;

        this.myScripts.RemoveAt(index);
        this.ScriptRemoved?.Invoke(this, new ItemAddOrRemoveEventArgs<Script>(index, script));
    }

    public void MoveScript(int oldIndex, int newIndex) {
        Script item = this.myScripts[oldIndex];
        this.myScripts.MoveItem(oldIndex, newIndex);
        if (oldIndex != newIndex) {
            this.ScriptMoved?.Invoke(this, new ItemMoveEventArgs<Script>(oldIndex, newIndex, item));
        }
    }
}