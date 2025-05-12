# Azure Resource Mover

A .NET 9 console application for moving Azure resources between resource groups. This tool helps with the specific scenario where web apps have been moved to a new resource group, but related resources (KeyVault and Service Bus) are still in the old resource group.

## Features

- Automatically discovers KeyVaults and Service Buses in a source resource group
- Identifies the appropriate target resource group based on tenant ID pattern matching
- Supports dry-run mode to preview changes without executing them
- Provides detailed logging and reporting
- Uses interactive login for authentication

## Requirements

- .NET 9 SDK
- PowerShell
- Azure PowerShell module
- Appropriate Azure permissions to move resources
- Access to Azure tenant ID: c5772ebb-4c35-4874-abb7-1eb6cbdc90d9
- Default subscription ID: 5c0a77d0-2891-4e4a-a39d-38d29bf072a0

## Usage

### Interactive Menu

Simply run the application without any arguments to launch the interactive menu:

```bash
dotnet run
```

This will display a menu of available commands. Follow the on-screen prompts to select a command and provide the necessary parameters.

### Command Line Usage

Alternatively, you can use the command-line interface directly:

```bash
dotnet run -- move-resources
```

By default, this will look for resources in the "rg-iqon-sticos" resource group.

### Command Line Options

```bash
dotnet run -- move-resources --help
```

```
Description:
  Move Azure resources between resource groups

Usage:
  IqonScripts move-resources [options]

Options:
  -s, --source-group <source-group>  Source resource group name [default: rg-iqon-sticos]
  -d, --dry-run                      Run in dry run mode (no changes will be made) [default: False]
  -v, --verbose                      Enable verbose logging [default: False]
  -?, -h, --help                     Show help and usage information
```

### Examples

Run with a specific source resource group:
```bash
dotnet run -- move-resources --source-group MySourceGroup
```

Perform a dry run to preview changes:
```bash
dotnet run -- move-resources --dry-run
```

Enable verbose logging:
```bash
dotnet run -- move-resources --verbose
```

## How It Works

1. The application authenticates with Azure using DefaultAzureCredential
2. It searches for KeyVaults with name pattern "kv-iqonsticos{tenantId}" and Service Buses with name pattern "sb-iqon-sticos-{tenantId}" in the source resource group
3. For each resource found, it extracts the tenant ID from the name
4. It searches for web apps with name pattern "app-iqon-sticos-{tenantId}" to determine the correct target resource group
5. In dry-run mode, it reports what would be moved without making changes
6. In execution mode, it moves the resources to their target resource groups using PowerShell's Move-AzResource cmdlet

## Extending the Application

The application is designed to be extended with additional scripts. To add a new script:

1. Create a new script class in the `Scripts` folder
2. Add a new command to `Program.cs`
3. Implement the script's logic following the pattern of `ResourceMoverScript.cs`

## Troubleshooting

If you encounter authentication issues, try the following:

1. Ensure you have the Azure PowerShell module installed: `Install-Module -Name Az`
2. Log in to Azure in PowerShell: `Connect-AzAccount`
3. Check that you have appropriate permissions to move resources in Azure
4. Use the `--verbose` flag to see more detailed logging
