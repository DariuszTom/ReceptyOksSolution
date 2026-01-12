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

# Create data directory for SQLite database
RUN mkdir -p /app/Data

EXPOSE 8080
EXPOSE 8081

COPY --from=build /app/publish .
COPY https/aspnetapp.pfx /https/aspnetapp.pfx
# Set environment variables
ENV ASPNETCORE_URLS=https://+:8081;http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTPS_PORTS=8081

# Certificate configuration - mount your certificate or use these env vars
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=

ENTRYPOINT ["dotnet", "ReceptyOks.Api.dll"]
