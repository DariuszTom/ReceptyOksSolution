# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ReceptyOks.Api/ReceptyOks.Api.csproj ReceptyOks.Api/
COPY ReceptyOks.Shared/ReceptyOks.Shared.csproj ReceptyOks.Shared/
COPY ReceptyOksSolution.ServiceDefaults/ReceptyOksSolution.ServiceDefaults.csproj ReceptyOksSolution.ServiceDefaults/
COPY HomeSeeker/HomeSeeker.csproj HomeSeeker/

# Restore dependencies
RUN dotnet restore ReceptyOks.Api/ReceptyOks.Api.csproj

# Copy source code
COPY ReceptyOks.Api/ ReceptyOks.Api/
COPY ReceptyOks.Shared/ ReceptyOks.Shared/
COPY ReceptyOksSolution.ServiceDefaults/ ReceptyOksSolution.ServiceDefaults/
COPY HomeSeeker/ HomeSeeker/

# Build and publish
WORKDIR /src/ReceptyOks.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

EXPOSE 8080

COPY --from=build /app/publish .

# HTTP only on 8080 (image default via ASPNETCORE_HTTP_PORTS). TLS is terminated by the ingress/reverse proxy.
ENV ASPNETCORE_ENVIRONMENT=Production

# Run as the built-in non-root user provided by the .NET images
USER $APP_UID

ENTRYPOINT ["dotnet", "ReceptyOks.Api.dll"]
