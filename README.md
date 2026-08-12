# TagBites.IO.Google

[![Nuget](https://img.shields.io/nuget/v/TagBites.IO.Google.svg)](https://www.nuget.org/packages/TagBites.IO.Google/)
![.NET Standard 2.1](https://img.shields.io/badge/.NET%20Standard-2.1-512BD4)
[![License](https://img.shields.io/github/license/TagBites/TagBites.IO.Google)](https://github.com/TagBites/TagBites.IO.Google/blob/master/LICENSE.md)
[![Downloads](https://img.shields.io/nuget/dt/TagBites.IO.Google.svg)](https://www.nuget.org/packages/TagBites.IO.Google/)

Google Cloud Storage file system support for [TagBites.IO](https://github.com/TagBites/TagBites.IO), built on `Google.Cloud.Storage.V1`. Browse, read and write a Google Cloud Storage bucket through the same `FileSystem` API used for local disk and other storages.

## Install

```
dotnet add package TagBites.IO.Google
```

Targets `netstandard2.1`. Depends on `Google.Cloud.Storage.V1`.

## Usage

```csharp
using var fs = GoogleFileSystem.Create(bucketName, jsonCredential);

var file = fs.GetFile("/reports/summary.txt");
file.WriteAllText("Hello world!");

var content = file.ReadAllText(); // "Hello world!"
```

Connection options, capabilities and limits: [documentation](https://tagbites.com/io/file-systems/google).

## Links

- [Changelog](https://tagbites.com/io/changelog#google)
