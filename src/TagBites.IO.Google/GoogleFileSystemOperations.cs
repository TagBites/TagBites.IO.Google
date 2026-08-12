using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using TagBites.IO.Operations;
using GoogleObject = Google.Apis.Storage.v1.Data.Object;

namespace TagBites.IO.Google;

internal class GoogleFileSystemOperations(GoogleCredential credential, string bucketName) : IFileSystemAsyncWriteOperations, IFileSystemFeatureSupport, IDisposable
{
    private readonly string _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
    private readonly GoogleCredential _credential = credential ?? throw new ArgumentNullException(nameof(credential));
    private readonly SemaphoreSlim _clientLock = new(1, 1);

    private StorageClient? _storageClient;

    private const string ContentType = "application/x-directory";
    private const char DirectorySeparator = '/';
    public string DirectorySeparatorString => "/";

    public string Kind => "google";
    public string Name => _bucketName;

    FileSystemOperationsFeatures IFileSystemFeatureSupport.Features => FileSystemOperationsFeatures.ConcurrentWriteOperations | FileSystemOperationsFeatures.HierarchicalDirectories;


    public async Task<IFileSystemStructureLinkInfo?> GetLinkInfoAsync(string fullName)
    {
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
        await client.DownloadObjectAsync(_bucketName, file.FullName, stream);
    }
    public async Task<IFileLinkInfo> WriteFileAsync(FileLink file, Stream stream, bool overwrite)
    {
        if (!overwrite && await ObjectExistsAsync(file.FullName))
            throw new IOException($"Unable to create a new file. File already exists: {file.FullName}");

        var client = await PrepareClientAsync();
        var result = await client.UploadObjectAsync(_bucketName, file.FullName, "application/octet-stream", stream);

        return GetFileInfo(result);
    }
    public async Task<IFileLinkInfo> MoveFileAsync(FileLink source, FileLink destination, bool overwrite)
    {
        if (!overwrite && await ObjectExistsAsync(destination.FullName))
            throw new IOException($"Unable to move a new file. File already exists: {destination.FullName}");

        var client = await PrepareClientAsync();

        var result = await client.CopyObjectAsync(_bucketName, source.FullName, _bucketName, destination.FullName);
        await client.DeleteObjectAsync(_bucketName, source.FullName);

        return GetFileInfo(result);
    }
    private async Task<bool> ObjectExistsAsync(string fullName)
    {
        var client = await PrepareClientAsync();
        try
        {
            await client.GetObjectAsync(_bucketName, fullName);
            return true;
        }
        catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
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

        var result = await client.UploadObjectAsync(_bucketName, directoryFullName, ContentType, new MemoryStream([]));

        return GetDirectoryInfo(result);
    }
    public async Task<IFileSystemStructureLinkInfo> MoveDirectoryAsync(DirectoryLink source, DirectoryLink destination)
    {
        var client = await PrepareClientAsync();
        var sourceFullName = GetCorrectDirectoryFullName(source.FullName);
        var destinationFullName = GetCorrectDirectoryFullName(destination.FullName);

        GoogleObject? marker = null;
        await foreach (var item in ListObjectsAsync(sourceFullName))
        {
            var destinationName = destinationFullName + item.Name[sourceFullName.Length..];
            var copy = await client.CopyObjectAsync(_bucketName, item.Name, _bucketName, destinationName);
            await client.DeleteObjectAsync(_bucketName, item.Name);

            if (item.Name == sourceFullName)
                marker = copy;
        }

        return marker != null
            ? GetDirectoryInfo(marker)
            : new DirectoryInfo(destination.FullName);
    }
    private async IAsyncEnumerable<GoogleObject> ListObjectsAsync(string prefix)
    {
        var client = await PrepareClientAsync();

        string? pageToken = null;
        do
        {
            var page = await client
                .ListObjectsAsync(_bucketName, prefix, new ListObjectsOptions { PageToken = pageToken })
                .ReadPageAsync(100);

            foreach (var item in page)
                yield return item;

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));
    }
    public async Task DeleteDirectoryAsync(DirectoryLink directory, bool recursive)
    {
        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);
        var client = await PrepareClientAsync();

        if (!recursive)
        {
            var result = client.ListObjectsAsync(_bucketName, directoryFullName, new ListObjectsOptions
            {
                Delimiter = DirectorySeparatorString,
                IncludeTrailingDelimiter = true
            });

            var page = await result.ReadPageAsync(2);
            if (page.Any(x => x.Name != directoryFullName))
                throw new IOException("Folder is not empty.");

            await client.DeleteObjectAsync(_bucketName, directoryFullName);
            return;
        }

        await foreach (var item in ListObjectsAsync(directoryFullName))
            await client.DeleteObjectAsync(_bucketName, item.Name);
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
            var listObjects = client.ListObjectsAsync(_bucketName, directoryFullName, new ListObjectsOptions
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

        return GetInfo(obj)!;
    }

    private static IFileSystemStructureLinkInfo? GetInfo(GoogleObject? metadata)
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
    private async Task<StorageClient> PrepareClientAsync()
    {
        if (_storageClient != null)
            return _storageClient;

        await _clientLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return _storageClient ??= await StorageClient.CreateAsync(_credential);
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public void Dispose()
    {
        _storageClient?.Dispose();
        _clientLock.Dispose();
    }

    private class FileInfo(GoogleObject metadata) : IFileLinkInfo
    {
        private GoogleObject Metadata { get; } = metadata ?? throw new ArgumentNullException(nameof(metadata));

        public string FullName { get; } = metadata.Name;
        public bool Exists => true;
        public bool? IsDirectory => false;
        public DateTime? CreationTime => Metadata.TimeCreatedDateTimeOffset?.LocalDateTime;
        public DateTime? LastWriteTime => Metadata.UpdatedDateTimeOffset?.LocalDateTime;
        public bool IsHidden => false;
        public bool IsReadOnly => false;

        public string ContentPath => FullName;
        public FileHash Hash { get; } = GetHash(metadata.Md5Hash);
        public long Length => (long)(Metadata.Size ?? 0);

        // Cloud Storage returns the MD5 as base64
        private static FileHash GetHash(string? md5Base64)
        {
            if (string.IsNullOrEmpty(md5Base64))
                return FileHash.Empty;

            try
            {
                return new FileHash(FileHashAlgorithm.Md5, BitConverter.ToString(Convert.FromBase64String(md5Base64)));
            }
            catch (FormatException)
            {
                return FileHash.Empty;
            }
        }
    }
    private class DirectoryInfo : IFileSystemStructureLinkInfo
    {
        private GoogleObject? Metadata { get; }

        public string FullName { get; }
        public bool Exists => true;
        public bool? IsDirectory => true;
        public DateTime? CreationTime => Metadata?.TimeCreatedDateTimeOffset?.LocalDateTime;
        public DateTime? LastWriteTime => Metadata?.UpdatedDateTimeOffset?.LocalDateTime;
        public bool IsHidden => false;
        public bool IsReadOnly => false;

        public DirectoryInfo(GoogleObject metadata)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            FullName = metadata.Name.TrimEnd(DirectorySeparator);
        }
        public DirectoryInfo(string fullName) => FullName = fullName.TrimEnd(DirectorySeparator);
    }
}

