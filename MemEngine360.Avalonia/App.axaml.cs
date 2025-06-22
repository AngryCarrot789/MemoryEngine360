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

using System;
using System.IO;
using Avalonia;
using Avalonia.Markup.Xaml;
using PFXToolKitUI;
using PFXToolKitUI.Avalonia;

namespace MemEngine360.Avalonia;

public partial class App : Application {
    private IApplicationStartupProgress progress;

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
        
        AvUtils.OnApplicationInitialised();

        ApplicationPFX.InitializeInstance(new MemoryEngineApplication(this));
        
        // if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop1) {
        //     desktop1.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        //     SplashScreenWindow splashScreen = new SplashScreenWindow();
        //     this.progress = splashScreen;
        //     desktop1.MainWindow = splashScreen;
        //     splashScreen.Show();
        // }
        // else {
        //     this.progress = new EmptyApplicationStartupProgress();
        // }

        // Splash Screen only flashes, so long startups are beyond our control here
        this.progress = new EmptyApplicationStartupProgress();
    }

    public override async void OnFrameworkInitializationCompleted() {
        base.OnFrameworkInitializationCompleted();
        AvUtils.OnFrameworkInitialised();

        string[] envArgs = Environment.GetCommandLineArgs();
        if (envArgs.Length > 0 && Path.GetDirectoryName(envArgs[0]) is string dir && dir.Length > 0) {
            Directory.SetCurrentDirectory(dir);
        }

        await ApplicationPFX.InitializeApplication(this.progress, envArgs);
    }
}