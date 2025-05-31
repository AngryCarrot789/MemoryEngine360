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

using MemEngine360.Connections;
using PFXToolKitUI.Tasks;

namespace MemEngine360.Sequencing;

/// <summary>
/// Contains context information about a currently running sequence
/// </summary>
public sealed class SequenceExecutionContext {
    /// <summary>
    /// Gets the sequence that that owns this context
    /// </summary>
    public TaskSequence Sequence { get; }
    
    /// <summary>
    /// The activity progress used to track what's currently going on with the sequence
    /// </summary>
    public IActivityProgress Progress { get; }
    
    /// <summary>
    /// Gets the connection being used by this sequence
    /// </summary>
    public IConsoleConnection Connection { get; }

    /// <summary>
    /// Gets the priority busy token, or null, if <see cref="TaskSequence.HasBusyLockPriority"/> is false
    /// </summary>
    public IDisposable? BusyToken { get; }

    /// <summary>
    /// Returns true when <see cref="Connection"/> is dedicated and has nothing to do with the memory engine, meaning <see cref="BusyToken"/> will be null
    /// </summary>
    public bool IsConnectionDedicated { get; }

    public SequenceExecutionContext(TaskSequence sequence, IActivityProgress progress, IConsoleConnection connection, IDisposable? busyToken, bool isConnectionDedicated) {
        this.Sequence = sequence;
        this.Progress = progress;
        this.Connection = connection;
        this.BusyToken = busyToken;
        this.IsConnectionDedicated = isConnectionDedicated;
    }
}