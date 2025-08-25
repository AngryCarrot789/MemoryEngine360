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

using MemEngine360.Connections;
using MemEngine360.Connections.Features;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.Engine.FileBrowsing;

public class FileTreeExplorer {
#if DEBUG
    public static FileTreeExplorer DummyInstance_UITest { get; } = new FileTreeExplorer(new MemoryEngine());
#endif

    /// <summary>
    /// Gets the folder that stores this ATM's layer hierarchy
    /// </summary>
    public FileTreeNodeDirectory RootEntry { get; }

    /// <summary>
    /// Gets the memory engine associated with this address table manager
    /// </summary>
    public MemoryEngine MemoryEngine { get; }
    
    private readonly AsyncRelayCommand refreshRootCommand;

    public FileTreeExplorer(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine;
        this.RootEntry = FileTreeNodeDirectory.InternalCreateRoot(this);

        this.MemoryEngine.ConnectionChanged += this.OnConnectionChanged;
        this.refreshRootCommand = new AsyncRelayCommand(this.RefreshRoot);
    }

    private void OnConnectionChanged(MemoryEngine sender, ulong frame, IConsoleConnection? oldconnection, IConsoleConnection? newconnection, ConnectionChangeCause cause) {
        this.refreshRootCommand.Execute(null);
    }

    public async Task RefreshRoot() {
        this.RootEntry.Clear();
        await this.MemoryEngine.BeginBusyOperationActivityAsync(async (t, connection) => {
            if (connection.TryGetFeature(out IFeatureFileSystemInfo? fs)) {
                foreach (string root in await fs.GetRoots()) {
                    this.RootEntry.Items.Add(new FileTreeNodeDirectory() {
                        FileName = root + ":"
                    });
                }
            }
        });
    }

    static FileTreeExplorer() {
#if DEBUG
        DummyInstance_UITest.RootEntry.Items.Add(new FileTreeNodeDirectory() {
            FileName = "C:\\",
            Items = {
                new FileTreeNodeDirectory() {
                    FileName = "Users",
                    Items = {
                        new FileTreeNodeDirectory() {
                            FileName = "SpookyUser1",
                            Items = {
                                new FileTreeNodeDirectory() {
                                    FileName = "Desktop",
                                    Items = {
                                        new FileTreeNodeFile() { FileName = "File 1.txt" },
                                        new FileTreeNodeFile() { FileName = "File 2.txt" },
                                        new FileTreeNodeFile() { FileName = "File 3.txt" }
                                    }
                                }
                            }
                        },
                        new FileTreeNodeDirectory() {
                            FileName = "An empty thing",
                        }
                    }
                }
            }
        });

        DummyInstance_UITest.RootEntry.Items.Add(new FileTreeNodeDirectory() {
            FileName = "D:\\",
            Items = {
                new FileTreeNodeDirectory() {
                    FileName = "OK",
                    Items = {
                        new FileTreeNodeFile() { FileName = "File 1.txt" },
                        new FileTreeNodeFile() { FileName = "File 2.txt" },
                        new FileTreeNodeFile() { FileName = "File 3.txt" }
                    }
                }
            }
        });
#endif
    }

    public async Task LoadContentsCommand(FileTreeNodeDirectory directory, bool isFromFileNotFound = false) {
        if (directory.ParentDirectory == null) {
            return;
        }

        directory.HasLoadedContents = false;
        directory.Items.Clear();
        await this.MemoryEngine.BeginBusyOperationActivityAsync(async (t, c) => {
            if (directory.ParentDirectory == null)
                return;
            
            if (!c.TryGetFeature(out IFeatureFileSystemInfo? info)) {
                directory.HasLoadedContents = true;
                return;
            }

            FileTreeNodeDirectory parent = directory.ParentDirectory!;
            (EnumFileSystemListResult, List<FileSystemEntry>?) result;
            try {
                result = await info.GetFileSystemEntries(directory.FullPath);
            }
            catch (Exception e) {
                await IMessageDialogService.Instance.ShowMessage("Error", "Error reading file system entries", e.Message);
                return;
            }

            switch (result.Item1) {
                case EnumFileSystemListResult.Success: {
                    foreach (FileSystemEntry entry in result.Item2!) {
                        directory.Items.Add(entry.IsDirectory
                            ? new FileTreeNodeDirectory() {
                                FileName = entry.Name,
                                CreationTimeUtc = entry.CreatedTime,
                                ModifiedTimeUtc = entry.ModifiedTime,
                                Size = entry.Size,
                            }
                            : new FileTreeNodeFile() {
                                FileName = entry.Name,
                                CreationTimeUtc = entry.CreatedTime,
                                ModifiedTimeUtc = entry.ModifiedTime,
                                Size = entry.Size,
                            });
                    }

                    break;
                }
                case EnumFileSystemListResult.AccessDenied: directory.ErrorText = "Access Denied"; break;
                    // if (!isFromFileNotFound)
                    //     await IMessageDialogService.Instance.ShowMessage("Access denied", "Access is denied to " + directory.FullPath); break;
                case EnumFileSystemListResult.NoSuchDirectory: 
                    if (!isFromFileNotFound)
                        await IMessageDialogService.Instance.ShowMessage("No such directory", "This folder no longer exists " + directory.FullPath);
                    await this.LoadContentsCommand(parent, isFromFileNotFound: true);
                    break;
                default:                                       break;
            }
        }, "Load directory");
    }
}