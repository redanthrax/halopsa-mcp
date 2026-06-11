# Changelog

All notable changes to this project are documented here. Version numbers match [GitHub Releases](https://github.com/redanthrax/halopsa-mcp/releases) and Docker Hub semver tags (`redanthrax/halopsa-mcp:<version>`).

## [Unreleased]

### Changed
- Docker builds use NuGet lockfile restore (`--locked-mode`) to match CI
- Helm chart version synced with application releases
- Trivy scan results published to GitHub Actions job summary

### Fixed
- Alpine OpenSSL CVE-2026-45447 via `apk upgrade` in runtime image

## [1.0.23] - 2026-06-11

### Security
- Encrypt Redis session payloads at rest
- Session revocation API wired through `ITokenStore`
- API path guard restricts `halopsa_api_call` to `/api/` GET/POST/PUT
- HTTP startup guards require DCR initial access token in production modes
- Log redaction for HaloPSA upstream responses
- CI: Trivy image scan on PRs; release workflow scans before push
- Pin GitHub Actions to commit SHAs; Helm supports `image.digest`

### Added
- Pluggable token stores: file (default) and Redis (HA)
- Desktop stdio setup tool and auto-login on MCP connect
- OSS documentation: LICENSE, CONTRIBUTING, SECURITY

## [1.0.19] - earlier

Initial public semver releases with OAuth 2.1, SQL guard, Helm chart, timesheet tools, and schema catalog.

[Unreleased]: https://github.com/redanthrax/halopsa-mcp/compare/v1.0.23...HEAD
[1.0.23]: https://github.com/redanthrax/halopsa-mcp/releases/tag/v1.0.23
[1.0.19]: https://github.com/redanthrax/halopsa-mcp/releases/tag/v1.0.19
