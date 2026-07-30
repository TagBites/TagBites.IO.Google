# Security Policy

## Supported versions

Security fixes are provided for the latest released version.

| Version | Supported |
|---------|-----------|
| 1.0.x   | ✅        |
| < 1.0   | ❌        |

## Reporting a vulnerability

Please **do not** open a public issue for security problems.

Report vulnerabilities privately through GitHub: **Security → [Report a vulnerability](https://github.com/TagBites/TagBites.IO.Google/security/advisories/new)**.

Include a description, the affected version, and a minimal program that reproduces the issue. We aim to acknowledge reports within a few business days and to release a fix or mitigation as soon as a valid issue is confirmed.

## Security model

This package is a provider for [TagBites.IO](https://github.com/TagBites/TagBites.IO). The core security model - no sandbox, paths are the only limit, advisory permissions, content buffered through the system temporary directory - is described in the [core security policy](https://github.com/TagBites/TagBites.IO/blob/master/SECURITY.md). What follows is specific to this provider.

### Credentials

A service account key is passed as a JSON string. It is held in memory for the lifetime of the file system and grants whatever the service account is allowed to do across the project, not only the bucket in use. Scope the service account to the bucket that is actually needed.
