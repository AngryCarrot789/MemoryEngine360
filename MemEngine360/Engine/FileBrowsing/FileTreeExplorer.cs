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
using PFXToolKitUI.Composition;
using PFXToolKitUI.Interactivity.Contexts;
using PFXToolKitUI.Services.Messaging;
using PFXToolKitUI.Utils.Commands;

namespace MemEngine360.Engine.FileBrowsing;

public class FileTreeExplorer : IComponentManager {
    public static readonly DataKey<FileTreeExplorer> DataKey = DataKeys.Create<FileTreeExplorer>(nameof(FileTreeExplorer));

#if DEBUG
    public static FileTreeExplorer DummyInstance_UITest { get; } = new FileTreeExplorer(new MemoryEngine());
#endif

    private readonly ComponentStorage myComponentStorage;

    /// <summary>
    /// Gets the folder that stores this ATM's layer hierarchy
    /// </summary>
    public FileTreeNodeDirectory RootEntry { get; }

    /// <summary>
    /// Gets the memory engine associated with this address table manager
    /// </summary>
    public MemoryEngine MemoryEngine { get; }

    ComponentStorage IComponentManager.ComponentStorage => this.myComponentStorage;

    private readonly AsyncRelayCommand refreshRootCommand;

    public FileTreeExplorer(MemoryEngine memoryEngine) {
        this.MemoryEngine = memoryEngine ?? throw new ArgumentNullException(nameof(memoryEngine));
        this.myComponentStorage = new ComponentStorage(this);
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
                foreach (DriveEntry root in await fs.GetDriveList()) {
                    this.RootEntry.Items.Add(new FileTreeNodeDirectory() {
                        FileName = root.Name,
                        Size = root.TotalSize
                    });
                }
            }
        });
    }

    static FileTreeExplorer() {
#if DEBUG
        DummyInstance_UITest.RootEntry.Items.Add(new FileTreeNodeDirectory() {
            FileName = "C:",
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
            FileName = "D:",
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

        IFeatureFileSystemInfo? fsInfo = null;

        ConnectionAction action = new ConnectionAction(IConnectionLockPair.Lambda(this.MemoryEngine, e => e.BusyLocker, e => e.Connection)) {
            ActivityCaption = "Load directory",
            Setup = async (action, connection, hasConnectionChanged) => {
                if (directory.ParentDirectory == null) {
                    return false;
                }

                if (!connection.TryGetFeature(out fsInfo)) {
                    directory.HasLoadedContents = true;
                    return false;
                }

                return true;
            },
            Execute = (action, connection) => LoadContentsInternal(directory, fsInfo!, isFromFileNotFound)
        };

        await action.RunAsync();
    }

    private static async Task LoadContentsInternal(FileTreeNodeDirectory directory, IFeatureFileSystemInfo fsInfo, bool isFromFileNotFound = false) {
        if (directory.ParentDirectory == null) {
            return;
        }

        FileTreeNodeDirectory parent = directory.ParentDirectory!;
        List<FileSystemEntry> result;
        try {
            result = await fsInfo.GetFileSystemEntries(directory.FullPath);
        }
        catch (FileSystemAccessDeniedException e) {
            await IMessageDialogService.Instance.ShowMessage("File System Error", "Access denied", e.Message);
            return;
        }
        catch (FileSystemNoSuchDirectoryException e) {
            if (!isFromFileNotFound)
                await IMessageDialogService.Instance.ShowMessage("File System Error", "No such directory", e.Message);
            await LoadContentsInternal(parent, fsInfo, isFromFileNotFound: true);
            return;
        }
        catch (Exception e) when (e is TimeoutException || e is IOException) {
            await IMessageDialogService.Instance.ShowMessage("Network Error", "Network error reading file system entries", e.Message);
            return;
        }

        foreach (FileSystemEntry entry in result.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Name)) {
            directory.Items.Add(entry.IsDirectory
                ? new FileTreeNodeDirectory() {
                    FileName = entry.Name, CreationTimeUtc = entry.CreatedTime, ModifiedTimeUtc = entry.ModifiedTime, Size = entry.Size,
                }
                : new FileTreeNodeFile() {
                    FileName = entry.Name, CreationTimeUtc = entry.CreatedTime, ModifiedTimeUtc = entry.ModifiedTime, Size = entry.Size,
                });
        }
    }
}