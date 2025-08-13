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

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using XDevkit;
using XboxExecutionState = MemEngine360.Connections.Features.XboxExecutionState;

namespace MemEngine360.Xbox360XDevkit;

public class XDevkitConsoleConnection : BaseConsoleConnection, IConsoleConnection {
    private readonly XboxManager manager;
    private readonly XboxConsole console;
    private bool isConnectedAsDebugger;
    private readonly FeaturesImpl features;

    public XboxConsole Console => this.console;

    public override RegisteredConnectionType ConnectionType => ConnectionTypeXbox360XDevkit.Instance;

    protected override bool IsConnectedCore => this.isConnectedAsDebugger;

    public override bool IsLittleEndian => false;

    public override AddressRange AddressableRange => new AddressRange(0, uint.MaxValue);

    public XDevkitConsoleConnection(XboxManager manager, XboxConsole console) {
        this.manager = manager;
        this.console = console;
        IXboxDebugTarget dbgTarget = this.console.DebugTarget;
        dbgTarget.MemoryCacheEnabled = false;
        XboxEvents_OnStdNotifyEventHandler handler = this.OnStdNotify;
        this.console.add_OnStdNotify(handler);
        this.console.add_OnTextNotify(this.OnTextNotify);
        this.isConnectedAsDebugger = true;
        this.features = new FeaturesImpl(this);

        // console.DebugTarget.SetDataBreakpoint(0, XboxBreakpointType.OnExecute);
    }
    
    public override bool TryGetFeature<T>([NotNullWhen(true)] out T? feature) where T : class {
        if (this.features is T t) {
            feature = t;
            return true;
        }
        
        return base.TryGetFeature(out feature);
    }

    public override bool HasFeature<T>() {
        return this.features is T || base.HasFeature<T>();
    }

    public override bool HasFeature(Type typeOfFeature) {
        return typeOfFeature.IsInstanceOfType(this.features) || base.HasFeature(typeOfFeature);
    }

    private void OnStdNotify(XboxDebugEventType eventcode, IXboxEventInfo eventinfo) {
        XBOX_EVENT_INFO inf = eventinfo.Info;
        StringBuilder sb = new StringBuilder();
        sb.Append($"IsThreadStopped: {inf.IsThreadStopped != 0}, ");
        sb.Append($"ExecState: {inf.ExecState}, ");
        sb.Append($"Message: {inf.Message}, ");
        sb.Append($"Code: {inf.Code}, ");
        sb.Append($"Address: {inf.Address}, ");
        sb.Append($"Flags: {inf.Flags}, ");
        sb.Append($"ParameterCount: {inf.ParameterCount}");
        if (inf.ParameterCount > 0)
            sb.Append($", Parameters: {string.Join(", ", inf.Parameters)}");

        System.Console.WriteLine($"[StdNotify] {eventcode} -> {sb.ToString()}");
    }

    private void OnTextNotify(string source, string notification) {
        System.Console.WriteLine($"[TextNotify] {source} -> {notification}");
    }

    public override Task<bool?> IsMemoryInvalidOrProtected(uint address, uint count) {
        return Task.FromResult<bool?>(null);
    }

    protected override void CloseOverride() {
        if (this.isConnectedAsDebugger) {
            this.isConnectedAsDebugger = false;
            this.console.remove_OnStdNotify(this.OnStdNotify);
            this.console.remove_OnTextNotify(this.OnTextNotify);
            this.console.DebugTarget.DisconnectAsDebugger();
        }
    }

    public async Task<FreezeResult> DebugFreeze() {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();
        return await Task.Run(() => {
            this.console.DebugTarget.Stop(out bool isAlreadyStopped);
            return isAlreadyStopped ? FreezeResult.AlreadyFrozen : FreezeResult.Success;
        }).ConfigureAwait(false);
    }

    public async Task<UnFreezeResult> DebugUnFreeze() {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();
        return await Task.Run(() => {
            this.console.DebugTarget.Go(out bool isAlreadyGoing);
            return isAlreadyGoing ? UnFreezeResult.AlreadyUnfrozen : UnFreezeResult.Success;
        }).ConfigureAwait(false);
    }

    public async Task<List<MemoryRegion>> GetMemoryRegions(bool willRead, bool willWrite) {
        this.EnsureNotClosed();
        using BusyToken x = this.CreateBusyToken();

        // Debug testing
        // foreach (IXboxThread thread in this.console.DebugTarget.Threads) {
        //     if (thread.ThreadId != 0xF900002C)
        //         continue;
        //     
        //     for (IXboxStackFrame? stack = thread.TopOfStack; stack != null; stack = stack.NextStackFrame) {
        //         XBOX_FUNCTION_INFO FunctionInfo = stack.FunctionInfo;
        //         uint StackPointer = stack.StackPointer;
        //         uint ReturnAddress = stack.ReturnAddress;
        //         Debug.WriteLine(stack);
        //     }
        // }

        List<MemoryRegion> regionList = new List<MemoryRegion>();
        IXboxMemoryRegions regions = await Task.Run(() => {
            try {
                return this.console.DebugTarget.MemoryRegions;
            }
            catch (COMException) {
                throw new TimeoutException("Timeout reading memory regions");
            }
        });
        for (int i = 0, count = regions.Count; i < count; i++) {
            IXboxMemoryRegion region = regions[i];
            regionList.Add(new MemoryRegion((uint) region.BaseAddress, (uint) region.RegionSize, (uint) region.Flags, 0));
        }

        return regionList;
    }

    protected override Task ReadBytesCore(uint address, byte[] dstBuffer, int offset, int count) {
        return Task.Run(() => {
            IXboxDebugTarget target = this.console.DebugTarget;
            target.GetMemory_cpp(address, (uint) count, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(dstBuffer), offset), out uint cbRead);
            if (cbRead < count) {
                dstBuffer.AsSpan((int) cbRead).Clear();
            }
        });
    }

    protected override Task WriteBytesCore(uint address, byte[] srcBuffer, int offset, int count) {
        return Task.Run(() => {
            this.console.DebugTarget.SetMemory_cpp(address, (uint) count, ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(srcBuffer), offset), out uint cbWritten);
        });
    }

    private class FeaturesImpl : IConsoleFeature, IFeatureXboxThreads, IFeatureMemoryRegions, IFeatureIceCubes, IFeatureFileSystemInfo { // IFeatureSystemEvents
        private readonly XDevkitConsoleConnection connection;

        public IConsoleConnection Connection => this.connection;

        public FeaturesImpl(XDevkitConsoleConnection connection) {
            this.connection = connection;
        }

        private static XboxThread XBThreadFromCOM(IXboxThread thread) {
            XBOX_THREAD_INFO info = thread.ThreadInfo;
            return new XboxThread() {
                id = thread.ThreadId,
                suspendCount = info.SuspendCount,
                priority = info.Priority,
                tlsBaseAddress = info.TlsBase,
                baseAddress = info.StackBase,
                stackLimit = info.StackLimit,
                stackSlack = info.StackSlackSpace,
                creationTime = 0,
                nameAddress = 0,
                nameLength = 0,
                currentProcessor = thread.CurrentProcessor,
                lastError = thread.LastError,
                readableName = info.Name
            };
        }

        public async Task<XboxThread> GetThreadInfo(uint threadId, bool requireName = true) {
            this.connection.EnsureNotClosed();
            using BusyToken token = this.connection.CreateBusyToken();

            return await Task.Run(() => {
                foreach (IXboxThread thread in this.connection.console.DebugTarget.Threads) {
                    if (thread.ThreadId == threadId) {
                        return XBThreadFromCOM(thread);
                    }
                }

                return default;
            });
        }

        public async Task<List<XboxThread>> GetThreadDump(bool requireNames = true) {
            this.connection.EnsureNotClosed();
            using BusyToken token = this.connection.CreateBusyToken();

            return await Task.Run(() => {
                List<XboxThread> threads = new List<XboxThread>();
                foreach (IXboxThread thread in this.connection.console.DebugTarget.Threads) {
                    threads.Add(XBThreadFromCOM(thread));
                }

                return threads;
            });
        }

        public Task<FreezeResult> DebugFreeze() {
            return this.connection.DebugFreeze();
        }

        public Task<UnFreezeResult> DebugUnFreeze() {
            return this.connection.DebugUnFreeze();
        }

        public async Task<bool?> IsFrozen() {
            XboxExecutionState state = await this.GetExecutionState();
            return state == XboxExecutionState.Stop;
        }

        public async Task DeleteFile(string path) {
            this.connection.EnsureNotClosed();
            using BusyToken token = this.connection.CreateBusyToken();

            await Task.Run(() => {
                this.connection.console.DeleteFile(path);
            });
        }

        public async Task LaunchFile(string path) {
            this.connection.EnsureNotClosed();
            using BusyToken token = this.connection.CreateBusyToken();
            await Task.Run(() => {
                string[] lines = path.Split('\\');
                StringBuilder dirSb = new StringBuilder();
                for (int i = 0; i < lines.Length - 1; i++)
                    dirSb.Append(lines[i]).Append('\\');
                
                this.connection.console.Reboot(path, dirSb.ToString(), "", XboxRebootFlags.Title);
            });
        }

        public Task<List<MemoryRegion>> GetMemoryRegions(bool willRead, bool willWrite) {
            return this.connection.GetMemoryRegions(willRead, willWrite);
        }

        public Task<XboxExecutionState> GetExecutionState() {
            this.connection.EnsureNotClosed();
            using BusyToken token = this.connection.CreateBusyToken();
            return Task.FromResult(XboxExecutionState.Unknown);
        }

        // public IDisposable SubscribeToEvents(ConsoleSystemEventHandler handler) {
        //     Dispos
        //     
        //     return this.connection.SubscribeToEvents(handler);
        // }

        // private class UnsubscribeDisposable : IDisposable {
        //     private volatile XbdmFeaturesImpl? featureImpl;
        //     private readonly XboxEvents_OnStdNotifyEventHandler handler;
        //
        //     public UnsubscribeDisposable(XbdmFeaturesImpl featureImpl, ConsoleSystemEventHandler theHandler) {
        //         this.featureImpl = featureImpl;
        //         this.handler = (code, info) => {
        //             theHandler()
        //         }
        //     }
        //
        //     public void Dispose() {
        //         XbdmFeaturesImpl? features = Interlocked.Exchange(ref this.featureImpl, null);
        //         if (features != null) {
        //             features.connection.console.remove_OnStdNotify(this.handler);
        //         }
        //     }
        // }
    }
}