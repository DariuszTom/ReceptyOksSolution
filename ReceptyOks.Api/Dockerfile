# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files
COPY ReceptyOks.Api/ReceptyOks.Api.csproj ReceptyOks.Api/
COPY ReceptyOks.Shared/ReceptyOks.Shared.csproj ReceptyOks.Shared/
COPY ReceptyOksSolution.ServiceDefaults/ReceptyOksSolution.ServiceDefaults.csproj ReceptyOksSolution.ServiceDefaults/

# Restore dependencies
RUN dotnet restore ReceptyOks.Api/ReceptyOks.Api.csproj

# Copy source code
COPY ReceptyOks.Api/ ReceptyOks.Api/
COPY ReceptyOks.Shared/ ReceptyOks.Shared/
COPY ReceptyOksSolution.ServiceDefaults/ ReceptyOksSolution.ServiceDefaults/

# Build and publish
WORKDIR /src/ReceptyOks.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Create persistent data directory OUTSIDE /app for SQLite database
# This should be mounted as a Docker volume to persist data across deployments
RUN mkdir -p /data

EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .

# Declare /data as a volume mount point for database persistence
VOLUME ["/data"]

# Running the container will serve HTTP on ports 8080 and 8081. TLS should be terminated by a reverse proxy
# or by mounting a certificate and configuring Kestrel at runtime if container-level HTTPS is required.

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080;http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ReceptyOks.Api.dll"]
