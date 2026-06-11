# Security Policy

## Supported versions

Security fixes are applied to the latest release on the `master` branch and published as a new Docker image tag (`redanthrax/halopsa-mcp`).

| Version | Supported |
| ------- | --------- |
| Latest release | Yes |
| Older releases | Best effort — upgrade recommended |

## Reporting a vulnerability

**Please do not open a public GitHub issue for security vulnerabilities.**

Report security issues privately via [GitHub Security Advisories](https://github.com/redanthrax/halopsa-mcp/security/advisories/new) on this repository.

If you cannot use that form, email the maintainers through the contact method on the [redanthrax](https://github.com/redanthrax) GitHub profile.

Include as much detail as possible:

- Description of the issue and potential impact
- Steps to reproduce
- Affected deployment mode (stdio, Docker, Kubernetes)
- Any suggested fix or mitigation

We aim to acknowledge reports within **3 business days** and will coordinate disclosure once a fix is available.

## Security-sensitive areas

When reviewing or reporting issues, pay particular attention to:

- OAuth / PKCE flow and token storage (`Modules/Authentication/`)
- Dynamic Client Registration (`/register`) and `MCP_DCR_INITIAL_ACCESS_TOKEN`
- SQL forwarding (`SqlGuard`, `halopsa_query`)
- Multi-tenant session isolation in HTTP mode
- Container and Helm hardening defaults

## Safe deployment reminders

- Set `MCP_DCR_INITIAL_ACCESS_TOKEN` for any internet-exposed HTTP deployment
- Keep `replicaCount: 1` until a shared token backend is available
- Pin Docker images to a SemVer tag or digest, not `latest`
- Rotate DataProtection keys and token stores according to your org policy
