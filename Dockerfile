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

# Runtime stage — alpine cuts ~100 MB vs the default debian image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine

# curl for HEALTHCHECK; cleaned via apk in same layer
RUN apk add --no-cache curl

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

# Use /ready so docker reports unhealthy if a critical dependency
# (token storage) fails. Schema catalog absence shows up as "degraded"
# in the body but still passes the probe.
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD curl -fsS http://localhost:3000/ready || exit 1

ENTRYPOINT ["dotnet", "HaloPsaMcp.dll", "--http"]
