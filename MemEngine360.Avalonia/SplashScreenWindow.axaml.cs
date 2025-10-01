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

using System.Threading.Tasks;
using Avalonia.Controls;
using PFXToolKitUI;
using PFXToolKitUI.Activities;
using PFXToolKitUI.Logging;

namespace MemEngine360.Avalonia;

public partial class SplashScreenWindow : Window, IApplicationStartupProgress {
    private volatile string? myActionText;

    public string? ActionText {
        get => this.myActionText;
        set {
            if (this.myActionText == value)
                return;

            this.myActionText = value;
            this.PART_ActivityTextBlock.Text = value;
        }
    }

    public CompletionState CompletionState { get; }

    public SplashScreenWindow() {
        this.InitializeComponent();
        this.CompletionState = new ConcurrentCompletionState(DispatchPriority.Normal);
        this.CompletionState.CompletionValueChanged += this.CompletionStateOnCompletionValueChanged;
    }

    private void CompletionStateOnCompletionValueChanged(CompletionState state) {
        this.PART_ProgressBar.Value = this.CompletionState.TotalCompletion;
    }

    public Task ProgressAndWaitForRender(string? action, double? newProgress) {
        if (action != null)
            this.ActionText = action;
        if (newProgress.HasValue)
            this.CompletionState.SetProgress(newProgress.Value);

        if (!string.IsNullOrWhiteSpace(action)) {
            double total = this.CompletionState.TotalCompletion;
            string comp = (total * 100.0).ToString("F1").PadRight(5, '0');
            AppLogger.Instance.WriteLine($"[{comp}%] {action}");
        }

        return this.WaitForRender();
    }

    public Task WaitForRender() => ApplicationPFX.Instance.Dispatcher.Process(DispatchPriority.Loaded);
}