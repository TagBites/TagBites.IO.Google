# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-08-23

### Changed
- Requires `TagBites.IO` 2.0.0.
- The license is Apache-2.0, previously MIT.
- Depends on `Google.Cloud.Storage.V1` 4.15.0, previously 4.7.0.
- `GoogleFileSystem.Create` validates its arguments before parsing the credential, so a `null` bucket name raises `ArgumentNullException` instead of a credential error.

### Fixed
- Moving a directory moved only its marker object and returned file info instead of directory info.
- Deleting a directory recursively left its contents in the bucket.
- Writing with `overwrite` set to `false` replaced an existing object instead of throwing.
- The MD5 hash was exposed in its raw Base64 form and failed when the value was missing.
- Timestamps are reported in local time, like every other provider.
- A link info lookup swallowed real errors and reported the link as missing.

## [1.0.0] - 2024-05-24

### Added
- First release. Google Cloud Storage support for `TagBites.IO`.

[2.0.0]: https://github.com/TagBites/TagBites.IO.Google/compare/1.0.0...2.0.0
[1.0.0]: https://github.com/TagBites/TagBites.IO.Google/releases/tag/1.0.0
