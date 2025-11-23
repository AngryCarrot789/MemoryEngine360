// 
// Copyright (c) 2025-2025 REghZy
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

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace MemEngine360.BaseFrontEnd.Scripting;

/// <summary>
/// An extension to <see cref="TextEditor"/> that implements behaviour similar to JetBrains' IDE
/// </summary>
public class LuaTextEditor : TextEditor {
    protected override Type StyleKeyOverride => typeof(TextEditor);

    public LuaTextEditor() : base(new LuaTextArea(), new TextDocument()) {
        this.Options.CompletionAcceptAction = CompletionAcceptAction.DoubleTapped;
        this.Options.HighlightCurrentLine = true;
        this.ShowLineNumbers = true;
    }
}

// TODO: implement code completion
public class LuaTextArea : TextArea {
    protected override Type StyleKeyOverride => typeof(TextArea);

    private OverloadInsightWindow? myInsightWindow;

    private CompletionWindow TheCompletionWindow => field ??= new CompletionWindow(this);
    
    public static readonly ICompletionData[] GlobalTablesCompletion = {
        new MyCompletionData("engine"),
        new MyCompletionData("jrpc"),
        new MyCompletionData("debugger"),
    };

    public LuaTextArea() : base() {
        this.RightClickMovesCaret = true;
        this.TextEntered += this.OnTextEntered;
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e) {
        // TextEditorEx textEditor = this.TextView.GetService<TextEditorEx>() ?? throw new Exception();
        if (e.Text == ".") {
            this.TheCompletionWindow.Show();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);

        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.Control) {
            this.TheCompletionWindow.Show();
        }
    }

    public class MyCompletionData : ICompletionData {
        private Control? _contentControl;

        public IImage Image => null;

        public string Text { get; }

        // Use this property if you want to show a fancy UIElement in the list.
        public object Content => this._contentControl ??= this.BuildContentControl();

        public object Description => "Description for " + this.Text;

        public double Priority { get; } = 0;

        public MyCompletionData(string text) {
            this.Text = text;
        }
        
        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) {
            textArea.Document.Replace(completionSegment, this.Text);
        }

        private Control BuildContentControl() {
            TextBlock textBlock = new TextBlock();
            textBlock.Text = this.Text;
            textBlock.Margin = new Thickness(5,3);

            return textBlock;
        }
    }
}