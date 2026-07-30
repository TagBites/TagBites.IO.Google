# TagBites.IO.Google

[![Nuget](https://img.shields.io/nuget/v/TagBites.IO.Google.svg)](https://www.nuget.org/packages/TagBites.IO.Google/)
![.NET Standard 2.1](https://img.shields.io/badge/.NET%20Standard-2.1-512BD4)
[![License](https://img.shields.io/github/license/TagBites/TagBites.IO.Google)](https://github.com/TagBites/TagBites.IO.Google/blob/master/LICENSE.md)

Google Cloud Storage file system support for [TagBites.IO](https://github.com/TagBites/TagBites.IO), built on `Google.Cloud.Storage.V1`. Browse, read and write a Google Cloud Storage bucket through the same `FileSystem` API used for local disk and other storages.

## Install

```
dotnet add package TagBites.IO.Google
```

Targets `netstandard2.1`. Depends on `Google.Cloud.Storage.V1`.

## Usage

```csharp
using TagBites.IO.Google;

var fs = GoogleFileSystem.Create(bucketName, jsonCredential);

var file = fs.GetFile("/reports/summary.txt");
file.WriteAllText("Hello world!");

var content = file.ReadAllText();
```

`jsonCredential` is the JSON key content of a Google Cloud service account with access to the bucket.

## Capabilities

- Asynchronous operations. Synchronous calls run on top of them.
- Metadata: none.
- Timestamps are reported in local time, like every other provider.

## Links

- [Changelog](https://github.com/TagBites/TagBites.IO.Google/blob/master/CHANGELOG.md)
- [Security policy](https://github.com/TagBites/TagBites.IO.Google/blob/master/SECURITY.md)
- [License (MIT)](https://github.com/TagBites/TagBites.IO.Google/blob/master/LICENSE.md)
