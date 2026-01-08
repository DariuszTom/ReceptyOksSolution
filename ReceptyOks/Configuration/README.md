# ReceptyOks - Configuration Setup

## Overview
This project uses **IConfiguration** from .NET for managing application settings in a type-safe, centralized manner.

## Architecture

### Files Structure
```
ReceptyOks/
??? appsettings.json                    # Configuration file (Embedded Resource)
??? Configuration/
?   ??? AppSettings.cs                  # Strongly-typed configuration classes
??? MauiProgram.cs                      # Configuration loader and DI setup
```

## Configuration Files

### appsettings.json
Located at project root, marked as **EmbeddedResource** in `.csproj`:
```json
{
  "Database": {
    "LocalDatabaseName": "recipes_local.db"
  },
  "Http": {
    "ApiServiceName": "receptyoks-api",
    "DefaultTimeoutSeconds": 30
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning"
    }
  }
}
```

### AppSettings.cs
Strongly-typed configuration classes:
```csharp
public class AppSettings
{
    public DatabaseSettings Database { get; set; }
    public HttpSettings Http { get; set; }
}

public class DatabaseSettings
{
    public string LocalDatabaseName { get; set; }
    public string LocalDatabasePath => Path.Combine(FileSystem.AppDataDirectory, LocalDatabaseName);
}

public class HttpSettings
{
    public string ApiServiceName { get; set; }
    public int DefaultTimeoutSeconds { get; set; }
}
```

## Usage

### In MauiProgram.cs
Configuration is loaded automatically at startup:
```csharp
var appSettings = LoadConfiguration(builder);
builder.Services.AddSingleton(appSettings);
```

### In ViewModels (Dependency Injection)
Inject `AppSettings` into any class:
```csharp
public class MyViewModel
{
    private readonly AppSettings _settings;
    
    public MyViewModel(AppSettings settings)
    {
        _settings = settings;
        
        // Access configuration
        var dbPath = _settings.Database.LocalDatabasePath;
        var apiName = _settings.Http.ApiServiceName;
        var timeout = _settings.Http.DefaultTimeoutSeconds;
    }
}
```

### Direct Access (Not Recommended)
If you need to access settings outside of DI:
```csharp
var settings = MauiApplication.Current.Services.GetService<AppSettings>();
```

## Required NuGet Packages

```xml
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.1" />
```

## .csproj Configuration

Ensure `appsettings.json` is marked as EmbeddedResource:
```xml
<ItemGroup>
  <EmbeddedResource Include="appsettings.json" />
</ItemGroup>
```

## Benefits

? **Type-Safe**: Compile-time checking and IntelliSense support  
? **Centralized**: All settings in one place  
? **Cross-Platform**: Works on Android, iOS, Windows, MacCatalyst  
? **DI Integration**: Easy access through dependency injection  
? **Maintainable**: Easy to add new settings  
? **Standard .NET**: Uses standard Microsoft.Extensions.Configuration  

## Adding New Settings

1. **Update appsettings.json**:
```json
{
  "NewSection": {
    "Setting1": "value1",
    "Setting2": 42
  }
}
```

2. **Create new configuration class**:
```csharp
public class NewSectionSettings
{
    public string Setting1 { get; set; }
    public int Setting2 { get; set; }
}
```

3. **Add to AppSettings**:
```csharp
public class AppSettings
{
    // ...existing properties...
    public NewSectionSettings NewSection { get; set; } = new();
}
```

4. **Use in your code**:
```csharp
var value = _settings.NewSection.Setting1;
```

## Best Practices

1. **Always use defaults** in configuration classes
2. **Use computed properties** for derived values (like `LocalDatabasePath`)
3. **Document settings** with XML comments
4. **Inject settings** through DI, not direct access
5. **Group related settings** in dedicated classes

## Troubleshooting

### "appsettings.json not found" error
- Ensure file exists in project root
- Check `.csproj` has `<EmbeddedResource Include="appsettings.json" />`
- Clean and rebuild solution

### Settings not updating
- Remember: appsettings.json is **embedded** at compile time
- Changes require rebuild to take effect
- For runtime changes, consider using Preferences API or database

### Package version conflicts
- Ensure all Microsoft.Extensions.* packages use compatible versions
- Check transitive dependencies
- Use highest required version if conflicts occur
