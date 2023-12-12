using System;

namespace LSymds.Filesystem.Internal.Validators.Files;

/// <summary>
/// Validation methods for paths in file related requests.
/// </summary>
internal static class FilePathValidator
{
    /// <summary>
    /// Validates the path in a file related request and throws if any of the validation criteria fail.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <exception cref="ArgumentNullException" />
    /// <exception cref="PathIsADirectoryException" />
    public static void ValidateAndThrowIfUnsuccessful(PathRepresentation filePath)
    {
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (filePath.FinalPathPartIsADirectory)
        {
            throw new PathIsADirectoryException(filePath.OriginalPath);
        }
    }
}
