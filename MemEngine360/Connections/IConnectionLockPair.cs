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

using MemEngine360.Engine;

namespace MemEngine360.Connections;

/// <summary>
/// An interface that provides access to a connection bound by a busy lock
/// </summary>
public interface IConnectionLockPair {
    /// <summary>
    /// Gets the busy lock used to obtain busy tokens, which must be taken before using the connection
    /// </summary>
    BusyLock BusyLock { get; }

    /// <summary>
    /// Gets the connection currently available
    /// </summary>
    IConsoleConnection? Connection { get; }

    async Task BeginBusyOperationActivityAsync(Func<IDisposable, IConsoleConnection, Task> action, string caption = "New Operation", string message = BusyLock.WaitingMessage) {
        if (this.Connection == null)
            return; // short path -- save creating an activity

        using IDisposable? token = await this.BusyLock.BeginBusyOperationUsingActivityAsync(caption, message);
        IConsoleConnection theConn; // save double volatile read
        if (token != null && (theConn = this.Connection) != null) {
            await action(token, theConn);
        }
    }

    async Task<TResult?> BeginBusyOperationActivityAsync<TResult>(Func<IDisposable, IConsoleConnection, Task<TResult>> action, string caption = "New Operation", string message = BusyLock.WaitingMessage) {
        if (this.Connection == null)
            return default; // short path -- save creating an activity

        using IDisposable? token = await this.BusyLock.BeginBusyOperationUsingActivityAsync(caption, message);
        IConsoleConnection theConn; // save double volatile read
        if (token != null && (theConn = this.Connection) != null) {
            return await action(token, theConn);
        }

        return default;
    }

    static IConnectionLockPair Constant(BusyLock busyLock, IConsoleConnection? connection) => new ConnectionLockPair(busyLock, connection);
    
    static IConnectionLockPair Lambda(Func<BusyLock> lockProvider, Func<IConsoleConnection?> connectionProvider) => new LambdaConnectionLockPair(lockProvider, connectionProvider);
    
    static IConnectionLockPair Lambda<T>(T state, Func<T, BusyLock> lockProvider, Func<T, IConsoleConnection?> connectionProvider) => new LambdaConnectionLockPairEx<T>(state, lockProvider, connectionProvider);
    
    static IConnectionLockPair Lambda<T>(T state, BusyLock busyLock, Func<T, IConsoleConnection?> connectionProvider) => new LambdaConnectionLockPairEx2<T>(state, busyLock, connectionProvider);
}

public sealed class LambdaConnectionLockPair(Func<BusyLock> lockProvider, Func<IConsoleConnection?> connectionProvider) : IConnectionLockPair {
    public BusyLock BusyLock => lockProvider();
    public IConsoleConnection? Connection => connectionProvider();
}

public sealed class LambdaConnectionLockPairEx<T>(T state, Func<T, BusyLock> lockProvider, Func<T, IConsoleConnection?> connectionProvider) : IConnectionLockPair {
    public BusyLock BusyLock => lockProvider(state);
    public IConsoleConnection? Connection => connectionProvider(state);
}

public sealed class LambdaConnectionLockPairEx2<T>(T state, BusyLock busyLock, Func<T, IConsoleConnection?> connectionProvider) : IConnectionLockPair {
    public BusyLock BusyLock => busyLock;
    public IConsoleConnection? Connection => connectionProvider(state);
}

public sealed class ConnectionLockPair(BusyLock busyLock, IConsoleConnection? connection) : IConnectionLockPair {
    public BusyLock BusyLock { get; } = busyLock;
    public IConsoleConnection? Connection { get; } = connection;
}