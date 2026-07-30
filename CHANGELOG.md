# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- `GoogleFileSystem.Create` validates its arguments before parsing the credential. A `null` bucket name now raises `ArgumentNullException` with the correct parameter name; previously the credential was parsed first, so an unusable credential masked the real problem with `InvalidOperationException`.

### Fixed
- Moving a directory moved only its marker object, leaving the contents behind, and returned file info instead of directory info.
- Deleting a directory recursively left its contents in the bucket.
- Writing with `overwrite` set to `false` replaced an existing object instead of throwing.
- The MD5 hash was exposed in its raw Base64 form and failed when the value was missing.
- Timestamps are reported in local time, like every other provider.
- The storage client was created per operation instead of once under a lock.

## [1.0.0] - 2024-05-24

### Added
- First release. Google Cloud Storage support for `TagBites.IO`.

[Unreleased]: https://github.com/TagBites/TagBites.IO.Google/compare/1.0.0...HEAD
[1.0.0]: https://github.com/TagBites/TagBites.IO.Google/releases/tag/1.0.0
