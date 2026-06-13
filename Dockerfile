# Multi-stage build for ContosoDashboard (.NET 10)
# ─────────────────────────────────────────────────────────────────────────────
# Build stage: SDK image with all build tools
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj first for layer caching (restore only when deps change)
COPY ContosoDashboard/ContosoDashboard.csproj ./ContosoDashboard/
RUN dotnet restore ./ContosoDashboard/ContosoDashboard.csproj

# Copy everything else and publish
COPY . .
WORKDIR /src/ContosoDashboard
RUN dotnet publish -c Release -o /app --no-restore

# ─────────────────────────────────────────────────────────────────────────────
# Runtime stage: minimal ASP.NET Core image
# ─────────────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Install curl for health check (not present in minimal aspnet image)
USER root
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Non-root user for security (default UID 1000)
USER app

# Environment: ASP.NET Core defaults to ports 8080 (HTTP) and 8081 (HTTPS)
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose HTTP port
EXPOSE 8080

# Copy published app from build stage
COPY --from=build --chown=app:app /app .

# Runtime volumes for persistence across container restarts
# - Uploaded documents (mounted outside wwwroot per security requirements)
# - SQLite database lives under /app/data/ (mount the directory, not the file)
VOLUME ["/app/AppData/uploads", "/app/data"]

# Health check: hits the /health endpoint (lightweight, no auth required)
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "ContosoDashboard.dll"]