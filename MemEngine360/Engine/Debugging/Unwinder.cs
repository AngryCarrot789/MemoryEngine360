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

using System.Buffers.Binary;
using MemEngine360.Connections;
using MemEngine360.Connections.Features;

namespace MemEngine360.Engine.Debugging;

// Some AI thing with some of my own fixes, I don't know enough about PowerPC to do this all myself.
// Somewhat works, but the 2nd and last call stack entries appear to be the same
// except that the return address is 0 on the last, though that could just be because
// the xboxkrnl calls a method that calls itself...?

// Not using this until it actually works

public static class Unwinder {
    // number of bytes to read from the function prolog to search for saves
    private const int PrologMaxBytes = 256;

    public static async Task<(FrameContext frame, Ctx nextCtx)?> UnwindFrame(IConsoleConnection connection, IFeatureXboxDebugging debug, Ctx ctx) {
        // find function that contains current PC
        FunctionCallEntry?[] functions = await debug.FindFunctions(new[] { ctx.IAR });
        FunctionCallEntry? functionEntry = functions[0];
        if (functionEntry == null)
            return null;

        // read a small prolog window (bounded)
        int toRead = (int) Math.Min(functionEntry.Size, PrologMaxBytes);
        byte[] code = await connection.ReadBytes(functionEntry.Address, toRead);

        // parse prolog to get list of saved regs + frameSize + lrOffset
        List<SavedReg> saves = ParseProlog(code, out int frameSize, out int lrOffset);

        // current SP (r1) is in ctx.Sp (this is the SP after stwu)
        uint sp = ctx.GPR1;
        uint callerSp = sp + (uint) frameSize;

        // where LR was stored; if lrOffset unknown, fallback to top-of-frame - 8
        uint savedLrAddr = (lrOffset != int.MinValue) ? sp + (uint) lrOffset : sp + (uint) frameSize - 8;

        // compute bytes to read from stack (conservative)
        int maxOffset = saves.Count == 0 ? 0 : saves.Max(s => s.Offset);
        int bytesNeeded = Math.Max(frameSize + 8, maxOffset + 16);
        byte[] stackBytes = await connection.ReadBytes(sp, bytesNeeded);

        Dictionary<string, ulong> registers = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        foreach (SavedReg s in saves) {
            int off = s.Offset;
            if (s.Kind == RegKind.GPR) {
                if (off + 4 <= stackBytes.Length) {
                    uint val = BinaryPrimitives.ReadUInt32BigEndian(stackBytes.AsSpan(off, 4));
                    registers[$"r{s.Index}"] = val;
                }
            }
            else if (s.Kind == RegKind.FPR) {
                if (off + 8 <= stackBytes.Length) {
                    ulong val = BinaryPrimitives.ReadUInt64BigEndian(stackBytes.AsSpan(off, 8));
                    registers[$"f{s.Index}"] = val;
                }
            }
            else if (s.Kind == RegKind.LR) {
                if (off + 4 <= stackBytes.Length) {
                    uint val = BinaryPrimitives.ReadUInt32BigEndian(stackBytes.AsSpan(off, 4));
                    registers["lr"] = val;
                }
            }
        }

        // determine caller PC (return address)
        uint callerReturnAddress = 0;
        if (registers.TryGetValue("lr", out ulong lrVal))
            callerReturnAddress = (uint) lrVal;
        else if (savedLrAddr > 0) {
            // try reading it from memory at savedLrAddr
            try {
                byte[] lrBytes = await connection.ReadBytes(savedLrAddr, 4);
                callerReturnAddress = BinaryPrimitives.ReadUInt32BigEndian(lrBytes);
            }
            catch {
                // ignore read failure; we'll fallback to ctx.Lr
            }
        }

        if (callerReturnAddress == 0)
            callerReturnAddress = ctx.LR; // final fallback: use current LR from context

        // construct FrameContext for this caller
        FrameContext frame = new FrameContext {
            ReturnAddress = callerReturnAddress,
            StackPointer = callerSp,
            RestoredRegisters = registers,
            Function = functionEntry,
        };

        // update next RegContext by applying restored registers and advancing PC/SP
        Ctx nextCtx = new Ctx {
            IAR = callerReturnAddress,
            GPR1 = callerSp,
            LR = ctx.LR // will overwrite if restored contains lr
        };

        // copy known GPRs/FPRs from current ctx to next (so we keep values not overwritten)
        foreach (KeyValuePair<int, ulong> kv in ctx.GPR)
            nextCtx.GPR[kv.Key] = kv.Value;
        foreach (KeyValuePair<int, ulong> kv in ctx.FPR)
            nextCtx.FPR[kv.Key] = kv.Value;

        // apply saved registers (they are caller's register values)
        foreach (KeyValuePair<string, ulong> kv in registers) {
            string name = kv.Key.ToLowerInvariant();
            if (name.StartsWith("r")) {
                if (int.TryParse(name.Substring(1), out int idx))
                    nextCtx.GPR[idx] = kv.Value;
            }
            else if (name.StartsWith("f")) {
                if (int.TryParse(name.Substring(1), out int idx))
                    nextCtx.FPR[idx] = kv.Value;
            }
            else if (name == "lr") {
                nextCtx.LR = (uint) kv.Value;
            }
        }

        return (frame, nextCtx);
    }

    /// High-level stack walker: read registers once, then iterate.
    public static async Task<List<FrameContext>> UnwindFullStack(IConsoleConnection conn, uint threadId, int maxDepth = PrologMaxBytes) {
        List<FrameContext> frames = new List<FrameContext>();
        IFeatureXboxDebugging debug = conn.GetFeatureOrDefault<IFeatureXboxDebugging>()!;

        // read initial registers ONCE
        RegisterContext? registers = await debug.GetThreadRegisters(threadId);
        if (registers == null)
            return frames;
        if (!registers.TryGetUInt64("gpr1", out ulong stackPointer))
            return frames;
        if (!registers.TryGetUInt32("IAR", out uint iar) || !registers.TryGetUInt32("lr", out uint lr))
            return frames;
        
        Ctx ctx = new Ctx {
            IAR = iar,
            GPR1 = (uint) (stackPointer & uint.MaxValue),
            LR = lr
        };

        HashSet<ulong> seen = new HashSet<ulong>(); // track (iar,sp) pairs to avoid loops
        uint lastSp = 0;

        for (int depth = 0; depth < maxDepth; depth++) {
            // detect cycles
            ulong key = ((ulong) ctx.IAR << 32) | ctx.GPR1;
            if (!seen.Add(key)) {
                break;
            }

            // try unwinding one frame using current context
            (FrameContext frame, Ctx nextCtx)? result = await UnwindFrame(conn, debug, ctx);
            if (!result.HasValue) {
                break;
            }

            FrameContext frame = result.Value.frame;
            Ctx nextCtx = result.Value.nextCtx;
            frames.Add(frame);

            // termination checks
            if (frame.ReturnAddress == 0)
                break;
            if (frames.Count > 1 && frame.StackPointer == lastSp) {
                // SP didn't advance — allow once, but break if repeats
                // (some leaf/frame-less chains keep SP unchanged; use cycle detection to avoid infinite loops)
                bool isProgressing = !seen.Contains(((ulong) frame.ReturnAddress << 32) | frame.StackPointer);
                if (!isProgressing)
                    break;
            }

            lastSp = frame.StackPointer;
            ctx = nextCtx; // advance context for next iteration
        }

        return frames;
    }

    // Parse prolog bytes and extract saves.
    // Returns a list of SavedReg, frameSize (positive), and lrOffset (offset within frame where LR saved; int.MinValue if unknown)
    public static List<SavedReg> ParseProlog(byte[] code, out int frameSize, out int lrOffset) {
        List<SavedReg> res = new List<SavedReg>();
        frameSize = 0;
        lrOffset = int.MinValue;

        // read words big-endian
        int words = code.Length / 4;
        for (int i = 0; i < words; i++) {
            uint word = BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(code, i * 4, 4));

            // detect special instructions by exact constant (mflr/mtlr)
            if (word == 0x7C0802A6 /* mflr r0 */) {
                // record that next stw r0,offs(r1) will be LR save
                // we'll detect the stw and set lrOffset accordingly
                continue;
            }

            if (word == 0x7C0803A6 /* mtlr r0 (epilog) */) {
                // end-of-frame marker often occurs later; we can stop if we want, but continue scanning prolog window
                continue;
            }

            // top byte is indicative of instruction family in practice for powerpc BE
            byte top = (byte) (word >> 24);

            // instructions that use the common D-form: opcode rt, immediate(ra)
            // decode fields common to D-form: rt = bits 6-10; ra = bits 11-15; simm = low16
            int rt = (int) ((word >> 21) & 0x1F);
            int ra = (int) ((word >> 16) & 0x1F);
            short simm = (short) (word & 0xFFFF);
            int imm = simm; // sign-extended

            // match stwu: observed top byte 0x94
            if (top == 0x94) {
                // stwu r1, imm(r1) : frame adjust
                if (rt == 1 && ra == 1) {
                    frameSize = -imm; // because imm is negative in typical stwu r1,-framesize(r1)
                    if (frameSize < 0)
                        frameSize = -frameSize; // safeguard
                    // continue scanning for saves after frame allocation
                }

                continue;
            }

            // match stw: top 0x90 (store word)
            if (top == 0x90) {
                // stw rt, imm(ra)
                // if ra == 1 (r1), it's storing to stack
                if (ra == 1) {
                    int offset = imm >= 0 ? imm : frameSize + imm;
                    // however typical: imm is signed and addresses are r1 + imm (r1 is after allocation)
                    // To be conservative, compute offset relative to r1: if imm negative, use (frameSize + imm) as positive index
                    if (imm < 0 && frameSize > 0)
                        offset = frameSize + imm;
                    // Special-case saving LR (rt == 0 is usually used after mflr r0)
                    if (rt == 0) {
                        lrOffset = offset;
                        res.Add(new SavedReg(RegKind.LR, 0, offset));
                    }
                    else {
                        res.Add(new SavedReg(RegKind.GPR, rt, offset));
                    }
                }

                continue;
            }

            // match std (store doubleword). Observed top bytes for std-like stores include 0xFB and 0xF8; use 0xF8..0xFF range for std family
            if (top >= 0xF8) {
                // For FPR/GPR double stores we treat as FPR or GPR based on mnemonic family:
                // observed: 0xFB / 0xF9 / 0xFA for stfd/std variants
                // We'll treat them as FPR saves if opcode is a floating-store (most likely 0xF9/0xFA/0xFB in your images)
                if (ra == 1) {
                    int offset = imm >= 0 ? imm : (frameSize + imm);
                    // Use common pattern: FPR indices are in rt field for stfd (but naming differs)
                    res.Add(new SavedReg(RegKind.FPR, rt, offset));
                }

                continue;
            }

            // match floating loads/stores restore patterns (epilog area) - we still capture them if in first window
            if (top >= 0xE8 && top <= 0xEF) {
                // lfd, lfs etc. treat as FPR loads; if ra==1, they restore from stack - capture offset for FPR
                if (ra == 1) {
                    int offset = imm >= 0 ? imm : (frameSize + imm);
                    res.Add(new SavedReg(RegKind.FPR, rt, offset));
                }

                continue;
            }

            // match lwz (load word) family: top 0x80
            if (top == 0x80) {
                if (ra == 1) {
                    int offset = imm >= 0 ? imm : frameSize + imm;
                    res.Add(new SavedReg(RegKind.GPR, rt, offset));
                }

                continue;
            }

            // mflr but not exactly encoded? handle other special patterns if necessary
        }

        // Normalise offsets: many of our heuristics produced offsets that assume r1 is current (after stwu).
        // If frameSize known and some offsets are negative, convert them to positive offsets relative to r1.
        for (int i = 0; i < res.Count; i++) {
            SavedReg s = res[i];
            if (s.Offset < 0 && frameSize > 0) {
                int normalized = frameSize + s.Offset;
                res[i] = new SavedReg(s.Kind, s.Index, normalized);
            }
        }

        // deduplicate by (Kind,Index) keeping the first discovered offset
        List<SavedReg> unique = res.GroupBy(x => (x.Kind, x.Index)).Select(g => g.First()).ToList();
        return unique;
    }
}

public enum RegKind { GPR, FPR, LR }

public record SavedReg(RegKind Kind, int Index, int Offset);

public class FrameContext {
    public uint ReturnAddress { get; init; }
    public uint StackPointer { get; init; }
    public Dictionary<string, ulong> RestoredRegisters { get; init; } = new();
    public FunctionCallEntry? Function { get; set; }
}

public class Ctx {
    /// <summary>Current instruction's address</summary>
    public uint IAR;
    /// <summary>Stack pointer (after prolog)</summary>
    public uint GPR1;
    /// <summary>Link register aka return address for current call </summary>
    public uint LR;
    /// <summary>General purpose register values</summary>
    public readonly Dictionary<int, ulong> GPR = new Dictionary<int, ulong>();
    /// <summary>Floating point registers</summary>
    public readonly Dictionary<int, ulong> FPR = new Dictionary<int, ulong>();
}