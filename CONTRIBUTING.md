# Contributing to HaloPSA MCP Server

Thanks for your interest in contributing. This project bridges [HaloPSA](https://halopsa.com/) to the [Model Context Protocol](https://modelcontextprotocol.io/) so MCP clients can query and manage PSA data safely.

Please read our [Code of Conduct](CODE_OF_CONDUCT.md) before participating.

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A HaloPSA tenant with an OAuth API application (see [README.md](README.md#halopsa-setup))

### Local setup

```bash
git clone https://github.com/redanthrax/halopsa-mcp.git
cd halopsa-mcp
cp .env.example .env   # fill in HALOPSA_URL and HALOPSA_CLIENT_ID
dotnet build
dotnet test
```

Stdio mode (desktop MCP client):

```bash
dotnet run
```

HTTP mode (production-like):

```bash
dotnet run -- --http
```

## Project layout

| Path | Purpose |
| ---- | ------- |
| `Modules/Authentication/` | OAuth 2.1, PKCE, DCR, encrypted token storage |
| `Modules/HaloPsa/` | API client, CQRS handlers, SQL guard |
| `Modules/Mcp/` | MCP tool definitions (`[McpServerTool]`) |
| `Modules/Common/` | Shared config, middleware |
| `tests/HaloPsaMcp.Tests/` | Unit tests (xUnit) |
| `schema/` | Offline reporting DB catalog for schema discovery tools |
| `helm/halopsa-mcp/` | Kubernetes deployment chart |

Handlers live in `Modules/HaloPsa/Handlers/` and are invoked by Wolverine from MCP tools. Prefer adding business logic in handlers rather than in the tool layer.

## Making changes

1. **Fork** the repository and create a branch from `master`.
2. **Keep scope focused** — one logical change per pull request.
3. **Match existing style** — nullable enabled, file-scoped namespaces where used, minimal comments.
4. **Add tests** for security-sensitive or non-trivial logic (`SqlGuard`, auth, SQL builders, handlers).
5. **Update README.md** when adding tools, env vars, or deployment changes.
6. **Run the test suite** before opening a PR:

   ```bash
   dotnet test tests/HaloPsaMcp.Tests/HaloPsaMcp.Tests.csproj --configuration Release
   ```

## Pull requests

- Describe the problem and your approach.
- Note any HaloPSA API quirks you discovered.
- Include a test plan (commands run, manual steps if applicable).
- Do not commit secrets (`.env`, tokens, client secrets).

CI runs build, tests, and a vulnerable NuGet package scan on every PR.

## Security

See [SECURITY.md](SECURITY.md) for vulnerability reporting. Do not open public issues for security bugs.

## Questions

Open a [GitHub Discussion](https://github.com/redanthrax/halopsa-mcp/discussions) or issue for questions that are not security-sensitive.
