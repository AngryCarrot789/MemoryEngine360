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

using PFXToolKitUI.CommandSystem;

namespace MemEngine360.Engine.Debugging.Commands;

public class RefreshAllCommand : BaseDebuggerCommand {
    protected override async Task ExecuteCommandAsync(ConsoleDebugger debugger, CommandEventArgs e) {
        await debugger.UpdateAllThreads(CancellationToken.None);

        // ThreadEntry? active = debugger.ActiveThread;
        // if (active == null) {
        //     return;
        // }
        //
        // IStackWalker service = ApplicationPFX.Instance.ServiceManager.GetService<IStackWalker>();
        // ThreadContext ctx = new ThreadContext();
        // List<RegisterEntry>? result = await ((IHaveXboxDebugFeatures) debugger.Connection!).GetRegisters(active.ThreadId);
        // if (result == null) {
        //     return;
        // }
        //
        // foreach (RegisterEntry entry in result) {
        //     switch (entry.Name.ToUpperInvariant()) {
        //         case "MSR":   ctx.MSR = ((RegisterEntry32) entry).Value; break;
        //         case "IAR":   ctx.IAR = ((RegisterEntry32) entry).Value; break;
        //         case "LR":    ctx.LR = ((RegisterEntry32) entry).Value; break;
        //         case "CTR":   ctx.CTR = ((RegisterEntry64) entry).Value; break;
        //         case "GPR0":  ctx.GPR0 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR1":  ctx.GPR1 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR2":  ctx.GPR2 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR3":  ctx.GPR3 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR4":  ctx.GPR4 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR5":  ctx.GPR5 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR6":  ctx.GPR6 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR7":  ctx.GPR7 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR8":  ctx.GPR8 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR9":  ctx.GPR9 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR10": ctx.GPR10 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR11": ctx.GPR11 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR12": ctx.GPR12 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR13": ctx.GPR13 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR14": ctx.GPR14 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR15": ctx.GPR15 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR16": ctx.GPR16 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR17": ctx.GPR17 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR18": ctx.GPR18 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR19": ctx.GPR19 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR20": ctx.GPR20 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR21": ctx.GPR21 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR22": ctx.GPR22 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR23": ctx.GPR23 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR24": ctx.GPR24 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR25": ctx.GPR25 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR26": ctx.GPR26 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR27": ctx.GPR27 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR28": ctx.GPR28 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR29": ctx.GPR29 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR30": ctx.GPR30 = ((RegisterEntry64) entry).Value; break;
        //         case "GPR31": ctx.GPR31 = ((RegisterEntry64) entry).Value; break;
        //         case "CR":    ctx.CR = ((RegisterEntry32) entry).Value; break;
        //         case "XER":   ctx.XER = ((RegisterEntry32) entry).Value; break;
        //         case "FPSCR": ctx.FPSCR = ((RegisterEntry64) entry).Value; break;
        //         case "FPR0":  ctx.FPR0 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR1":  ctx.FPR1 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR2":  ctx.FPR2 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR3":  ctx.FPR3 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR4":  ctx.FPR4 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR5":  ctx.FPR5 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR6":  ctx.FPR6 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR7":  ctx.FPR7 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR8":  ctx.FPR8 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR9":  ctx.FPR9 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR10": ctx.FPR10 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR11": ctx.FPR11 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR12": ctx.FPR12 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR13": ctx.FPR13 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR14": ctx.FPR14 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR15": ctx.FPR15 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR16": ctx.FPR16 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR17": ctx.FPR17 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR18": ctx.FPR18 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR19": ctx.FPR19 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR20": ctx.FPR20 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR21": ctx.FPR21 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR22": ctx.FPR22 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR23": ctx.FPR23 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR24": ctx.FPR24 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR25": ctx.FPR25 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR26": ctx.FPR26 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR27": ctx.FPR27 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR28": ctx.FPR28 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR29": ctx.FPR29 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR30": ctx.FPR30 = ((RegisterEntry64) entry).Value; break;
        //         case "FPR31": ctx.FPR31 = ((RegisterEntry64) entry).Value; break;
        //     }
        // }
        //
        // service.Walk(debugger.Connection!, ctx);
    }
}