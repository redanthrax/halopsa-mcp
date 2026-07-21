# Build stage
# Pin SDK patch to match global.json (10.0.301) so dotnet publish resolves reliably in CI.
FROM mcr.microsoft.com/dotnet/sdk:10.0.301 AS builder

WORKDIR /app

# Copy project files (lockfile keeps Docker builds aligned with CI)
COPY HaloPsaMcp.csproj Directory.Build.props packages.lock.json ./

# Restore dependencies
RUN dotnet restore --locked-mode

# Copy source code
COPY . ./

# Build and publish the application
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage — alpine cuts ~100 MB vs the default debian image.
# Use patched runtime train to pick up .NET 10.0.10 security fixes.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-alpine

# Apply Alpine security updates (base image may lag behind apk index).
RUN apk upgrade --no-cache

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
