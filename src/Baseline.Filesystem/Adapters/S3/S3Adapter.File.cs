using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Baseline.Filesystem.Internal.Adapters.S3;

namespace Baseline.Filesystem;

/// <summary>
/// Provides the file based functions of an <see cref="IAdapter"/> for Amazon's Simple Storage Service.
/// </summary>
public partial class S3Adapter
{
    /// <inheritdoc />
    public async Task<CopyFileResponse> CopyFileAsync(
        CopyFileRequest copyFileRequest,
        CancellationToken cancellationToken
    )
    {
        await EnsureFileExistsAsync(copyFileRequest.SourceFilePath, cancellationToken)
            .ConfigureAwait(false);
        await EnsureFileDoesNotExistAsync(copyFileRequest.DestinationFilePath, cancellationToken)
            .ConfigureAwait(false);

        await CopyFileInternalAsync(
                copyFileRequest.SourceFilePath,
                copyFileRequest.DestinationFilePath,
                cancellationToken
            )
            .ConfigureAwait(false);

        return new CopyFileResponse
        {
            DestinationFile = new FileRepresentation { Path = copyFileRequest.DestinationFilePath }
        };
    }

    /// <inheritdoc />
    public async Task<DeleteFileResponse> DeleteFileAsync(
        DeleteFileRequest deleteFileRequest,
        CancellationToken cancellationToken
    )
    {
        await EnsureFileExistsAsync(deleteFileRequest.FilePath, cancellationToken)
            .ConfigureAwait(false);

        await DeleteFileInternalAsync(deleteFileRequest.FilePath, cancellationToken)
            .ConfigureAwait(false);

        return new DeleteFileResponse();
    }

    /// <inheritdoc />
    public async Task<FileExistsResponse> FileExistsAsync(
        FileExistsRequest fileExistsRequest,
        CancellationToken cancellationToken
    )
    {
        return new FileExistsResponse
        {
            FileExists = await FileExistsInternalAsync(
                    fileExistsRequest.FilePath,
                    cancellationToken
                )
                .ConfigureAwait(false)
        };
    }

    /// <inheritdoc />
    public async Task<GetFileResponse> GetFileAsync(
        GetFileRequest getFileRequest,
        CancellationToken cancellationToken
    )
    {
        var fileExists = await FileExistsInternalAsync(getFileRequest.FilePath, cancellationToken)
            .ConfigureAwait(false);
        return fileExists
            ? new GetFileResponse
            {
                File = new FileRepresentation { Path = getFileRequest.FilePath }
            }
            : null;
    }

    /// <inheritdoc />
    public async Task<GetFilePublicUrlResponse> GetFilePublicUrlAsync(
        GetFilePublicUrlRequest getFilePublicUrlRequest,
        CancellationToken cancellationToken
    )
    {
        await EnsureFileExistsAsync(getFilePublicUrlRequest.FilePath, cancellationToken)
            .ConfigureAwait(false);

        var expiry = getFilePublicUrlRequest.Expiry ?? DateTime.Today.AddDays(1);

        return new GetFilePublicUrlResponse
        {
            Expiry = expiry,
            Url = _s3Client.GetPreSignedURL(
                new GetPreSignedUrlRequest
                {
                    BucketName = _adapterConfiguration.BucketName,
                    Key = getFilePublicUrlRequest.FilePath.NormalisedPath,
                    Expires = expiry
                }
            )
        };
    }

    /// <inheritdoc />
    public async Task<MoveFileResponse> MoveFileAsync(
        MoveFileRequest moveFileRequest,
        CancellationToken cancellationToken
    )
    {
        await EnsureFileExistsAsync(moveFileRequest.SourceFilePath, cancellationToken)
            .ConfigureAwait(false);
        await EnsureFileDoesNotExistAsync(moveFileRequest.DestinationFilePath, cancellationToken)
            .ConfigureAwait(false);

        await CopyFileInternalAsync(
                moveFileRequest.SourceFilePath,
                moveFileRequest.DestinationFilePath,
                cancellationToken
            )
            .ConfigureAwait(false);

        await DeleteFileInternalAsync(moveFileRequest.SourceFilePath, cancellationToken)
            .ConfigureAwait(false);

        return new MoveFileResponse
        {
            DestinationFile = new FileRepresentation { Path = moveFileRequest.DestinationFilePath }
        };
    }

    /// <inheritdoc />
    public async Task<ReadFileAsStreamResponse> ReadFileAsStreamAsync(
        ReadFileAsStreamRequest readFileAsStreamRequest,
        CancellationToken cancellationToken
    )
    {
        await EnsureFileExistsAsync(readFileAsStreamRequest.FilePath, cancellationToken)
            .ConfigureAwait(false);

        var file = await _s3Client
            .GetObjectAsync(
                _adapterConfiguration.BucketName,
                readFileAsStreamRequest.FilePath.NormalisedPath,
                cancellationToken
            )
            .ConfigureAwait(false);

        // await using var responseStream = file.ResponseStream;

        // var streamToReturn = new MemoryStream();
        // await responseStream.CopyToAsync(streamToReturn, cancellationToken);
        // streamToReturn.Seek(0, SeekOrigin.Begin);

        return new ReadFileAsStreamResponse { FileContents = file.ResponseStream };
    }

    /// <inheritdoc />
    public async Task<ReadFileAsStringResponse> ReadFileAsStringAsync(
        ReadFileAsStringRequest readFileAsStringRequest,
        CancellationToken cancellationToken
    )
    {
        await EnsureFileExistsAsync(readFileAsStringRequest.FilePath, cancellationToken)
            .ConfigureAwait(false);

        var file = await _s3Client
            .GetObjectAsync(
                _adapterConfiguration.BucketName,
                readFileAsStringRequest.FilePath.NormalisedPath,
                cancellationToken
            )
            .ConfigureAwait(false);

        using var streamReader = new StreamReader(file.ResponseStream);
        return new ReadFileAsStringResponse
        {
            FileContents = await streamReader.ReadToEndAsync().ConfigureAwait(false)
        };
    }

    /// <inheritdoc />
    public async Task<TouchFileResponse> TouchFileAsync(
        TouchFileRequest touchFileRequest,
        CancellationToken cancellationToken
    )
    {
        await EnsureFileDoesNotExistAsync(touchFileRequest.FilePath, cancellationToken)
            .ConfigureAwait(false);
        await TouchFileInternalAsync(touchFileRequest.FilePath, cancellationToken)
            .ConfigureAwait(false);

        return new TouchFileResponse
        {
            File = new FileRepresentation { Path = touchFileRequest.FilePath }
        };
    }

    /// <inheritdoc />
    public async Task<WriteStreamToFileResponse> WriteStreamToFileAsync(
        WriteStreamToFileRequest writeStreamToFileRequest,
        CancellationToken cancellationToken
    )
    {
        await _s3Client
            .PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = _adapterConfiguration.BucketName,
                    AutoCloseStream = false,
                    InputStream = writeStreamToFileRequest.Stream,
                    ContentType = writeStreamToFileRequest.ContentType,
                    Key = writeStreamToFileRequest.FilePath.NormalisedPath
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return new WriteStreamToFileResponse();
    }

    /// <inheritdoc />
    public async Task<WriteTextToFileResponse> WriteTextToFileAsync(
        WriteTextToFileRequest writeTextToFileRequest,
        CancellationToken cancellationToken
    )
    {
        await _s3Client
            .PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = _adapterConfiguration.BucketName,
                    ContentBody = writeTextToFileRequest.TextToWrite,
                    ContentType = writeTextToFileRequest.ContentType,
                    Key = writeTextToFileRequest.FilePath.NormalisedPath
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return new WriteTextToFileResponse();
    }

    /// <summary>
    /// Checks and returns whether a file exists. For use within methods that do their own validation.
    /// </summary>
    /// <param name="path">The path to check to see if it exists.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Whether the file exists or not.</returns>
    private async Task<bool> FileExistsInternalAsync(
        PathRepresentation path,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _s3Client
                .GetObjectMetadataAsync(
                    _adapterConfiguration.BucketName,
                    path.NormalisedPath,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return true;
        }
        catch (Exception exception)
        {
            if (exception is AmazonS3Exception { StatusCode: HttpStatusCode.NotFound })
            {
                return false;
            }

            throw;
        }
    }

    /// <summary>
    /// Copies a file without performing any validation. For use within methods that do their own validation.
    /// </summary>
    /// <param name="source">The file to copy.</param>
    /// <param name="destination">The destination to copy it to.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task CopyFileInternalAsync(
        PathRepresentation source,
        PathRepresentation destination,
        CancellationToken cancellationToken
    )
    {
        await _s3Client
            .CopyObjectAsync(
                _adapterConfiguration.BucketName,
                source.NormalisedPath,
                _adapterConfiguration.BucketName,
                destination.NormalisedPath,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a file without performing any validation. For use within methods that do their own validation.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="cancellationToken"></param>
    private async Task DeleteFileInternalAsync(
        PathRepresentation file,
        CancellationToken cancellationToken
    )
    {
        await _s3Client
            .DeleteObjectAsync(
                _adapterConfiguration.BucketName,
                file.NormalisedPath,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Touches (i.e. creates a blank file) without performing any validation. For use within methods that do their
    /// own validation.
    /// </summary>
    /// <param name="file">The file to create.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task TouchFileInternalAsync(
        PathRepresentation file,
        CancellationToken cancellationToken
    )
    {
        await _s3Client
            .PutObjectAsync(
                new PutObjectRequest
                {
                    BucketName = _adapterConfiguration.BucketName,
                    ContentBody = "",
                    ContentType = "text/plain",
                    Key = file.NormalisedPath
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Used only in methods inside of this class, EnsureFileDoesNotExistAsync does the opposite of
    /// <see cref="EnsureFileExistsAsync" /> and verifies that the file DOES NOT exist, or it throws an exception.
    /// </summary>
    /// <param name="path">The path to the file that should not exist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task EnsureFileDoesNotExistAsync(
        PathRepresentation path,
        CancellationToken cancellationToken
    )
    {
        if (await FileExistsInternalAsync(path, cancellationToken).ConfigureAwait(false))
        {
            throw new FileAlreadyExistsException(path.NormalisedPath);
        }
    }

    /// <summary>
    /// Used only in methods inside of this class, EnsureFileExistsAsync checks if the requested file exists and, if
    /// it doesn't, throws a <see cref="FileNotFoundException"/>.
    /// </summary>
    /// <param name="path">The path to check exists.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task EnsureFileExistsAsync(
        PathRepresentation path,
        CancellationToken cancellationToken
    )
    {
        if (!await FileExistsInternalAsync(path, cancellationToken).ConfigureAwait(false))
        {
            throw new FileNotFoundException(path.NormalisedPath);
        }
    }

    /// <summary>
    /// Lists all of the files under a particular path prefix within S3.
    /// </summary>
    /// <param name="path">The path to use as a prefix.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="marker">
    /// A point at which the listing should continue. Useful for paginating large directories as S3 is limited
    /// to 1000 objects returned from the list endpoint.
    /// </param>
    /// <returns>The response from S3 containing the files (if there are any) for the prefix.</returns>
    private async Task<ListObjectsResponse> ListFilesUnderPath(
        PathRepresentation path,
        CancellationToken cancellationToken,
        string marker = null
    )
    {
        return await _s3Client
            .ListObjectsAsync(
                new ListObjectsRequest
                {
                    BucketName = _adapterConfiguration.BucketName,
                    Prefix = path.S3SafeDirectoryPath(),
                    Marker = marker
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loops through all of the files under a particular path prefix and performs an action until actions are performed
    /// on each page within the paginated result set. Returns every file that was retrieved as part of the loop.
    /// </summary>
    /// <param name="path">The path prefix to retrieve files under.</param>
    /// <param name="action">An optional action to perform on the returned response.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task<
        List<S3Object>
    > ListAndReturnPaginatedFilesUnderPathAndPerformActionUntilCompleteAsync(
        PathRepresentation path,
        Func<ListObjectsResponse, Task> action = null,
        CancellationToken cancellationToken = default
    )
    {
        var objects = new List<S3Object>();

        await ListPaginatedFilesUnderPathAndPerformActionUntilCompleteAsync(
                path,
                async listObjectsResponse =>
                {
                    objects.AddRange(listObjectsResponse.S3Objects);

                    if (action == null)
                    {
                        return true;
                    }

                    await action.Invoke(listObjectsResponse).ConfigureAwait(false);
                    return true;
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return objects;
    }

    /// <summary>
    /// Retrieves all of the files under a particular path prefix and performs an action until actions are performed
    /// on each page within the paginated result set.
    /// </summary>
    /// <param name="path">The path prefix to retrieve files under.</param>
    /// <param name="action">An optional action to perform on the returned response.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    private async Task ListPaginatedFilesUnderPathAndPerformActionUntilCompleteAsync(
        PathRepresentation path,
        Func<ListObjectsResponse, Task<bool>> action = null,
        CancellationToken cancellationToken = default
    )
    {
        string marker = null;

        ListObjectsResponse objectsInPrefix;
        do
        {
            objectsInPrefix = await ListFilesUnderPath(path, cancellationToken, marker)
                .ConfigureAwait(false);

            if (objectsInPrefix.S3Objects == null || !objectsInPrefix.S3Objects.Any())
            {
                break;
            }

            marker = objectsInPrefix.NextMarker;

            if (action == null)
            {
                continue;
            }

            var @continue = await action(objectsInPrefix).ConfigureAwait(false);
            if (!@continue)
            {
                break;
            }
        } while (objectsInPrefix.IsTruncated);
    }
}
