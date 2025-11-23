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

using Lua;
using PFXToolKitUI.Utils.Events;

namespace MemEngine360.ModTools.Gui;

public sealed class MTTextBox : BaseMTElement {
    public string? LeftContent {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.LeftContentChanged);
    }

    public string? RightContent {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.RightContentChanged);
    }

    /// <summary>
    /// Gets the content
    /// </summary>
    public object? Content { get; private set; }

    /// <summary>
    /// Gets the type of content
    /// </summary>
    public EnumContentType ContentType { get; private set; }

    /// <summary>
    /// Gets or sets the function that runs when the <see cref="Content"/> changes
    /// </summary>
    public LuaFunction? ValueChangeFunction {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.ValueChangeFunctionChanged);
    }

    /// <summary>
    /// Gets or sets the function that runs when the user presses or releases a key
    /// </summary>
    public LuaFunction? KeyPressFunction {
        get => field;
        set => PropertyHelper.SetAndRaiseINE(ref field, value, this, this.KeyPressFunctionChanged);
    }

    public event EventHandler? LeftContentChanged, RightContentChanged;
    public event EventHandler? ContentChanged;
    public event EventHandler? ValueChangeFunctionChanged;
    public event EventHandler? KeyPressFunctionChanged;

    public MTTextBox() {
    }

    /// <summary>
    /// Gets the lua value of our content
    /// </summary>
    public LuaValue GetLuaValue() {
        switch (this.ContentType) {
            case EnumContentType.Text:   return new LuaValue((string?) this.Content ?? "");
            case EnumContentType.UInt32: return new LuaValue((uint) (this.Content ?? throw new Exception("Misuse of " + nameof(this.UnsafeSetContent))));
            case EnumContentType.Number: return new LuaValue((double) (this.Content ?? throw new Exception("Misuse of " + nameof(this.UnsafeSetContent))));
            default:                     throw new ArgumentOutOfRangeException();
        }
    }

    public string GetReadableText() {
        switch (this.ContentType) {
            case EnumContentType.Text:   return (string?) this.Content ?? "";
            case EnumContentType.UInt32: return ((uint) (this.Content ?? throw new Exception("Misuse of " + nameof(this.UnsafeSetContent)))).ToString("X8");
            case EnumContentType.Number: return ((double) (this.Content ?? throw new Exception("Misuse of " + nameof(this.UnsafeSetContent)))).ToString();
            default:                     throw new ArgumentOutOfRangeException();
        }
    }

    public void SetText(string? text) => this.UnsafeSetContent(text, EnumContentType.Text);

    public void SetUInt32(uint value) => this.UnsafeSetContent(value, EnumContentType.UInt32);

    public void SetNumber(double number) => this.UnsafeSetContent(number, EnumContentType.Number);

    public void SetDefaultContent(EnumContentType type) {
        switch (type) {
            case EnumContentType.Text:   this.SetText(""); break;
            case EnumContentType.UInt32: this.SetUInt32(0); break;
            case EnumContentType.Number: this.SetNumber(0); break;
            default:                     throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    public void UnsafeSetContent(object? content, EnumContentType contentType) {
        if (this.ContentType == contentType && Equals(this.Content, content)) {
            return; // same content and type, so do nothing
        }
        
        this.Content = content;
        this.ContentType = contentType;
        this.ContentChanged?.Invoke(this, EventArgs.Empty);

        LuaFunction? function = this.ValueChangeFunction;
        if (function != null) {
            this.GUI?.ModTool.Machine?.PostMessage(async (ctx, ct) => {
                _ = await function.InvokeAsync(ctx.State, [this.ownerTable!], ct);
            });
        }
    }

    public void OnKeyPress(bool isPress, string text, int keyCode) {
        LuaFunction? function = this.KeyPressFunction;
        if (function != null) {
            this.GUI?.ModTool.Machine?.PostMessage(async (ctx, ct) => {
                _ = await function.InvokeAsync(ctx.State, [this.ownerTable!, isPress, text, keyCode], ct);
            });
        }
    }

    public enum EnumContentType {
        Text,
        UInt32,
        Number
    }
}