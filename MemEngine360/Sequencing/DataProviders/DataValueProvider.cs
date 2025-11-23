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

using MemEngine360.ValueAbstraction;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.Sequencing.DataProviders;

public abstract class DataValueProvider {
    /// <summary>
    /// Gets or sets whether to add a null-character to the end of strings. Setting this to false could cause the
    /// game to become unstable/laggy if the original null char gets overwritten and the next one is really far away
    /// (since it may try to render a million characters)
    /// </summary>
    public bool AppendNullCharToString {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.AppendNullCharToStringChanged);
    } = true;

    /// <summary>
    /// Gets a lock that should be acquired when reading/writing properties that may be accessed by multiple threads.
    /// For maximum safety, wrap <see cref="Provide"/> in a lock expression
    /// </summary>
    public Lock Lock { get; } = new Lock();
    
    public event EventHandler? AppendNullCharToStringChanged;
    
    protected DataValueProvider() {
    }
    
    /// <summary>
    /// Tries to provide a data value. Returns null if the provider didn't want to return a value, e.g. randomly decide to return a value or not
    /// </summary>
    /// <returns>The provided value</returns>
    public abstract IDataValue? Provide();

    public abstract DataValueProvider CreateClone();
}