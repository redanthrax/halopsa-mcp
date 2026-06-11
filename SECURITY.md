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

- **HTTP mode (`--http`) requires `MCP_DCR_INITIAL_ACCESS_TOKEN`** — startup fails without it unless `MCP_ALLOW_OPEN_DCR=1` (local Docker only)
- desktop MCP client stdio binds OAuth to **127.0.0.1** by default; set `HTTP_BIND_ALL=1` only if you understand the LAN exposure risk
- Set `TRUSTED_PROXY_CIDRS` behind ingress (defaults to RFC1918 private ranges); use `none` to disable forwarded headers
- Use `halopsa.tokenStore.backend=redis` when `replicaCount > 1`; keep Redis on a private network with TLS; store Redis credentials in Kubernetes Secrets
- Helm chart enables **NetworkPolicy** by default; provide `dcrInitialAccessToken` via chart Secret or `existingSecret`
- Pin Docker images to a SemVer tag or digest, not `latest`
- Rotate DataProtection keys and token stores according to your org policy
- HaloPSA upstream error bodies and full SQL are **not** written to Information-level logs or returned to clients
- Redis session payloads are **encrypted with DataProtection** before write (same key ring as file backend)
- `halopsa_api_call` is restricted to relative `/api/` paths (GET/POST/PUT only)
- NuGet dependencies are **lockfile-pinned** (`packages.lock.json`); CI restores with `--locked-mode`
- PR CI runs **Trivy** on a built Docker image; releases no longer publish a `latest` tag
