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

using System.Diagnostics;
using MemEngine360.Engine;
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Utils;
using PFXToolKitUI.Utils.Collections.Observable;

namespace MemEngine360.Scripting;

public class ScriptingManager : IComponentManager, IUserLocalContext {
    public static readonly DataKey<ScriptingManager> DataKey = DataKeys.Create<ScriptingManager>(nameof(ScriptingManager));

    ComponentStorage IComponentManager.ComponentStorage => field ??= new ComponentStorage(this);

    public IMutableContextData UserContext { get; } = new ContextData();

    /// <summary>
    /// Gets the list of scripts
    /// </summary>
    public ObservableList<Script> Scripts { get; }

    public MemoryEngine MemoryEngine { get; }

    public ScriptingManager(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;
        this.Scripts = new ObservableList<Script>();
        this.Scripts.ValidateAdd += (list, index, items) => {
            foreach (Script item in items) {
                if (item == null)
                    throw new ArgumentNullException(nameof(items), "List contains a null entry");
                if (item.Manager == this)
                    throw new InvalidOperationException("Entry already exists in this entry. It must be removed first");
                if (item.Manager != null)
                    throw new InvalidOperationException("Entry already exists in another sequence manager. It must be removed first");
                Debug.Assert(!item.IsRunning, "Impossible for a sequence to run without a manager");
            }
        };

        this.Scripts.ValidateRemove += (list, index, count) => {
            for (int i = 0; i < count; i++) {
                list[index + i].CheckNotRunning("Cannot remove sequence while it's running");
                if (list[index + i].IsCompiling)
                    Debug.Fail("Script is still compiling but attempted to remove it");
            }
        };

        this.Scripts.ValidateReplace += (list, index, oldItem, newItem) => {
            if (newItem == null)
                throw new ArgumentNullException(nameof(newItem), "Cannot replace sequence with null");

            oldItem.CheckNotRunning("Cannot replace item while it's running");
            if (oldItem.IsCompiling)
                Debug.Fail("Script is still compiling but attempted to replace it");

            newItem.CheckNotRunning("Replacement item cannot be running");
        };

        this.Scripts.ItemsAdded += (list, index, items) => items.ForEach(this, (x, t) => x.myManager = t);
        this.Scripts.ItemsRemoved += (list, index, items) => items.ForEach(x => x.myManager = null);
        this.Scripts.ItemReplaced += (list, index, oldItem, newItem) => {
            oldItem.myManager = null;
            newItem.myManager = this;
        };

        this.Scripts.Add(new Script());
        this.Scripts[0].SetCustomNameWithoutPath("Cool Script.lua");
        this.Scripts[0].Document.Text = "-- read BO2 ammo count of primary weapon\n" +
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
                                        "end";
    }
}