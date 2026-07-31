using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace TagBites.IO.Google.Tests;

public class GoogleFileSystemTests
{
    [Fact]
    public void Create_ValidArguments_KindIsGoogle()
    {
        var fileSystem = GoogleFileSystem.Create("my-bucket", CreateServiceAccountJson());

        Assert.Equal("google", fileSystem.Kind);
    }

    [Fact]
    public void Create_ValidArguments_NameIsBucketName()
    {
        var fileSystem = GoogleFileSystem.Create("my-bucket", CreateServiceAccountJson());

        Assert.Equal("my-bucket", fileSystem.Name);
    }

    [Fact]
    public void Create_NullBucketName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GoogleFileSystem.Create(null!, CreateServiceAccountJson()));
    }

    [Fact]
    public void Create_NullCredential_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => GoogleFileSystem.Create("my-bucket", null!));

        Assert.Equal("jsonCredential", exception.ParamName);
    }

    // Arguments are validated before the credential is parsed, so a null bucket name is reported even when the
    // credential is unusable.
    [Fact]
    public void Create_NullBucketNameAndInvalidCredential_ReportsBucketName()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => GoogleFileSystem.Create(null!, "{}"));

        Assert.Equal("bucketName", exception.ParamName);
    }

    [Fact]
    public void Create_CredentialIsNotJson_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => GoogleFileSystem.Create("my-bucket", "not a json document"));
    }

    [Fact]
    public void Create_CredentialWithoutRequiredFields_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => GoogleFileSystem.Create("my-bucket", "{}"));
    }

    private static string CreateServiceAccountJson()
    {
        using var rsa = RSA.Create(2048);

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "test-project",
            ["private_key_id"] = "0000000000000000000000000000000000000000",
            ["private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "test@test-project.iam.gserviceaccount.com",
            ["client_id"] = "000000000000000000000",
            ["token_uri"] = "https://oauth2.googleapis.com/token"
        });
    }
}
