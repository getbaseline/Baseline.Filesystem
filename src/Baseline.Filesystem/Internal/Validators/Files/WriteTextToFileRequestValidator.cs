using System;

namespace Baseline.Filesystem.Internal.Validators.Files;

/// <summary>
/// Validation methods for the <see cref="WriteTextToFileRequest" /> class.
/// </summary>
internal static class WriteTextToFileRequestValidator
{
    /// <summary>
    /// Validates the request and throws any related exceptions if that validation fails.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <exception cref="ArgumentNullException" />
    public static void ValidateAndThrowIfUnsuccessful(WriteTextToFileRequest request)
    {
        BaseSingleFileRequestValidator.ValidateAndThrowIfUnsuccessful(request);

        if (request.TextToWrite == null)
        {
            throw new ArgumentNullException(nameof(request.TextToWrite));
        }
    }
}
