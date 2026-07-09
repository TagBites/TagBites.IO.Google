using Google.Apis.Auth.OAuth2;

namespace TagBites.IO.Google;

public class GoogleFileSystem
{
    public static FileSystem Create(string bucketName, string jsonCredential)
    {
        var credential = CredentialFactory.FromJson<ServiceAccountCredential>(jsonCredential).ToGoogleCredential();
        return new FileSystem(new GoogleFileSystemOperations(credential, bucketName));
    }
}
