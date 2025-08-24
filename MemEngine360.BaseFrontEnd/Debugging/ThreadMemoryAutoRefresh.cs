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

using AvaloniaHex.Async;
using AvaloniaHex.Base.Document;
using MemEngine360.Connections;
using MemEngine360.Engine.Debugging;
using MemEngine360.Engine.HexEditing;
using PFXToolKitUI;

namespace MemEngine360.BaseFrontEnd.Debugging;

public class ThreadMemoryAutoRefresh : IDisposable {
    private readonly DebuggerWindow window;
    private volatile CancellationTokenSource? cts;
    private Task? task;

    public ConsoleDebugger Debugger { get; }

    public ThreadMemoryAutoRefresh(ConsoleDebugger debugger, DebuggerWindow window) {
        this.Debugger = debugger;
        this.window = window;
        this.cts = new CancellationTokenSource();
    }

    public void Run() {
        if (this.cts == null || this.cts.IsCancellationRequested) {
            return;
        }

        this.task = Task.Run(async () => {
            CancellationToken token = this.cts?.Token ?? new CancellationToken(true);
            if (token.IsCancellationRequested)
                return;

            while (!token.IsCancellationRequested) {
                BitRange visibleRange = await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => this.window.PART_HexEditor.HexView.VisibleRange, token: CancellationToken.None);
                if (visibleRange.ByteLength > 0) {
                    if (visibleRange.Start.ByteIndex >= uint.MaxValue) {
                        await Task.Delay(500, token);
                        continue;
                    }

                    uint addr = (uint) visibleRange.Start.ByteIndex;
                    int len = visibleRange.ByteLength > int.MaxValue ? int.MaxValue : (int) visibleRange.ByteLength;

                    IConsoleConnection? connection = this.Debugger.Connection;
                    if (connection == null || connection.IsClosed) {
                        this.Dispose();
                        return;
                    }

                    byte[] bytes;
                    using (IDisposable? t = await this.Debugger.BusyLock.BeginBusyOperationAsync(500, token)) {
                        if (t == null)
                            continue;

                        if ((connection = this.Debugger.Connection) == null || connection.IsClosed) {
                            this.Dispose();
                            return;
                        }

                        try {
                            bytes = await connection.ReadBytes(addr, len);
                        }
                        catch (Exception) {
                            await Task.Delay(500, token);
                            continue;
                        }
                    }

                    await ApplicationPFX.Instance.Dispatcher.InvokeAsync(() => {
                        if (this.cts != null && !this.cts.IsCancellationRequested) {
                            Span<byte> span = bytes.AsSpan(0, len);
                            AsyncHexEditor editor = this.window.PART_HexEditor!;
                            this.window.changeManager.ProcessChanges(addr, span);
                            ((ConsoleHexBinarySource) editor.BinarySource!).WriteBytesToCache(addr, span);
                            
                            this.window.UpdateSelectionText();
                            this.window.UpdateCaretText();
                            
                        }
                    }, token: CancellationToken.None);
                }

                await Task.Delay(250, token);
            }
        });
    }

    public void Dispose() {
        this.cts?.Cancel();
        this.cts?.Dispose();
        this.cts = null;
    }
}