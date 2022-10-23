using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Baseline.Filesystem;

/// <summary>
/// An in-memory representation of a filesystem, with helper methods to make understanding it easier.
/// </summary>
public class MemoryFilesystem
{
    /// <summary>
    /// Gets the root directory which is the base of the memory filesystem.
    /// </summary>
    public MemoryDirectoryRepresentation RootDirectory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryFilesystem"/> class.
    /// </summary>
    public MemoryFilesystem()
    {
        RootDirectory = new(
            ChildDirectories: new Dictionary<PathRepresentation, MemoryDirectoryRepresentation>(),
            Files: new Dictionary<PathRepresentation, MemoryFileRepresentation>()
        );
    }

    /// <summary>
    /// Ensures that the given directory path representation exists within memory.
    /// </summary>
    public bool DirectoryExists(PathRepresentation path)
    {
        if (path.NormalisedPath == "")
        {
            return true;
        }

        var workingDirectory = RootDirectory;

        foreach (var pathPart in path.GetPathTree())
        {
            if (!workingDirectory.ChildDirectories.ContainsKey(pathPart))
            {
                return false;
            }

            workingDirectory = workingDirectory.ChildDirectories[pathPart];
        }

        return true;
    }

    /// <summary>
    /// Gets or creates the parent directory (i.e. the second to last path part) of a given path.
    /// </summary>
    public MemoryDirectoryRepresentation GetOrCreateParentDirectoryOf(PathRepresentation path)
    {
        var pathTree = path.GetPathTree().ToList();

        return pathTree.Count == 1 ? RootDirectory : GetOrCreateDirectory(pathTree[^2]);
    }

    /// <summary>
    /// Gets or creates a directory within the memory filesystem.
    /// </summary>
    public MemoryDirectoryRepresentation GetOrCreateDirectory(PathRepresentation path)
    {
        var workingDirectory = RootDirectory;
        var directoryToReturn = RootDirectory;
        var pathTree = path.GetPathTree().ToList();

        foreach (var pathPart in pathTree)
        {
            if (workingDirectory.ChildDirectories.ContainsKey(pathPart))
            {
                workingDirectory = workingDirectory.ChildDirectories[pathPart];
                directoryToReturn = workingDirectory;
                continue;
            }

            var newDirectory = new MemoryDirectoryRepresentation(
                ChildDirectories: new Dictionary<
                    PathRepresentation,
                    MemoryDirectoryRepresentation
                >(),
                Files: new Dictionary<PathRepresentation, MemoryFileRepresentation>()
            );

            workingDirectory.ChildDirectories.Add(pathPart, newDirectory);

            workingDirectory = newDirectory;
            directoryToReturn = newDirectory;
        }

        return directoryToReturn;
    }

    /// <summary>
    /// Given a path tree (i.e. a/, a/b/, a/b/c/) retrieve the Nth level <see cref="MemoryDirectoryRepresentation"/>
    /// of that path from the in memory filesystem. The Nth level should be 0 based.
    /// </summary>
    public MemoryDirectoryRepresentation GetDirectoryFromNthLevelOfPathTree(
        IReadOnlyList<PathRepresentation> pathTree,
        int level
    )
    {
        var workingDirectory = RootDirectory;

        for (var i = 0; i <= level; i++)
        {
            var pathPart = pathTree[i];
            workingDirectory = workingDirectory.ChildDirectories[pathPart];
        }

        return workingDirectory;
    }
}
