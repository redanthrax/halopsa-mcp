# HaloPSA MCP Server

MCP server for HaloPSA — query tickets, agents, clients, and reporting data via SQL or REST APIs.

## Features

- OAuth 2.1 authentication with PKCE
- Dual-mode: stdio (Claude Desktop) + Streamable HTTP (Claude.ai / production)
- Stdio mode includes a background OAuth server — authenticate without restarting
- SQL query tool for efficient counts, aggregation, and date-based filtering
- Live schema with status IDs, agent IDs, and query examples
- Automatic token refresh
- Response trimming to minimize context usage

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

## Quick Start

### Claude Desktop (Local / Stdio)

1. **Create `.env` file** in the project root (all configuration lives here):
```bash
HALOPSA_URL=https://your-tenant.halopsa.com
HALOPSA_CLIENT_ID=your-client-id
HALOPSA_TOKEN_STORE=./data/tokens.json
HTTP_PORT=3000
# Only needed for "Client ID and Secret" auth method:
# HALOPSA_CLIENT_SECRET=your-client-secret
```

2. **Build the project**:
```bash
dotnet build
```

3. **Configure Claude Desktop** (`%APPDATA%\Claude\claude_desktop_config.json`):

> **Note:** Claude Desktop's `env` block does not pass through `wsl.exe` to the Linux process. All environment variables must be set in the `.env` file instead.

**WSL (recommended for development):**
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

4. **Restart Claude Desktop** — Claude will prompt you with a login URL when authentication is needed

### Claude.ai (Remote MCP)

Add as a remote MCP integration in Claude.ai settings:

1. Go to **Settings → Integrations → Add Integration**
2. Enter your server URL: `https://your-domain.com/mcp`
3. Claude.ai handles OAuth automatically — you'll be prompted to log in via HaloPSA on first use

### Production (Docker / Kubernetes)

```bash
docker run -d \
  -p 3000:3000 \
  -v ./data:/app/data \
  -e HALOPSA_URL=https://your-tenant.halopsa.com \
  -e HALOPSA_CLIENT_ID=your-client-id \
  -e AUTH_BASE_URL=https://your-domain.com \
  redanthrax/halopsa-mcp:latest
```

Add `-e HALOPSA_CLIENT_SECRET=your-secret` if using the Client ID and Secret authentication method.

## Available Tools

| Tool | Description |
|------|-------------|
| `halopsa_query` | **Primary tool.** SQL SELECT against reporting database. Best for counts, aggregation, date filtering. |
| `halopsa_get_schema` | Returns table/column names, live status IDs, agent IDs, and example queries. Call before writing SQL. |
| `halopsa_list_tickets` | Search tickets by keyword or filters. Returns trimmed summary fields. |
| `halopsa_get_ticket` | Get full details for a specific ticket by ID. |
| `halopsa_list_actions` | List notes/updates for a specific ticket. Returns trimmed summary fields. |
| `halopsa_auth_status` | Check current authentication status. |

## Modes

| Mode | Use Case | Command | Transport |
|------|----------|---------|-----------|
| **Stdio** (default) | Claude Desktop local | `dotnet run` | stdin/stdout + background HTTP on port 3000 for OAuth |
| **HTTP** | Claude.ai / production | `dotnet run -- --http` | Streamable HTTP on port 3000 |

## Configuration

### Required
- `HALOPSA_URL` — Your HaloPSA instance URL (e.g. `https://your-tenant.halopsa.com`)
- `HALOPSA_CLIENT_ID` — OAuth client ID from HaloPSA

### Optional
- `HALOPSA_CLIENT_SECRET` — OAuth client secret (only for "Client ID and Secret" auth method)
- `HALOPSA_TOKEN_STORE` — Token file path for stdio mode (default: `./data/tokens.json`)
- `HTTP_PORT` — HTTP server port (default: `3000`)
- `AUTH_BASE_URL` — External URL for OAuth callbacks (default: `http://localhost:3000`)

## Troubleshooting

**"NOT AUTHENTICATED"**:
1. For Claude.ai: re-add the integration or start a new chat
2. For Claude Desktop: Claude will provide a login URL — open it in your browser to authenticate

**Query returns 0 rows**:
1. Call `halopsa_get_schema` first to verify table/column names
2. Check that datetime filters use UTC format
3. The Report API may require specific permissions in HaloPSA

## Requirements

- .NET 10.0 SDK (for local development)
- HaloPSA instance with OAuth client registered

## License

MIT
