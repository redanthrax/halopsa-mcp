# HaloPSA MCP Server

MCP server for HaloPSA — query tickets, agents, clients, and reporting data via SQL or REST APIs. Full read/write support for tickets, actions, and timesheets.

## Features

- OAuth 2.1 authentication with PKCE
- Dual-mode: stdio (desktop MCP client) + Streamable HTTP (remote MCP client / production)
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
| **Local / Stdio** | desktop MCP client on your laptop | stdin/stdout + background OAuth on `:3000` | Single user, single HaloPSA tenant |
| **Docker** | Self-hosted, single host | Streamable HTTP on `:3000` | DCR optional; bind to localhost or trusted LAN |
| **AKS / Kubernetes** | Internet-exposed multi-user MCP server (remote MCP client) | Streamable HTTP behind ingress + TLS | DCR gated, NetworkPolicy, HSTS, read-only rootfs |

Use the same image/binary for all three; only environment variables and surrounding infra change.

## 1. Local Dev (desktop MCP client / Stdio)

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

3. Configure desktop MCP client (`%APPDATA%\MCP client\mcp host config`):

   > The MCP host's `env` block does not pass through `wsl.exe`. Put env vars in `.env`.

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

4. Restart desktop MCP client. The first call prints a login URL; open it in a browser.

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
  redanthrax/halopsa-mcp:latest
```

If you expose this to the public internet, also set `MCP_DCR_INITIAL_ACCESS_TOKEN` so `/register` is gated, and front it with a TLS-terminating reverse proxy (Caddy, Traefik, nginx).

## 3. AKS / Kubernetes (Helm)

The Helm chart at `helm/halopsa-mcp/` ships with hardened defaults: read-only root filesystem, `seccompProfile: RuntimeDefault`, no auto-mounted SA token, optional NetworkPolicy + PodDisruptionBudget, optional `MCP_DCR_INITIAL_ACCESS_TOKEN` from a generated Secret.

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
- [ ] `networkPolicy.enabled=true`
- [ ] `dcrInitialAccessToken` set (rotate on schedule)
- [ ] `image.tag` pinned to a SemVer or digest, not `latest`
- [ ] Image pulled from your private ACR (mirror from Docker Hub)
- [ ] DataProtection keys backed by Azure Key Vault (see Limitations)
- [ ] `halopsa.publicBaseUrl` set to the externally reachable URL (e.g., `https://your-domain.com`)

### Helm Values Reference

| Key | Default | Purpose |
|-----|---------|---------|
| `replicaCount` | `1` | Increase when `halopsa.tokenStore.backend=redis`. |
| `halopsa.tokenStore.backend` | `file` | `file` for single replica; `redis` for HA. |
| `halopsa.redisConnection` | `""` | StackExchange.Redis connection string when backend is `redis`. |
| `image.tag` | `""` (Chart.appVersion) | Pin to a SemVer or `@sha256:...` digest. |
| `serviceAccount.create` | `true` | |
| `serviceAccount.automountServiceAccountToken` | `false` | App does not call the K8s API. |
| `securityContext.readOnlyRootFilesystem` | `true` | Writable paths are PVC + tmpfs `/tmp`. |
| `podSecurityContext.seccompProfile.type` | `RuntimeDefault` | |
| `networkPolicy.enabled` | `false` | Set true; defaults allow ingress-nginx → pod and egress to DNS / `:443` / IMDS only. |
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
| `HALOPSA_DPKEY_DIR` | `./data/dp-keys` | DataProtection key ring |
| `HTTP_PORT` | `3000` | Listener port |
| `AUTH_BASE_URL` | `http://localhost:3000` | External base URL — used in OAuth callbacks and `WWW-Authenticate` |
| `HALOPSA_PUBLIC_URL` | `${AUTH_BASE_URL}` | Public base URL for login links (defaults to `AUTH_BASE_URL`) |
| `HALOPSA_REDIRECT_URI` | `${AUTH_BASE_URL}/callback` | Override only if callback path differs |
| `MCP_DCR_INITIAL_ACCESS_TOKEN` | _(unset)_ | When set, `/register` requires `Authorization: Bearer <token>`. **Set this for any internet-exposed deployment.** |
| `MCP_READY_VERBOSE` | `0` | `1` exposes detailed `/ready` JSON for trusted scrapers |
| `LOG_FORMAT` | `json` | `text` for human-readable console |
| `HALOPSA_SCHEMA_PATH` | `./schema` | Override schema catalog dir |

## Modes

| Mode | Use Case | Command | Transport |
|------|----------|---------|-----------|
| **Stdio** (default) | desktop MCP client local | `dotnet run` | stdin/stdout + background HTTP on port 3000 for OAuth |
| **HTTP** | remote MCP client / production | `dotnet run -- --http` | Streamable HTTP on port 3000 |

## Security Posture

- OAuth 2.1 Authorization Code + PKCE
- Distinct rotating refresh tokens (`mcr_*`); bearer (`mcp_*`) stable across rotation
- Dynamic Client Registration optionally gated by Initial Access Token
- SQL allowlist (`SqlGuard`) on `halopsa_query`: only `SELECT`/`WITH … SELECT`, no comments/`;`/DDL/DML/EXEC, 8000-char cap
- HSTS + ForwardedHeaders enabled in HTTP mode
- Tokens + DCR registrations encrypted at rest via ASP.NET DataProtection
- HaloPSA error response bodies redacted from logs
- Container: non-root UID 1001, no curl/wget, K8s probes only

## Limitations

- **File backend is single-replica.** Default `HALOPSA_TOKEN_STORE_BACKEND=file` uses a local encrypted JSON file + RWO PVC. For `replicaCount > 1`, set `halopsa.tokenStore.backend=redis` and provide `HALOPSA_REDIS_CONNECTION` (private Redis, TLS recommended).
- **DataProtection keys on PVC.** Surviving cluster rebuilds requires backing up the PVC or wrapping with Azure Key Vault.
- **Single HaloPSA tenant per deployment.**

## Available Tools

### Core

| Tool | Description |
|------|-------------|
| `halopsa_query` | **Primary tool.** SQL SELECT against reporting database. Best for counts, aggregation, date filtering, and satisfaction survey analysis. All datetimes are UTC — convert local times before querying. |
| `halopsa_get_schema` | Returns table/column names, live status IDs, agent IDs, and example queries. Call before writing SQL. |
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
| `halopsa_api_call` | Make a direct HaloPSA REST call against an arbitrary endpoint. Use for endpoints not covered by the typed tools. |

## Troubleshooting

**"NOT AUTHENTICATED"**:
1. For remote MCP client: re-add the integration or start a new chat
2. For desktop MCP client: MCP client will provide a login URL — open it in your browser to authenticate

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

See [CONTRIBUTING.md](CONTRIBUTING.md). Security issues: [SECURITY.md](SECURITY.md).

## License

MIT — see [LICENSE](LICENSE).
