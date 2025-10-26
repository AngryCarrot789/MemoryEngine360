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
using Lua;
using PFXToolKitUI.Utils;

namespace MemEngine360.ModTools.Gui;

public delegate void MTButtonEventHandler(MTButton sender);

public sealed class MTButton : BaseMTElement {
    private string text = "";
    private LuaFunction? pressFunction, holdFunction;

    public string Text {
        get => this.text;
        set => PropertyHelper.SetAndRaiseINE(ref this.text, value, this, static t => t.TextChanged?.Invoke(t));
    }

    public LuaFunction? PressFunction {
        get => this.pressFunction;
        set => PropertyHelper.SetAndRaiseINE(ref this.pressFunction, value, this, static t => t.PressFunctionChanged?.Invoke(t));
    }

    public LuaFunction? HoldFunction {
        get => this.holdFunction;
        set => PropertyHelper.SetAndRaiseINE(ref this.holdFunction, value, this, static t => t.HoldFunctionChanged?.Invoke(t));
    }

    public event MTButtonEventHandler? TextChanged;
    public event MTButtonEventHandler? PressFunctionChanged, HoldFunctionChanged;

    private bool isPressed = false;
    private TaskCompletionSource? tcsReleased;
    private LuaModToolMachine? pressedMachine;

    public MTButton() {
    }

    public void OnPressed() {
        if (this.isPressed) {
            this.OnReleased();
        }

        this.isPressed = true;
        TaskCompletionSource tcs = this.tcsReleased = new TaskCompletionSource();
        LuaModToolMachine? machine = this.pressedMachine = this.GUI?.ModTool.Machine;
        machine?.PostMessage(async (ctx, ct) => {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            
            double lastTime = stopwatch.Elapsed.TotalSeconds;
            while (this.HoldFunction is { } holdFunc && !tcs.Task.IsCompleted) {
                double elapsed = stopwatch.Elapsed.TotalSeconds;
                double delta = elapsed - lastTime;
                lastTime = elapsed;
                
                LuaValue[] result = await holdFunc.InvokeAsync(ctx.State, new LuaValue[1] {new LuaValue(delta)}, ct);
                if (result.Length > 0 && result[0].TryRead(out bool s) && s) {
                    break;
                }

                await machine.TryRunMessages(ctx, ct);
            }
        });
    }

    public void OnReleased() {
        if (!this.isPressed) {
            return;
        }

        this.isPressed = false;
        this.tcsReleased!.SetResult();
        this.tcsReleased = null;
        this.pressedMachine?.PostMessage(async (ctx, ct) => {
            if (this.PressFunction != null) {
                await this.PressFunction.InvokeAsync(ctx.State, new LuaValue[0], ct);
            }
        });
    }

    protected override void OnGUIChanged(ModToolGUI? oldGui, ModToolGUI? newGui) {
        base.OnGUIChanged(oldGui, newGui);
        this.OnReleased();
    }
}