# Build stage
# Digest pinned to multi-arch manifest list for mcr.microsoft.com/dotnet/sdk:10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:dc8430e6024d454edadad1e160e1973be3cabbb7125998ef190d9e5c6adf7dbb AS builder

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

# Runtime stage — alpine cuts ~100 MB vs the default debian image
# Digest pinned to multi-arch manifest list for mcr.microsoft.com/dotnet/aspnet:10.0-alpine
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:f03685b2735e0d3d25d6c60672e74b21bb6334f1402f71bae2d2cf02307163cd

# Non-root user (alpine has addgroup/adduser, not groupadd/useradd)
RUN addgroup -g 1001 -S dotnet && adduser -S -u 1001 -G dotnet dotnet

WORKDIR /app
COPY --from=builder /app/publish ./

# Token storage volume mount point
RUN mkdir -p /app/data && chown -R dotnet:dotnet /app/data

USER dotnet

# Default to JSON-formatted logs in containerized deployments. Override
# with LOG_FORMAT=text for human-readable console output during debugging.
ENV LOG_FORMAT=json

EXPOSE 3000
VOLUME ["/app/data"]

# Health is exposed on /health and /ready; rely on Kubernetes probes
# (helm chart configures liveness/readiness). No in-image HEALTHCHECK
# avoids shipping curl/wget and keeps the image attack surface minimal.

ENTRYPOINT ["dotnet", "HaloPsaMcp.dll", "--http"]
