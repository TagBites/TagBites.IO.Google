# TagBites.IO.Google

Google Cloud Storage file system support for [TagBites.IO](https://github.com/TagBites/TagBites.IO), built on `Google.Cloud.Storage.V1`. Browse, read and write a Google Cloud Storage bucket through the same `FileSystem` API used for local disk and other storages.

## Install

```
dotnet add package TagBites.IO.Google
```

## Usage

```csharp
using TagBites.IO.Google;

var fs = GoogleFileSystem.Create(bucketName, jsonCredential);

var file = fs.GetFile("/reports/summary.txt");
file.WriteAllText("Hello world!");

var content = file.ReadAllText();
```

`jsonCredential` is the JSON key content of a Google Cloud service account with access to the bucket.

## License

See [https://www.tagbites.com/io](https://www.tagbites.com/io) for licensing terms.
