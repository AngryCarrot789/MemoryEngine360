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

namespace MemEngine360.Connections.Features;

/// <summary>
/// A feature for a connection that can explore and modify the file system and launch files  
/// </summary>
public interface IFeatureFileSystemInfo : IConsoleFeature {
    // TODO: file system navigation

    /// <summary>
    /// Gets a list of drives
    /// </summary>
    Task<List<DriveEntry>> GetDriveList();

    /// <summary>
    /// Gets a list of all file system entries within a directory, such as within a root obtained
    /// from <see cref="GetDriveList"/>. Returns a null list when the directory does not exist
    /// </summary>
    /// <param name="directory">Directory path</param>
    /// <returns>The entries</returns>
    Task<(EnumFileSystemListResult, List<FileSystemEntry>?)> GetFileSystemEntries(string directory);

    /// <summary>
    /// Deletes a file on the console
    /// </summary>
    /// <param name="path">The file path</param>
    Task DeleteFile(string path);

    /// <summary>
    /// Launches an executable file, e.g. an XEX
    /// </summary>
    /// <param name="path"></param>
    Task LaunchFile(string path);
}

public struct FileSystemEntry {
    public string Name;
    public ulong Size;
    public DateTime CreatedTime;
    public DateTime ModifiedTime;
    public bool IsDirectory;
}

public struct DriveEntry {
    public string Name;
    public ulong TotalSize;
    public ulong FreeBytes;
}

public enum EnumFileSystemListResult {
    Success,
    NoSuchDirectory,
    AccessDenied,
}