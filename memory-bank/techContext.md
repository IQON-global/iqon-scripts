# Azure Resource Management Tools - Technical Context

## Development Environment

The Azure Resource Mover is built as a .NET 9 console application, leveraging the latest features and performance improvements from this framework. The application is structured as a standard .NET project with the following technical characteristics:

- **Platform**: .NET 9 (Preview)
- **Application Type**: Console Application
- **Language**: C# 12
- **Project Structure**: Standard .NET SDK project format

## Core Technologies

### Azure DevOps REST API Integration

The Azure DevOps Release Agent Pool Updater script uses direct REST API calls to interact with Azure DevOps:

- **HttpClient**: For making HTTP requests to the Azure DevOps API endpoints
- **System.Text.Json**: For JSON serialization and deserialization
- **JsonDocument**: For parsing and manipulating complex JSON structures
- **REST API Endpoints**:
  - `/release/definitions` for listing release definitions
  - `/release/definitions/{id}` for getting and updating specific release definitions
  - `/distributedtask/pools` for listing agent pools

This approach was chosen instead of using the Azure DevOps SDK because:
- It offers more flexibility with API versions
- It avoids compatibility issues with .NET 9
- It provides more direct control over the request/response cycle

### Azure AD Authentication for Azure DevOps

The Azure DevOps integration uses Azure AD authentication:

- Uses `DefaultAzureCredential` from Azure.Identity
- Requests a token with the Azure DevOps resource scope (`499b84ac-1321-427f-aa17-267ca6975798/.default`)
- Passes the token via the Authorization header with Bearer scheme

### Azure SDK for .NET

The application uses several Azure SDK libraries to interact with Azure resources:

- **Azure.Identity**: For authentication with Azure
- **Azure.ResourceManager**: Core ARM library for resource management
- **Azure.ResourceManager.Resources**: For working with Azure resource groups
- **Azure.ResourceManager.KeyVault**: For KeyVault operations
- **Azure.ResourceManager.ServiceBus**: For Service Bus operations

These SDK libraries provide strongly-typed interfaces for Azure resources, making the code more maintainable and less error-prone than direct REST API calls.

### PowerShell Integration

The application executes PowerShell commands for resource movement operations:

- Uses `System.Diagnostics.Process` to execute PowerShell commands
- Specifically leverages the `Move-AzResource` PowerShell cmdlet
- Captures both standard output and standard error
- Waits asynchronously for command completion

This approach provides better reliability for Azure resource moves compared to using the SDK directly.

### Command-Line Interface

The application implements a simple command-line interface that:

- Accepts command arguments (e.g., `move-resources`)
- Supports flags like `--dry-run` and `--verbose`
- Uses a menu system for interactive mode
- Provides a consistent framework for adding new commands

## Supporting Technologies

### Regular Expressions

The application makes heavy use of regular expressions for:

- Extracting tenant IDs from resource names
- Validating resource naming patterns
- Matching resources across different naming conventions

These regex patterns are defined as compiled expressions for better performance:

```csharp
private readonly Regex _keyVaultTenantIdRegex = new Regex(@"kv-iqonsticos(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
private readonly Regex _serviceBusTenantIdRegex = new Regex(@"sb-iqon-sticos-?(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
private readonly Regex _webAppTenantIdRegex = new Regex(@"app-iqon-sticos-?(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
```

### Logging System

The application implements a comprehensive logging system that:

- Supports multiple log levels (Information, Warning, Error, Success, Verbose)
- Outputs to both console and standard output
- Uses color-coding for different log types
- Provides context-aware logging with method and class information

### HTTP Client

The application includes an HTTP client for potential direct API calls:

- Uses `System.Net.Http.HttpClient` for REST operations
- Supports JSON serialization/deserialization
- Can be used for API operations not covered by the Azure SDK

## Data Models

The application defines several key data models:

- **ResourceInfo**: Contains information about resources to be moved
- **ScriptResult**: Represents the outcome of script execution
- **SubscriptionInfo**: Contains Azure subscription details
- **CommandOptions**: Defines options for command execution

These models use standard C# features like:

- Automatic properties
- Object initializers
- Collection initializers
- Nullable reference types

## Development Constraints

### Resource Mover Constraints

- **Permissions**: The application requires appropriate Azure RBAC permissions to:
  - Read resources from source resource groups
  - Write to target resource groups
  - Execute move operations

- **Azure PowerShell Module**: The host system must have the Azure PowerShell module installed

- **Connection Requirements**: 
  - Active internet connection
  - Valid Azure credentials
  - Access to required Azure subscriptions

### Azure DevOps Release Agent Pool Updater Constraints

- **Permissions**: The application requires appropriate permissions to:
  - Read release definitions in Azure DevOps projects
  - Update release definitions in Azure DevOps projects
  - List agent pools in Azure DevOps projects

- **Azure AD Configuration**: The Azure AD tenant must be properly configured:
  - OAuth2 permissions for Azure DevOps must be granted to the application
  - The user or service principal must have sufficient rights in Azure DevOps

- **Azure DevOps Organization**: 
  - The organization URL must be accessible
  - The project must exist and contain release definitions
  - The target agent pool "Iqon Sticos VMSS 2" must exist
