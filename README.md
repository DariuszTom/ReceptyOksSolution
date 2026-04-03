# ReceptyOks Solution

[![.NET](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/dotnet.yml/badge.svg)](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/dotnet.yml)
[![CodeQL](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/codeql.yml/badge.svg)](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/codeql.yml)
[![Docker Build](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/docker-build.yml/badge.svg)](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/docker-build.yml)
[![Android Release](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/android-release.yml/badge.svg)](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/android-release.yml)
[![Secrets Scan](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/secrets-scan.yml/badge.svg)](https://github.com/DariuszTom/ReceptyOksSolution/actions/workflows/secrets-scan.yml)

A cross-platform recipe management application built with **.NET 10**, **.NET MAUI**, and **.NET Aspire 9.5**.

## 📋 Overview

ReceptyOks is a modern recipe management solution that allows users to create, organize, and sync recipes across devices. The application features:

- Cross-platform mobile/desktop client (Android, Windows)
- RESTful API backend with SQLite database
- Offline-first architecture with sync capabilities
- OCR support for extracting text from recipe images
- Rich text editing for recipe instructions using Blazor Hybrid
- AI-powered chatbot with function calling support using Microsoft Agent Framework
- Random recipe generator with advanced filtering options
- Weekly meal planning with drag-and-drop timeline
- Shopping list management with AI-powered generation
- Biometric authentication support
- Speech-to-text input capabilities
  
![App Demo](https://github.com/DariuszTom/ReceptyOksSolution/blob/master/UsagePresentation4.gif)

## 🏗️ Solution Architecture

```
ReceptyOksSolution/
├── ReceptyOks/                          # .NET MAUI client application
├── ReceptyOks.Api/                      # ASP.NET Core Web API
├── ReceptyOks.Shared/                   # Shared models and DTOs
├── ReceptyOks.BlazorComponents/         # Blazor Razor components library
├── ReceptyOks_UnitTests/                # Unit tests (NUnit)
├── ReceptyOksSolution.AppHost/          # .NET Aspire orchestration host
└── ReceptyOksSolution.ServiceDefaults/  # Aspire service defaults
```

### Projects

| Project | Description | Target Framework |
|---------|-------------|------------------|
| **ReceptyOks** | .NET MAUI cross-platform client | net10.0-android, net10.0-windows10.0.19041.0 |
| **ReceptyOks.Api** | ASP.NET Core Web API backend | net10.0 |
| **ReceptyOks.Shared** | Shared models, DTOs, and AI components | net10.0 |
| **ReceptyOks.BlazorComponents** | Blazor Razor components with MudBlazor and ApexCharts | net10.0 |
| **ReceptyOks_UnitTests** | Unit tests with NUnit and Moq | net10.0-windows10.0.19041 |
| **ReceptyOksSolution.AppHost** | .NET Aspire 9.5 application host | net10.0 |
| **ReceptyOksSolution.ServiceDefaults** | Common service configurations with OpenTelemetry | net10.0 |

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [.NET MAUI workload](https://learn.microsoft.com/dotnet/maui/get-started/installation)
- [.NET Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)
- Visual Studio 2022 17.12+ or VS Code with C# Dev Kit

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/DariuszTom/ReceptyOksSolution.git
   cd ReceptyOksSolution
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Run the application using Aspire AppHost:
   ```bash
   dotnet run --project ReceptyOksSolution.AppHost
   ```

   This will start both the API and the MAUI client with proper orchestration.

### Running Individual Projects

**API only:**
```bash
dotnet run --project ReceptyOks.Api
```

**MAUI client (Android):**
```bash
dotnet build ReceptyOks -f net10.0-android
```

**MAUI client (Windows):**
```bash
dotnet build ReceptyOks -f net10.0-windows10.0.19041.0
```

## 📱 MAUI Client Features

- **Recipes Management**: Create, edit, view, and delete recipes
- **Categories**: Organize recipes into categories
- **Ingredients**: Manage recipe ingredients
- **OCR Integration**: Extract text from recipe photos using device camera
- **Offline Support**: Local SQLite database with sync capabilities
- **Rich Text Editor**: Format recipe instructions with rich text
- **AI-Powered ChatBot**: Conversational AI assistant for recipe recommendations and cooking help using Anthropic Claude
- **Random Recipe Generator**: Find recipes by category and ingredients with advanced filtering
- **Secure Authentication**: Multi-layer authentication with API keys and backend token provider
- **Logging**: Built-in logging with Serilog (stored locally in SQLite)
- **HTML Viewer**: Rich HTML rendering using Blazor Hybrid components

### Key Dependencies

- [CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui) - MAUI extensions
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM toolkit
- [UraniumUI](https://github.com/enisn/UraniumUI) - Material design components
- [Plugin.Maui.OCR](https://github.com/kfrancis/Plugin.Maui.OCR) - OCR functionality
- [Plugin.Maui.Biometric](https://github.com/nicoriff/Plugin.Maui.Biometric) - Biometric authentication
- [Anthropic](https://github.com/tryAGI/Anthropic) - Anthropic Claude SDK
- [Serilog](https://serilog.net/) - Structured logging
- [Polly](https://github.com/App-vNext/Polly) - Resilience and transient-fault-handling
- [AsyncAwaitBestPractices](https://github.com/brminnick/AsyncAwaitBestPractices) - Async/await extensions
- [Spillgebees.Blazor.RichTextEditor](https://github.com/nicoriff/Spillgebees.Blazor.RichTextEditor) - Rich text editing

## 🔌 API Features

The backend API provides RESTful endpoints for:

| Endpoint Group | Description | Rate Limit |
|----------------|-------------|------------|
| `/api/recipes` | Recipe CRUD operations, image retrieval | fixed (60/min) |
| `/api/categories` | Category management, recipes by category | fixed (60/min) |
| `/api/ingredients` | Ingredient management | fixed (60/min) |
| `/api/shopping-list` | Shopping list management (single & bulk operations) | fixed (60/min) |
| `/api/sync` | Bidirectional synchronization, full sync | fixed (60/min) |
| `/api/auth/validate` | Password hash validation | strict (10/min) |
| `/api/tokenprovider/token` | Anthropic token provider for AI features | strict (10/min) |

### Security Features

- **API Key Authentication**: Custom middleware validates API keys on all requests
- **JWT Bearer Authentication**: Token-based authentication for protected endpoints
- **Rate Limiting**: Per-IP rate limiting with fixed window (60 req/min) and strict (10 req/min) policies
- **HMAC-SHA256 Validation**: Secure password hash validation using constant-time comparison
- **Response Compression**: Brotli and Gzip compression for reduced payload sizes

### API Documentation

When running in development mode, API documentation is available via Scalar UI at:
```
https://localhost:{port}/scalar/v1
```

### Key Dependencies

- [Entity Framework Core](https://docs.microsoft.com/ef/core/) with SQLite and SQL Server providers
- [Scalar.AspNetCore](https://github.com/scalar/scalar) - Modern API documentation UI
- [Anthropic](https://github.com/tryAGI/Anthropic) - Anthropic Claude SDK
- [Azure.Identity](https://github.com/Azure/azure-sdk-for-net) - Azure authentication
- [Azure.Extensions.AspNetCore.Configuration.Secrets](https://github.com/Azure/azure-sdk-for-net) - Azure Key Vault configuration
- [Polly](https://github.com/App-vNext/Polly) - Resilience and transient-fault-handling
- [ASP.NET Core JWT Bearer Authentication](https://learn.microsoft.com/aspnet/core/security/authentication/) - Token-based authentication

## 🌐 .NET Aspire Integration

The solution uses .NET Aspire 9.5 (SDK) with Aspire.Hosting.AppHost 13.2.1 for:

- **Service Discovery**: Automatic service registration and discovery
- **Health Checks**: Built-in health monitoring with EF Core database checks
- **OpenTelemetry**: Distributed tracing, metrics, and logging via OTLP exporter
- **Resilience**: HTTP client resilience patterns via Microsoft.Extensions.Http.Resilience

### Background Services

- **ShoppingListCleaner**: Hosted service for automatic cleanup of old shopping list items

## ☁️ Azure Deployment

The solution includes Infrastructure as Code (IaC) using **Azure Bicep** for production deployment.

### Azure Resources (main.bicep)

| Resource | Description |
|----------|-------------|
| **Azure SQL Database** | Basic tier (2 GB), SQL Server 12.0, TLS 1.2 |
| **Container App Environment** | Managed environment for containerized API |
| **Container App** | `receptyoks-api` with auto-scaling (1-2 replicas) |
| **Log Analytics** | Optional workspace for centralized logging |

### Container App Configuration

- **Resources**: 0.25 vCPU, 0.5 GB memory (Consumption workload profile)
- **Ingress**: External HTTPS on port 8080
- **Health Probes**:
  - Liveness: `/alive` (30s interval)
  - Readiness: `/health` (10s interval)
- **Identity**: System-assigned managed identity for ACR access

### Deployment

```bash
az deployment group create \
  --resource-group <RG> \
  --template-file ReceptyOks.Api/.azure/main.bicep \
  --parameters main.bicepparam
```

### Required Parameters

| Parameter | Description |
|-----------|-------------|
| `containerImage` | Docker image name (without tag) |
| `acrServer` | ACR server (e.g., `myacr.azurecr.io`) |
| `jwtKey` | JWT signing key (secure) |
| `apiKey` | API access key (secure) |
| `sqlAdminPassword` | SQL admin password (secure) |

## 📦 Shared Library

The `ReceptyOks.Shared` project contains:

- **Models**: `Recipe`, `Category`, `Ingredient`, `RecipeIngredient`, `MealPlan`, `ShoppingListItem`
- **DTOs**: Sync-related data transfer objects (`SyncRequest`, `SyncResponse`, `CategorySyncDto`, etc.)
- **OCR Interfaces**: `IOCRService` and related types
- **AI Components**: AI chat functionality using Anthropic SDK and Microsoft.Extensions.AI
- **Microsoft Agents**: Integration with Microsoft.Agents.AI framework
- **Extension Methods**: Utility methods for encoding/decoding secrets and byte arrays
- **Configuration**: `GlobalConstants` for shared configuration values

## 📊 Blazor Components Library

The `ReceptyOks.BlazorComponents` project provides:

- **MudBlazor**: Material Design component framework
- **Blazor-ApexCharts**: Interactive charting components
- **Rich Text Editor**: Spillgebees rich text editing capabilities

## 🗄️ Data Storage

### Development Environment
- **API**: SQLite database (`recipes.db`) stored in the API's Data folder
- **Client**: Local SQLite database (`recipes_local.db`) stored in app data directory
- **Logs**: Application logs stored locally using Serilog file sink

### Production Environment (Azure)
- **API**: Azure SQL Server with connection string from Azure Key Vault
- **Features**:
  - Connection pooling with DbContextPool
  - Automatic retry on transient failures (configurable max retries)
  - Query splitting behavior for optimized loading
  - Command timeout: 240 seconds
- **Client**: Local SQLite with sync to cloud backend

## 🔧 Configuration

### API Configuration

The API uses standard ASP.NET Core configuration with support for:
- Database path configuration (`Database:Name`)
- JWT authentication settings (`Jwt:Key`)
- Rate limiting policies (fixed and strict window limiters)
- Secret resolution from Azure Key Vault or environment variables (via `SecretsResolver`)
- API key authentication middleware

### Client Configuration

The MAUI client uses embedded `appsettings.json` with:
- Local database path configuration
- HTTP client settings (API base URL, timeouts, service discovery)
- GitHub user agent configuration
- Secure storage for sensitive credentials (API keys, authentication tokens)

## 🧪 Testing

The solution includes unit tests in the `ReceptyOks_UnitTests` project:

- **Framework**: NUnit 4.x with NUnit3TestAdapter
- **Mocking**: Moq for dependency mocking
- **Coverage**: Coverlet for code coverage collection
- **Integration Tests**: Microsoft.AspNetCore.Mvc.Testing for API testing

### Running Tests

```bash
dotnet test
```

### Code Coverage

```bash
dotnet-coverage collect -f cobertura -o coverage.cobertura.xml dotnet test
```

## 📝 License

This project is licensed under the terms specified in the repository.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
