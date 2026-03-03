# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder

WORKDIR /app

# Copy project files
COPY HaloPsaMcp.csproj ./
COPY Directory.Build.props ./

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . ./

# Build and publish the application
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# Install curl for health checks (as root before switching user)
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN groupadd -g 1001 dotnet && useradd -r -u 1001 -g dotnet dotnet

WORKDIR /app

# Copy published application
COPY --from=builder /app/publish ./

# Create data directory for token storage
RUN mkdir -p /app/data && chown -R dotnet:dotnet /app/data

USER dotnet

EXPOSE 3000

VOLUME ["/app/data"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:3000/health || exit 1

# Run in HTTP mode for production (OAuth + MCP endpoints)
ENTRYPOINT ["dotnet", "HaloPsaMcp.dll", "--http"]
