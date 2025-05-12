# Azure Resource Mover - System Patterns

## Architecture Overview

The Azure Resource Mover follows a service-oriented architecture with clear separation of concerns. The application is structured as follows:

```mermaid
flowchart TD
    Program[Program.cs] --> MenuSystem[MenuSystem]
    MenuSystem --> Scripts[ResourceMoverScript]
    Scripts --> Services[AzureResourceService]
    Services --> AzureSDK[Azure SDK]
    Services --> PowerShell[PowerShell Execution]
    Services --> Logger[LoggerService]
    Services --> Auth[AzureAuthenticationService]
```

## Key Design Patterns

### 1. Service-Oriented Architecture
The application is organized into specialized services, each with a specific responsibility:

- **AzureResourceService**: Core service for discovering and moving resources
- **LoggerService**: Handles all logging with different severity levels
- **AzureAuthenticationService**: Manages Azure authentication and subscription selection

This pattern promotes:
- Separation of concerns
- Testability
- Maintainability
- Single responsibility principle

### 2. Command Pattern
The script execution follows a command pattern where:

- **ResourceMoverScript** encapsulates the command execution logic
- **CommandOptions** represents the command parameters
- **ScriptResult** captures the outcome of the command execution

This allows for:
- Decoupled execution logic
- Consistent result handling
- Future extensibility for new commands

### 3. Strategy Pattern
Resource discovery implements a strategy pattern:

- Different discovery methods for different resource types (KeyVault, Service Bus)
- Common interface for adding resources to the collection
- Fallback mechanisms when primary discovery methods fail

This enables:
- Extension to new resource types
- Consistent handling of different resource types
- Resilience to permission issues

### 4. Data Transfer Objects
The application uses DTOs to represent:

- **ResourceInfo**: Resource metadata and mapping information
- **ScriptResult**: Operation results including errors and execution time
- **SubscriptionInfo**: Azure subscription details

### 5. Regular Expression Strategy
A critical pattern is the use of regular expressions to:

- Extract tenant IDs from resource names
- Match resources to their corresponding web apps
- Validate resource naming conventions

## Technical Decisions

### 1. PowerShell Command Execution
The application uses PowerShell's `Move-AzResource` command for resource movement, rather than direct API calls. This decision was made because:

- PowerShell commands are more reliable for resource group operations
- They include built-in validation and error handling
- They provide better compatibility with Azure's management layer

### 2. Generic Resource Discovery Fallback
The application first attempts to use specialized resource-type APIs but falls back to generic resource enumeration when:

- Permission issues might prevent direct API access
- More comprehensive resource information is needed
- Greater resilience is required

### 3. Tenant ID-Based Resource Grouping
Resources are grouped by tenant ID before processing, which:

- Ensures logical organization of moves
- Prevents partial moves of related resources
- Simplifies association with target resource groups

### 4. Comprehensive Logging
The application implements verbose, multi-level logging that:

- Documents each step of the process
- Provides debugging information
- Records both successes and failures
- Makes the tool behavior transparent to users

## Error Handling Strategy

The application implements a robust error handling approach:

1. **Try-Catch Blocks**: Around all external operations
2. **Error Collection**: Aggregation of all errors in the ScriptResult
3. **Warning Generation**: For non-fatal issues (like missing resources)
4. **Graceful Degradation**: Continuing operation when possible despite errors
5. **Detailed Error Messages**: Including both user-friendly and technical details
