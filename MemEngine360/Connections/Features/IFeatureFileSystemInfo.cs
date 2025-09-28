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

// TODO: use a FileSystem class that delegates to this feature under the hood?

/// <summary>
/// A feature for a connection that can explore and modify the file system and launch files  
/// </summary>
public interface IFeatureFileSystemInfo : IConsoleFeature {
    /// <summary>
    /// Gets a list of drives
    /// </summary>
    Task<List<DriveEntry>> GetDriveList();

    /// <summary>
    /// Gets a list of all file system entries within a directory, such as within a root obtained
    /// from <see cref="GetDriveList"/>. Returns a null list when the directory does not exist
    /// </summary>
    /// <param name="fullPath">Directory path</param>
    /// <returns>The entries</returns>
    Task<List<FileSystemEntry>> GetFileSystemEntries(string fullPath);
    
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
    
    /// <summary>
    /// Moves a file from one location to another location.
    /// </summary>
    Task MoveFile(string oldPath, string newPath);
    
    /// <summary>
    /// Creates a directory with the given path. All subdirectories will be created
    /// </summary>
    Task CreateDirectory(string path);

    /// <summary>
    /// Gets the directory path from a path
    /// </summary>
    string GetDirectoryPath(string path);

    /// <summary>
    /// Gets the file name (or directory name) of a path
    /// </summary>
    string GetFileName(string path);
    
    /// <summary>
    /// Joins together multiple path parts into a final path
    /// </summary>
    string JoinPaths(params string[] paths);

    /// <summary>
    /// Reverse of <see cref="JoinPaths"/>
    /// </summary>
    string[] SplitPath(string filePath);
    

    /// <summary>
    /// Checks whether the path is valid, as in, does not contain invalid characters or is not too long, etc
    /// </summary>
    bool IsPathValid(string path);
}

public struct FileSystemEntry {
    /// <summary>The name of this entry, i.e. file or folder name</summary>
    public string Name;

    /// <summary>The size of this entry. This is zero for folders, since it must be calculated dynamically</summary>
    public ulong Size;

    /// <summary>The time that the entry was created</summary>
    public DateTime CreatedTime;

    /// <summary>The time that the entry was last modified</summary>
    public DateTime ModifiedTime;

    /// <summary>Whether this entry represents a directory</summary>
    public bool IsDirectory;
}

public struct DriveEntry {
    public string Name;
    public ulong TotalSize;
    public ulong FreeBytes;
}

public abstract class FileSystemException : Exception {
    protected FileSystemException(string? message) : base(message) {
    }
}

public sealed class FileSystemNoSuchDirectoryException : FileSystemException {
    public FileSystemNoSuchDirectoryException(string directoryName) : base($"No such directory: {directoryName}") {
    }
}

public sealed class FileSystemAccessDeniedException : FileSystemException {
    public FileSystemAccessDeniedException(string message) : base(message) {
    }
}