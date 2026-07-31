using Google.Apis.Auth.OAuth2;

namespace TagBites.IO.Google;

/// <summary>
/// Exposes static method for creating a Google Cloud Storage file system.
/// </summary>
public class GoogleFileSystem
{
    /// <summary>
    /// Creates a Google Cloud Storage file system.
    /// </summary>
    /// <param name="bucketName">The name of an existing Google Cloud Storage bucket.</param>
    /// <param name="jsonCredential">The JSON key content of a Google Cloud service account with access to the bucket.</param>
    /// <returns>A Google Cloud Storage file system contains the procedures that are used to perform file and directory operations.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static FileSystem Create(string bucketName, string jsonCredential)
    {
        if (bucketName == null)
            throw new ArgumentNullException(nameof(bucketName));
        if (jsonCredential == null)
            throw new ArgumentNullException(nameof(jsonCredential));

        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(jsonCredential).ToGoogleCredential();
        return new FileSystem(new GoogleFileSystemOperations(credential, bucketName));
    }
}
