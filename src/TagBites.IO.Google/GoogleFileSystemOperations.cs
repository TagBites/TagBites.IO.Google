using System.Net;
using System.Text;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using TagBites.IO.Operations;
using GoogleObject = Google.Apis.Storage.v1.Data.Object;

namespace TagBites.IO.Google;
internal class GoogleFileSystemOperations : IFileSystemAsyncWriteOperations, IFileSystemMetadataSupport, IDisposable
{
    private readonly string _bucketName;
    private readonly GoogleCredential _credential;

    private StorageClient? _storageClient;

    private const string ContentType = "application/x-directory";
    private const char DirectorySeparator = '/';
    public string DirectorySeparatorString => "/";

    public string Kind => "google";
    public string Name => _bucketName;

    #region IFileSystemOperationsMetadataSupport

    bool IFileSystemMetadataSupport.SupportsIsHiddenMetadata => false;
    bool IFileSystemMetadataSupport.SupportsIsReadOnlyMetadata => false;
    bool IFileSystemMetadataSupport.SupportsLastWriteTimeMetadata => false;

    #endregion

    public GoogleFileSystemOperations(GoogleCredential credential, string bucketName)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    }


    public async Task<IFileSystemStructureLinkInfo?> GetLinkInfoAsync(string fullName)
    {
        var client = await PrepareClientAsync();

        try
        {
            return await GetLinkInfoCoreAsync(fullName);
        }
        catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
        {
            if (Path.HasExtension(fullName))
                return null;

            try
            {
                var correctFullName = GetCorrectDirectoryFullName(fullName);
                return await GetLinkInfoCoreAsync(correctFullName);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
    private async Task<IFileSystemStructureLinkInfo?> GetLinkInfoCoreAsync(string fullName)
    {
        var client = await PrepareClientAsync();
        var info = await client.GetObjectAsync(_bucketName, fullName);
        return GetInfo(info);
    }

    public async Task ReadFileAsync(FileLink file, Stream stream)
    {
        var client = await PrepareClientAsync();
        var _ = await client.DownloadObjectAsync(_bucketName, file.FullName, stream);
    }
    public async Task<IFileLinkInfo> WriteFileAsync(FileLink file, Stream stream, bool overwrite)
    {
        var client = await PrepareClientAsync();
        var result = await client.UploadObjectAsync(_bucketName, file.FullName, "application/octet-stream", stream);

        return GetFileInfo(result);
    }
    public async Task<IFileLinkInfo> MoveFileAsync(FileLink source, FileLink destination, bool overwrite)
    {
        var client = await PrepareClientAsync();

        var result = await client.CopyObjectAsync(_bucketName, source.FullName, _bucketName, destination.FullName);
        await client.DeleteObjectAsync(_bucketName, source.FullName);

        return GetFileInfo(result);
    }
    public async Task DeleteFileAsync(FileLink file)
    {
        var client = await PrepareClientAsync();
        try
        {
            await client.DeleteObjectAsync(_bucketName, file.FullName);
        }
        catch (GoogleApiException e)
        {
            throw new IOException(e.Message, e);
        }
    }

    public async Task<IFileSystemStructureLinkInfo> CreateDirectoryAsync(DirectoryLink directory)
    {
        var client = await PrepareClientAsync();
        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);
        var content = Encoding.UTF8.GetBytes("");

        var result = await client.UploadObjectAsync(_bucketName, directoryFullName, ContentType, new MemoryStream(Array.Empty<byte>()));

        return GetDirectoryInfo(result);
    }
    public async Task<IFileSystemStructureLinkInfo> MoveDirectoryAsync(DirectoryLink source, DirectoryLink destination)
    {
        var client = await PrepareClientAsync();
        var sourceFullName = GetCorrectDirectoryFullName(source.FullName);
        var destinationFullName = GetCorrectDirectoryFullName(destination.FullName);
        var result = await client.CopyObjectAsync(_bucketName, sourceFullName, _bucketName, destinationFullName);
        await client.DeleteObjectAsync(_bucketName, sourceFullName);

        return new FileInfo(result);
    }
    public async Task DeleteDirectoryAsync(DirectoryLink directory, bool recursive)
    {
        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);
        var client = await PrepareClientAsync();

        if (!recursive)
        {
            var result = client.ListObjectsAsync(_bucketName, directoryFullName, new ListObjectsOptions()
            {
                Delimiter = DirectorySeparatorString,
                IncludeTrailingDelimiter = true
            });

            var page = await result.ReadPageAsync(2);
            if (page.Any(x => x.Name != directoryFullName))
                throw new IOException("Folder is not empty.");
        }

        await client.DeleteObjectAsync(_bucketName, directoryFullName);
    }

    public async Task<IList<IFileSystemStructureLinkInfo>> GetLinksAsync(DirectoryLink directory, FileSystem.ListingOptions options)
    {
        var client = await PrepareClientAsync();

        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);
        options.RecursiveHandled = true;

        var isTruncated = true;
        string? continuationToken = null;
        var result = new List<IFileSystemStructureLinkInfo>();

        var delimiter = !options.Recursive ? DirectorySeparatorString : null;
        while (isTruncated)
        {
            var listObjects = client.ListObjectsAsync(_bucketName, directoryFullName, new ListObjectsOptions()
            {
                Delimiter = delimiter,
                PageToken = continuationToken,
                IncludeTrailingDelimiter = !string.IsNullOrEmpty(delimiter) && options.SearchForDirectories
            });

            var page = await listObjects.ReadPageAsync(100);
            foreach (var item in page)
            {
                if (item.Name == directoryFullName)
                    continue;

                var info = GetInfo(item);
                if (info != null)
                    result.Add(info);
            }

            continuationToken = page.NextPageToken;
            isTruncated = !string.IsNullOrEmpty(continuationToken);
        }

        return result;
    }
    public async Task<IFileSystemStructureLinkInfo> UpdateMetadataAsync(FileSystemStructureLink link, IFileSystemLinkMetadata metadata)
    {
        var client = await PrepareClientAsync();

        var obj = await client.GetObjectAsync(_bucketName, link.FullName);
        // The object was just successfully fetched, so response is never null here.
        return GetInfo(obj)!;
    }

    private static IFileSystemStructureLinkInfo? GetInfo(GoogleObject metadata)
    {
        if (metadata == null)
            return null;

        if (metadata.ContentType == ContentType)
            return new DirectoryInfo(metadata);

        return new FileInfo(metadata);
    }
    private static DirectoryInfo GetDirectoryInfo(GoogleObject metadata) => new(metadata);
    private static FileInfo GetFileInfo(GoogleObject metadata) => new(metadata);

    private string GetCorrectDirectoryFullName(string directoryFullName) => directoryFullName.TrimEnd(DirectorySeparator) + DirectorySeparator;
    private async Task<StorageClient> PrepareClientAsync() => _storageClient ??= await StorageClient.CreateAsync(_credential);

    public void Dispose()
    {
        _storageClient?.Dispose();
    }

    private class FileInfo : IFileLinkInfo
    {
        private GoogleObject Metadata { get; }

        public string FullName { get; }
        public bool Exists => true;
        public bool? IsDirectory => false;
        public DateTime? CreationTime => Metadata.TimeCreatedDateTimeOffset?.DateTime;
        public DateTime? LastWriteTime => Metadata.UpdatedDateTimeOffset?.DateTime;
        public bool IsHidden => false;
        public bool IsReadOnly => false;

        public string ContentPath => FullName;
        public FileHash Hash { get; }
        public long Length => (long)(Metadata.Size ?? 0);

        public FileInfo(GoogleObject metadata)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            FullName = metadata.Name;
            Hash = new FileHash(FileHashAlgorithm.Md5, metadata.Md5Hash);
        }
    }
    private class DirectoryInfo : IFileSystemStructureLinkInfo
    {
        private GoogleObject Metadata { get; }

        public string FullName { get; }
        public bool Exists => true;
        public bool? IsDirectory => true;
        public DateTime? CreationTime => Metadata.TimeCreatedDateTimeOffset?.DateTime;
        public DateTime? LastWriteTime => Metadata.UpdatedDateTimeOffset?.DateTime;
        public bool IsHidden => false;
        public bool IsReadOnly => false;

        public DirectoryInfo(GoogleObject metadata)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            FullName = metadata.Name.TrimEnd(DirectorySeparator);
        }
    }
}

