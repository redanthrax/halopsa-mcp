# HaloPSA MCP Server

[![CI](https://github.com/redanthrax/halopsa-mcp/actions/workflows/ci.yml/badge.svg)](https://github.com/redanthrax/halopsa-mcp/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

MCP server for [HaloPSA](https://halopsa.com/) — query tickets, agents, clients, and reporting data via SQL or REST APIs. Full read/write support for tickets, actions, and timesheets.

> **Disclaimer:** This is an independent open-source project. It is not affiliated with, endorsed by, or supported by HaloPSA or NinjaOne.

See [CHANGELOG.md](CHANGELOG.md) for release history. Docker images are published as semver tags on [Docker Hub](https://hub.docker.com/r/redanthrax/halopsa-mcp) (`redanthrax/halopsa-mcp:<version>` — no `latest` tag).

## Features

- OAuth 2.1 authentication with PKCE
- Dual-mode: stdio (desktop MCP host) + Streamable HTTP (remote MCP clients / production)
- Stdio mode includes a background OAuth server — authenticate without restarting
- SQL query tool for efficient counts, aggregation, and date-based filtering
- Live schema with status IDs, agent IDs, and query examples
- Automatic token refresh
- Response trimming to minimize context usage
- Full ticket lifecycle: create, read, update, add actions
- Timesheet management: day records, time entries, submit/approve workflows

## HaloPSA Setup

Create an API application in HaloPSA before configuring the MCP server.

### Recommended: Authorisation Code (Native Application)

This is the recommended setup — uses OAuth 2.1 Authorization Code + PKCE with no client secret.

1. Go to **Configuration → Integrations → HaloPSA API → Applications → New**
2. Set **Authentication Method** to **Authorisation Code (Native Application)**
3. Set **Login Redirect URL** to `http://localhost:3000/callback` (add additional URLs for production, e.g. `https://your-domain.com/callback`)
4. Enable **Allow Agent Logins**
5. Leave **Client Secret** blank
6. Save and copy the **Client ID**

### Alternative: Client ID and Secret (Services)

Use this if your HaloPSA instance requires confidential clients or you need machine-to-machine access without user login.

1. Go to **Configuration → Integrations → HaloPSA API → Applications → New**
2. Set **Authentication Method** to **Client ID and Secret (Services)**
3. Set **Login Redirect URL** to `http://localhost:3000/callback`
4. Enable **Allow Agent Logins**
5. Save and copy both the **Client ID** and **Client Secret**
6. Add `HALOPSA_CLIENT_SECRET=your-secret` to your `.env` file

> **Note:** Do not use **Implicit Flow (Single Page Application)** — it is deprecated in OAuth 2.1 and less secure than Authorization Code + PKCE.

## Deployment Modes

| Mode | Audience | Transport | Auth surface |
|------|----------|-----------|--------------|
| **Local / Stdio** | Desktop MCP host on your laptop | stdin/stdout + background OAuth on `:3000` | Single user, single HaloPSA tenant |
| **Docker** | Self-hosted, single host | Streamable HTTP on `:3000` | DCR optional; bind to localhost or trusted LAN |
| **AKS / Kubernetes** | Internet-exposed multi-user MCP server | Streamable HTTP behind ingress + TLS | DCR gated, NetworkPolicy, HSTS, read-only rootfs |

Use the same image/binary for all three; only environment variables and surrounding infra change.

## 1. Local Dev (Desktop Stdio)

1. Create `.env` (copy `.env.example`):
   ```bash
   HALOPSA_URL=https://your-tenant.halopsa.com
   HALOPSA_CLIENT_ID=your-client-id
   HALOPSA_TOKEN_STORE=./data/tokens.json
   HALOPSA_DPKEY_DIR=./data/dp-keys
   AUTH_BASE_URL=http://localhost:3000
   LOG_FORMAT=text
   ```

2. Build:
   ```bash
   dotnet build
   ```

3. Configure your MCP host's stdio entry (example config shape):

   > The host's `env` block does not pass through `wsl.exe`. Put env vars in `.env`.

   **WSL (recommended):**
   ```json
   {
     "mcpServers": {
       "halopsa": {
         "command": "wsl.exe",
         "args": [
           "--distribution", "Ubuntu",
           "bash", "-c",
           "cd /path/to/halopsa-mcp && exec ./bin/Debug/net10.0/HaloPsaMcp"
         ]
       }
     }
   }
   ```

   **Native Windows:**
   ```json
   {
     "mcpServers": {
       "halopsa": {
         "command": "dotnet",
         "args": ["run", "--project", "C:\\path\\to\\halopsa-mcp"]
       }
     }
   }
   ```

4. Restart your MCP host.

5. On first connect, if you are not signed in, the MCP server **opens the login page in your browser** and sends **server instructions** to the host to prompt sign-in. Complete HaloPSA sign-in, then retry in the host — no restart needed. Set `HALOPSA_AUTO_OPEN_LOGIN=0` to disable auto-open.

## 2. Docker (Self-hosted HTTP)

The shipped `docker-compose.yml` runs the image with hardened defaults: non-root UID 1001, `read_only: true`, `cap_drop: ALL`, `no-new-privileges`, tmpfs `/tmp`, and a single `./data` bind mount for tokens + DataProtection keys.

```bash
cp .env.example .env   # fill in HALOPSA_URL + HALOPSA_CLIENT_ID
docker compose up -d
```

For a one-shot run without compose:
```bash
docker run -d \
  --name halopsa-mcp \
  --user 1001:1001 \
  --read-only --tmpfs /tmp \
  --cap-drop ALL --security-opt no-new-privileges:true \
  -p 3000:3000 \
  -v ./data:/app/data \
  -e HALOPSA_URL=https://your-tenant.halopsa.com \
  -e HALOPSA_CLIENT_ID=your-client-id \
  -e AUTH_BASE_URL=https://your-domain.com \
  -e HALOPSA_DPKEY_DIR=/app/data/dp-keys \
  redanthrax/halopsa-mcp:1.0.23
```

> Pin to a specific semver tag from [GitHub Releases](https://github.com/redanthrax/halopsa-mcp/releases) or use `image.digest` in Kubernetes.

If you expose this to the public internet, front it with a TLS-terminating reverse proxy (Caddy, Traefik, nginx). For Claude.ai org connectors, leave `MCP_DCR_INITIAL_ACCESS_TOKEN` unset (or set `MCP_ALLOW_OPEN_DCR=1`) and configure `MCP_CORS_ALLOWED_ORIGINS` — see `helm/halopsa-mcp/values-claude-connector.example.yaml`.

## 3. AKS / Kubernetes (Helm)

The Helm chart at `helm/halopsa-mcp/` ships with hardened defaults: read-only root filesystem, `seccompProfile: RuntimeDefault`, no auto-mounted SA token, **NetworkPolicy enabled by default**, PodDisruptionBudget, and a generated Secret for `dcrInitialAccessToken` / client secret / Redis connection.

```bash
helm upgrade --install halopsa-mcp helm/halopsa-mcp/ \
  --namespace halopsa-mcp --create-namespace \
  --set halopsa.url=https://your-tenant.halopsa.com \
  --set halopsa.clientId=your-client-id \
  --set halopsa.authBaseUrl=https://halopsa-mcp.example.com \
  --set halopsa.publicBaseUrl=https://halopsa-mcp.example.com \
  --set ingress.enabled=true \
  --set ingress.hosts[0].host=halopsa-mcp.example.com \
  --set networkPolicy.enabled=true \
  --set dcrInitialAccessToken=$(openssl rand -hex 32)
```

For a confidential client (Client ID + Secret method) provide the secret without committing it:
```bash
--set-file halopsa.clientSecret=./halopsa-client-secret.txt
```

For multiple replicas (HA), use a shared Redis session store:
```bash
--set replicaCount=3 \
--set halopsa.tokenStore.backend=redis \
--set halopsa.redisConnection='redis-master:6379,password=...,ssl=true'
```

Production checklist:
- [ ] TLS terminated by ingress (cert-manager or Azure Application Gateway)
- [ ] `networkPolicy.enabled=true` (adjust ingress namespace if not using ingress-nginx)
- [ ] `AUTH_BASE_URL` / `halopsa.authBaseUrl` matches public connector URL
- [ ] HaloPSA Login Redirect URL includes `https://<host>/callback`
- [ ] Claude connector: open DCR + CORS (`values-claude-connector.example.yaml`)
- [ ] `image.digest` set to `sha256:...` (preferred) or `image.tag` pinned to a SemVer — not `latest`
- [ ] Image pulled from your private ACR (mirror from Docker Hub)
- [ ] DataProtection keys backed by Azure Key Vault (see Limitations)
- [ ] `halopsa.publicBaseUrl` set to the externally reachable URL (e.g., `https://your-domain.com`)

### Helm Values Reference

| Key | Default | Purpose |
|-----|---------|---------|
| `replicaCount` | `1` | Increase when `halopsa.tokenStore.backend=redis`. |
| `halopsa.tokenStore.backend` | `file` | `file` for single replica; `redis` for HA. |
| `halopsa.redisConnection` | `""` | StackExchange.Redis connection string when backend is `redis`. |
| `image.digest` | `""` | **Production:** `sha256:...` from release attestation or `docker buildx imagetools inspect`. |
| `image.tag` | `""` (Chart.appVersion) | Used when `image.digest` is empty. |
| `serviceAccount.create` | `true` | |
| `serviceAccount.automountServiceAccountToken` | `false` | App does not call the K8s API. |
| `securityContext.readOnlyRootFilesystem` | `true` | Writable paths are PVC + tmpfs `/tmp`. |
| `podSecurityContext.seccompProfile.type` | `RuntimeDefault` | |
| `networkPolicy.enabled` | `true` | Restricts ingress to the release namespace and egress to DNS / `:443` / IMDS. Set `false` only for constrained clusters. |
| `podDisruptionBudget.enabled` | `false` | Only useful with replicaCount > 1. |
| `dcrInitialAccessToken` | `""` | When set, gates `/register`. |
| `halopsa.dpKeyDir` | `/app/data/dp-keys` | DataProtection keys directory. |
| `logFormat` | `json` | Use `text` only for debugging. |
| `readyVerbose` | `false` | Set true for trusted in-cluster scrapers only. |
| `persistence.enabled` | `true` | RWO PVC for tokens + DP keys. |

## Configuration Reference

### Required everywhere
| Env | Purpose |
|-----|---------|
| `HALOPSA_URL` | HaloPSA tenant URL |
| `HALOPSA_CLIENT_ID` | OAuth client ID |

### Optional
| Env | Default | Purpose |
|-----|---------|---------|
| `HALOPSA_CLIENT_SECRET` | _(unset)_ | For "Client ID + Secret" method only |
| `HALOPSA_TOKEN_STORE_BACKEND` | `file` | `file` (default) or `redis` for multi-replica HTTP |
| `HALOPSA_TOKEN_STORE` | `./data/tokens.json` | Encrypted token store path (file backend) |
| `HALOPSA_REDIS_CONNECTION` | _(unset)_ | Redis connection string when backend is `redis` |
| `HALOPSA_REDIS_CONNECTION_FILE` | _(unset)_ | Docker-style: read Redis connection from mounted file (preferred in K8s) |
| `HALOPSA_CLIENT_SECRET_FILE` | _(unset)_ | Read client secret from mounted file instead of env |
| `MCP_DCR_INITIAL_ACCESS_TOKEN_FILE` | _(unset)_ | Read DCR initial access token from mounted file |
| `MCP_ENABLED_TOOLS` | _(all tools)_ | Comma-separated allowlist, e.g. `halopsa_query,halopsa_list_tickets,halopsa_list_agents` |
| `MCP_METRICS_ENABLED` | `0` | Set `1` to expose gated `/metrics` (Prometheus text format) |
| `MCP_METRICS_TOKEN` | _(unset)_ | Optional bearer token required for `/metrics` |
| `MCP_HTTP_STATELESS` | auto | `1`/`0`/`auto` — stateless Streamable HTTP (no `Mcp-Session-Id` affinity). **Auto: enabled when `HALOPSA_TOKEN_STORE_BACKEND=redis`.** Required for multi-replica + server-to-server MCP callers (Claude.ai org connector). |
| `MCP_HTTP_IDLE_TIMEOUT_MINUTES` | `120` | Stateful mode only: idle session TTL before 404 |
| `HALOPSA_DPKEY_DIR` | `./data/dp-keys` | DataProtection key ring |
| `HTTP_PORT` | `3000` | Listener port |
| `AUTH_BASE_URL` | `http://localhost:3000` | External base URL — used in OAuth callbacks and `WWW-Authenticate` |
| `HALOPSA_PUBLIC_URL` | `${AUTH_BASE_URL}` | Public base URL for login links (defaults to `AUTH_BASE_URL`) |
| `HALOPSA_REDIRECT_URI` | `${AUTH_BASE_URL}/callback` | Override only if callback path differs |
| `MCP_DCR_INITIAL_ACCESS_TOKEN` | _(unset)_ | Optional. When set, gates `/register` (DCR) unless `MCP_ALLOW_OPEN_DCR=1`. Advertised in authorization-server metadata. |
| `MCP_ALLOW_OPEN_DCR` | _(unset)_ | `1` = allow unauthenticated DCR (required for Claude.ai org connectors). Rate-limited. |
| `MCP_CORS_ALLOWED_ORIGINS` | `https://claude.ai` | Comma-separated browser origins allowed for MCP/OAuth CORS (required for Claude.ai connectors). |
| `HTTP_BIND_ALL` | _(unset)_ | `1` = bind stdio OAuth to all interfaces (default: localhost only) |
| `TRUSTED_PROXY_CIDRS` | RFC1918 private | Comma-separated CIDRs for `X-Forwarded-*`; `none` disables |
| `MCP_READY_VERBOSE` | `0` | `1` exposes detailed `/ready` JSON for trusted scrapers |
| `LOG_FORMAT` | `json` | `text` for human-readable console |
| `HALOPSA_SCHEMA_PATH` | `./schema` | Override schema catalog dir |

## Modes

| Mode | Use Case | Command | Transport |
|------|----------|---------|-----------|
| **Stdio** (default) | Desktop MCP host (local) | `dotnet run` | stdin/stdout + background HTTP on port 3000 for OAuth |
| **HTTP** | Remote MCP clients / production | `dotnet run -- --http` | Streamable HTTP on port 3000 |

## Security Posture

- OAuth 2.1 Authorization Code + PKCE
- Distinct rotating refresh tokens (`mcr_*`); bearer (`mcp_*`) stable across rotation
- Dynamic Client Registration optionally gated by Initial Access Token
- SQL allowlist (`SqlGuard`) on `halopsa_query`: only `SELECT`/`WITH … SELECT`, no comments/`;`/DDL/DML/EXEC, 8000-char cap
- HSTS + ForwardedHeaders enabled in HTTP mode
- Tokens + DCR registrations encrypted at rest via ASP.NET DataProtection
- OAuth/DCR state shared across replicas via Redis (or file + watcher for best-effort HA)
- Structured `tool_audit` log line per MCP tool call (`user`, `tool`, `args_hash`, `traceId`, `status`)
- Per-deployment tool allowlist via `MCP_ENABLED_TOOLS`
- Gated `/metrics` endpoint when `MCP_METRICS_ENABLED=1`
- HaloPSA error response bodies redacted from logs
- Container: non-root UID 1001, no curl/wget, K8s probes only

## Limitations

- **File backend is best-effort multi-replica.** Default `HALOPSA_TOKEN_STORE_BACKEND=file` uses a local encrypted JSON file + RWO PVC with FileSystemWatcher reload. OAuth flow state and DCR registrations follow the same pattern. For `replicaCount > 1`, set `halopsa.tokenStore.backend=redis` and provide `HALOPSA_REDIS_CONNECTION` (or `HALOPSA_REDIS_CONNECTION_FILE`) so sessions, OAuth state, and DCR are atomically shared.
- **MCP Streamable HTTP sessions are process-local in the SDK** unless you enable stateless mode. With `replicaCount > 1`, set `MCP_HTTP_STATELESS=1` (auto when `backend=redis`) so each `POST /mcp` is handled independently — this matches how Claude.ai's connector backend calls tools without cookie affinity. For stateful mode (`MCP_HTTP_STATELESS=0`), use nginx `upstream-hash-by: $http_mcp_session_id` or scale to one replica.
- **DataProtection keys on PVC.** Surviving cluster rebuilds requires backing up the PVC or wrapping with Azure Key Vault.
- **Single HaloPSA tenant per deployment.**

## Threat Model

This server is designed for **operator-controlled deployments** where the organization trusts its own infrastructure but must constrain what AI clients can do against HaloPSA.

**Trust assumptions**

- **OAuth user consent is the primary gate.** Any MCP client that completes Dynamic Client Registration and drives a user through HaloPSA login receives a session scoped to that user's HaloPSA permissions. DCR is rate-limited; optionally gate it with `MCP_DCR_INITIAL_ACCESS_TOKEN` for private deployments.
- **The MCP server inherits HaloPSA authorization.** Tools call HaloPSA as the authenticated user. Provision a dedicated low-privilege OAuth application for shared-team or org-wide connectors rather than reusing a super-admin app.
- **SqlGuard enforces read-only SQL by design.** `halopsa_query` accepts only `SELECT` / `WITH … SELECT` against the reporting database — no DDL, DML, comments, or multi-statement batches. Other tools use typed REST endpoints; restrict surface area with `MCP_ENABLED_TOOLS`.
- **Secrets should use `*_FILE` mounts in production.** Prefer `HALOPSA_REDIS_CONNECTION_FILE` and `MCP_DCR_INITIAL_ACCESS_TOKEN_FILE` over plain env vars so values never appear in `/proc/<pid>/environ` or `kubectl describe`.
- **Redirect URIs are normalized at registration** (lowercase host, default port stripped, trailing slash removed) so `/cb` and `/cb/` match consistently.

**Out of scope / residual risk**

- A malicious but authenticated user can invoke every enabled tool with their own HaloPSA rights (by design).
- File-backend multi-replica sync is eventually consistent; use Redis for strict HA.
- Container images are signed with cosign (keyless, Sigstore) and include SPDX SBOMs in the OCI manifest; CycloneDX SBOMs are attached to GitHub release builds for admission-policy verification (Ratify/Kyverno).

## Supply Chain

Release tags (`v*`) trigger `.github/workflows/docker-build.yml`, which:

1. Builds multi-arch images (`linux/amd64`, `linux/arm64`)
2. Attaches an SPDX SBOM to the OCI manifest (`sbom: true`)
3. Signs with cosign keyless (GitHub OIDC → Sigstore)
4. Publishes a CycloneDX SBOM artifact for offline verification

Verify at deploy time:

```bash
cosign verify redanthrax/halopsa-mcp:<version> --certificate-identity-regexp='.*' --certificate-oidc-issuer-regexp='.*'
```

## Available Tools

### Core

| Tool | Description |
|------|-------------|
| `halopsa_query` | **Primary tool.** SQL SELECT against reporting database. Best for counts, aggregation, date filtering, and satisfaction survey analysis. All datetimes are UTC — convert local times before querying. |
| `halopsa_get_schema` | Returns table/column names, live status IDs, agent IDs, and example queries. Call before writing SQL. |
| `halopsa_setup` | **Desktop stdio.** Check local setup, session status, login URL, and troubleshooting steps. Call when installing or if login fails. |
| `halopsa_auth_status` | Check current authentication status. Call first for any HaloPSA request. |
| `halopsa_whoami` | Decode the access token and return granted scopes plus identity claims (`agent_id`, `role`, `client_id`, etc). Useful right after login. |
| `halopsa_capabilities` | Probe HaloPSA endpoints to discover which permissions/scopes the active token actually has. Returns an allow/deny map per capability. |
| `halopsa_list_contracts` | List `/api/ClientContract` records (budget/usage hours). Filter by `clientId` or `search`. Use when the reporting DB is unavailable. |

### Tickets

| Tool | Description |
|------|-------------|
| `halopsa_list_tickets` | Search tickets by keyword or filters (`count`, `status`, `clientId`, `agentId`, `search`). Returns summary fields. |
| `halopsa_get_ticket` | Get full details for a specific ticket by ID. |
| `halopsa_create_ticket` | Create a new ticket (`summary`, `details`, `clientId`, `agentId`, `statusId`, `priorityId`, `ticketTypeId`, `siteId`). Use 0 for optional fields to use defaults. |
| `halopsa_update_ticket` | Update an existing ticket by ID. Use 0 for optional fields to leave unchanged. |

### Actions

| Tool | Description |
|------|-------------|
| `halopsa_list_actions` | List notes/updates for a specific ticket (`ticketId`, `count`). Returns summary fields. |
| `halopsa_add_action` | Add a note/action to a ticket. Requires `ticketId`, `outcomeId` (get from `halopsa_get_outcomes`), optional `note`, `timeTaken`, `newStatus`, `hiddenFromUser`. |
| `halopsa_get_outcomes` | Get valid outcome IDs for use with `halopsa_add_action`. |

### Timesheets

| Tool | Description |
|------|-------------|
| `halopsa_get_timesheet` | Get a timesheet day record by ID, including all time entries. Returns id=0 if no record exists for that date — create one with `halopsa_create_timesheet`. |
| `halopsa_create_timesheet` | Create a new timesheet day record for an agent (`agentId`, `date`, optional `startTime`, `endTime`, `utcOffset`). |
| `halopsa_update_timesheet` | Update shift times or submit/approve a timesheet day record. Fetches current state and applies changes. Supports `startTime`, `endTime`, `submitApproval`, `approve`, `reject`, `revertApproval`. |
| `halopsa_list_timesheet_events` | List time entries for a date range (`startDate`, `endDate`, optional `agentId`). All datetimes must be UTC. |
| `halopsa_upsert_timesheet_event` | Create or update a time entry. Set `id=0` to create. Fields: `ticketId`, `agentId`, `startDate`, `endDate`, `timeTaken` (hours), `note`, `subject`, `clientId`, `siteId`. |
| `halopsa_delete_timesheet_event` | Delete a time entry by ID. Use `halopsa_list_timesheet_events` to find IDs first. |

### Catalog

| Tool | Description |
|------|-------------|
| `halopsa_list_clients` | List clients (companies). Filter by `search`. |
| `halopsa_get_client` | Get a single client by ID. |
| `halopsa_list_sites` | List sites for a `clientId`. |
| `halopsa_list_agents` | List internal agents. Filter by `search`. |
| `halopsa_list_users` | List end users (customer contacts). Filter by `clientId` or `search`. |

### Assets & Knowledge

| Tool | Description |
|------|-------------|
| `halopsa_list_assets` | List managed assets. Filter by `clientId` or `search`. |
| `halopsa_get_asset` | Get a single asset by ID. |
| `halopsa_list_kb_articles` | List knowledge base articles. Filter by `search`. |
| `halopsa_get_kb_article` | Get a single KB article by ID. |

### Reports & Surveys

| Tool | Description |
|------|-------------|
| `halopsa_list_reports` | List saved report definitions. |
| `halopsa_get_report_definition` | Get the parameters/SQL/layout of a report by ID. |
| `halopsa_run_report` | Run a saved report; pass `parameters` as a JSON object. |
| `halopsa_list_surveys` | List satisfaction survey responses. Filter by `ticketId`. |
| `halopsa_list_statuses` | List ticket statuses. Optional `type` filter. |
| `halopsa_list_request_types` | List request types (categories). |

### Projects & Scheduling

| Tool | Description |
|------|-------------|
| `halopsa_list_projects` | List projects. Filter by `clientId` or `search`. |
| `halopsa_list_opportunities` | List sales opportunities. Filter by `clientId` or `search`. |
| `halopsa_list_appointments` | List appointments. Filter by `agentId` and a UTC date range. |

### Database Catalog

The MCP server ships with an offline dump of the HaloPSA reporting database schema in `schema/catalog.json` (845 tables, 15 domains). These tools let the MCP client browse it cheaply when planning SQL — drill domains → tables → columns rather than dumping 3 MB into context.

| Tool | Description |
|------|-------------|
| `halopsa_db_domains` | List the 15 database domains with table counts and one-line descriptions. Call this first when planning a query. |
| `halopsa_db_tables` | List tables (filterable by `domain` and substring `search`) with row counts, primary keys, and FK targets. |
| `halopsa_db_columns` | Full column + FK detail for one table. Pass `columnSearch` to narrow on wide tables (FAULTS has 625 cols). |
| `halopsa_db_search` | Find tables/columns whose name contains a term. Use when you don't know which table holds a concept. |

The schema folder is auto-copied next to the binary at build/publish. Override the path with `HALOPSA_SCHEMA_PATH` if you keep it elsewhere.

### Escape Hatch

| Tool | Description |
|------|-------------|
| `halopsa_api_call` | Direct HaloPSA REST call for endpoints not covered by typed tools. Path must start with `/api/` (GET/POST/PUT only). |

## Troubleshooting

**"NOT AUTHENTICATED"**:
1. For desktop stdio: run `halopsa_setup` or open `http://localhost:3000/` — use the login URL in your browser
2. After signing in, retry in your MCP host (no restart required)
3. For remote HTTP clients: re-add the integration or start a new session
4. If port 3000 is in use, check MCP logs for the actual login URL (ephemeral port fallback)

**Query returns 0 rows**:
1. Call `halopsa_get_schema` first to verify table/column names
2. Check that datetime filters use UTC format
3. For satisfaction surveys, use the `feedback` table with `halopsa_query`
4. For timesheet and action analysis, use the relationship helpers in the schema
5. The Report API may require specific permissions in HaloPSA

**Timesheet record not found**:
1. Use `halopsa_query` to look up the timesheet ID: `SELECT TSid, TSunum, TSdate FROM timesheet WHERE TSunum = <agent_id> AND TSdate >= '2026-03-04T00:00:00Z'`
2. If no record exists for the date, call `halopsa_create_timesheet` before updating

## Table Relationships

The schema includes helpers for common table relationships:

- **faults.faultid** → **actions.faultid** (ticket actions and updates)
- **faults.faultid** → **timesheet.faultid** (work time logged against tickets)
- **faults.faultid** → **feedback.FBFaultID** (customer satisfaction surveys)
- **faults.Requesttype** → **requesttype.RTid** (request type configurations)

Use `halopsa_get_schema` to see example queries for joining these tables.

## Requirements

- .NET 10.0 SDK (for local development)
- HaloPSA instance with OAuth client registered

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Security issues: [SECURITY.md](SECURITY.md).

## License

MIT — see [LICENSE](LICENSE).
