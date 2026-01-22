# ReceptyOks Solution

A cross-platform recipe management application built with **.NET 10**, **.NET MAUI**, and **.NET Aspire**.

## 📋 Overview

ReceptyOks is a modern recipe management solution that allows users to create, organize, and sync recipes across devices. The application features:

- Cross-platform mobile/desktop client (Android, iOS, macOS, Windows)
- RESTful API backend with SQLite database
- Offline-first architecture with sync capabilities
- OCR support for extracting text from recipe images
- Rich text editing for recipe instructions
- AI-powered chatbot for recipe assistance and recommendations
- Random recipe generator with advanced filtering options

## 🏗️ Solution Architecture

```
ReceptyOksSolution/
├── ReceptyOks/                          # .NET MAUI client application
├── ReceptyOks.Api/                      # ASP.NET Core Web API
├── ReceptyOks.Shared/                   # Shared models and DTOs
├── ReceptyOksSolution.AppHost/          # .NET Aspire orchestration host
└── ReceptyOksSolution.ServiceDefaults/  # Aspire service defaults
```

### Projects

| Project | Description | Target Framework |
|---------|-------------|------------------|
| **ReceptyOks** | .NET MAUI cross-platform client | net10.0-android, net10.0-ios, net10.0-maccatalyst, net10.0-windows |
| **ReceptyOks.Api** | ASP.NET Core Web API backend | net10.0 |
| **ReceptyOks.Shared** | Shared models, DTOs, and interfaces | net10.0 |
| **ReceptyOks.BlazorComponents** | Blazor Razor components for HTML rendering | net10.0 |
| **ReceptyOksSolution.AppHost** | .NET Aspire application host | net10.0 |
| **ReceptyOksSolution.ServiceDefaults** | Common service configurations | net10.0 |

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
- [UraniumUI](https://github.com/nicoriff/uranium-ui) - Material design components
- [Plugin.Maui.OCR](https://github.com/nicoriff/Plugin.Maui.OCR) - OCR functionality
- [Serilog](https://serilog.net/) - Structured logging
- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI) - AI abstraction layer for chat and embeddings
- [Microsoft.Extensions.AI.Anthropic](https://www.nuget.org/packages/Microsoft.Extensions.AI.Anthropic) - Anthropic Claude integration

## 🔌 API Features

The backend API provides RESTful endpoints for:

- `/api/recipes` - Recipe CRUD operations
- `/api/categories` - Category management
- `/api/ingredients` - Ingredient management
- `/api/sync` - Data synchronization
- `/api/auth/validate` - API key authentication validation

### API Documentation

When running in development mode, API documentation is available via Scalar UI at:
```
https://localhost:{port}/scalar/v1
```

### Key Dependencies

- [Entity Framework Core](https://docs.microsoft.com/ef/core/) with SQLite provider
- [Scalar.AspNetCore](https://github.com/scalar/scalar) - Modern API documentation UI
- [ASP.NET Core Rate Limiting](https://learn.microsoft.com/aspnet/core/performance/rate-limit) - Built-in rate limiting
- [ASP.NET Core JWT Bearer Authentication](https://learn.microsoft.com/aspnet/core/security/authentication/) - Token-based authentication

## 🌐 .NET Aspire Integration

The solution uses .NET Aspire for:

- **Service Discovery**: Automatic service registration and discovery
- **Health Checks**: Built-in health monitoring
- **OpenTelemetry**: Distributed tracing, metrics, and logging
- **Resilience**: HTTP client resilience patterns

## 📦 Shared Library

The `ReceptyOks.Shared` project contains:

- **Models**: `Recipe`, `Category`, `Ingredient`, `RecipeIngredient`
- **DTOs**: Sync-related data transfer objects
- **OCR Interfaces**: `IOCRService` and related types
- **AI Components**: `AiAgent`, `AnthropicAgent`, and `AnthropicSettings` for AI chat functionality
- **Extension Methods**: Utility methods for encoding/decoding secrets and byte arrays

## 🗄️ Data Storage

- **API**: SQLite database (`recipes.db`) stored in the API's Data folder
- **Client**: Local SQLite database (`recipes_local.db`) stored in app data directory
- **Logs**: Application logs stored in SQLite using custom Serilog sink

## 🔧 Configuration

### API Configuration

The API uses standard ASP.NET Core configuration with support for:
- Database path configuration (`Database:Name`)
- JWT authentication settings (`Jwt:Key`)
- Rate limiting policies (fixed window limiter)
- Secret resolution from Azure Key Vault or environment variables (via `SecretsResolver`)
- API key authentication middleware

### Client Configuration

The MAUI client uses embedded `appsettings.json` with:
- Local database path configuration
- HTTP client settings (API base URL, timeouts, service discovery)
- GitHub user agent configuration
- Secure storage for sensitive credentials (API keys, authentication tokens)

## 📝 License

This project is licensed under the terms specified in the repository.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
