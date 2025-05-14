# Azure Resource Management Tools - Active Context

## Current Focus

We have developed three primary tools for Azure resource management:

### 1. Azure Resource Mover

Addresses a specific problem in Azure resource management: when web apps are moved to new resource groups, associated resources like KeyVaults and Service Buses can be left behind in the original resource groups. This disconnection causes deployment failures when scripts attempt to update apps but can't find expected resources in the same resource group.

The application implements a robust solution that:

1. Discovers resources in source resource groups based on naming patterns
2. Matches them to web apps via tenant ID extraction
3. Identifies target resource groups where the web apps reside
4. Moves resources to reunite them with their web apps

### 2. Azure DevOps Release Agent Pool Updater

Addresses the need to update Agent Pools for Azure DevOps release definitions that follow a specific naming pattern. This tool allows DevOps engineers to easily update the Agent Pool configuration across multiple release definitions, ensuring consistent deployment environments.

The script implements a solution that:

1. Connects to Azure DevOps using Azure AD authentication
2. Finds release definitions matching the pattern "iqon-sticos-{tenantId}"
3. Updates the Agent Pool to "Iqon Sticos VMSS 2" for all matching releases
4. Provides detailed logging and supports dry-run mode for testing

### 3. Azure Key Vault Access Policy Updater

Addresses the need to add access policies to Azure Key Vaults matching a specific naming pattern. This tool allows administrators to quickly grant access to Key Vaults across multiple resource groups and tenants.

The script implements a solution that:

1. Discovers key vaults matching the pattern "kv-iqonsticos{tenantId}" in resource groups matching "rg-iqon-sticos(-\d+)?"
2. Adds a specified Entra Object ID to each key vault's access policies
3. Configures appropriate permissions based on the selected access level
4. Provides detailed logging and supports dry-run mode for testing

## Implementation Status

We've successfully implemented:

- **Resource Discovery**: Using both specific resource type APIs and generic resource enumeration as a fallback
- **Resource Matching**: Using regex patterns to extract and match tenant IDs
- **Target Resolution**: Finding the correct resource groups for each resource
- **Safe Movement**: Using PowerShell commands for reliable resource moves
- **Operational Safety**: Dry-run mode and comprehensive logging
- **Access Management**: Adding access policies to key vaults with configurable permission levels

The system uses a service-oriented architecture with the following key components:

- **AzureResourceService**: Core service handling resource discovery and movement
- **LoggerService**: Provides multi-level logging throughout the application
- **AzureAuthenticationService**: Manages Azure authentication and subscription selection

## Key Technical Features

The application leverages:

- **.NET 9**: Latest framework features and performance improvements
- **Azure SDK**: Strongly-typed interfaces for Azure resources
- **PowerShell Integration**: For reliable resource movement and access policy operations
- **Regular Expressions**: For tenant ID extraction and resource matching
- **Error Handling**: Comprehensive exception management and result reporting

## Current Challenges

During testing, we encountered some challenges:

1. **Permission Issues**: Some resources might not be discoverable using direct type-specific APIs
   - Solution: Implemented fallback to generic resource enumeration

2. **Resource Naming Variations**: Slight variations in naming patterns for Service Buses
   - Solution: Made regex patterns more flexible with optional hyphens

3. **Resource Dependencies**: Some resources might have dependencies that prevent movement
   - Solution: Implemented detailed error reporting and grouped moves by tenant ID

## Next Steps

The application is functional in its current state but could benefit from:

1. **Additional Resource Types**: Extending beyond KeyVaults and Service Buses to other resource types
2. **Parallel Processing**: Implementing parallel operations for better performance with large numbers of resources
3. **Pre-operation Validation**: Adding more comprehensive validation before attempting operations
4. **Dependency Analysis**: Automatically detecting and handling resource dependencies
5. **Rollback Mechanism**: Implementing a rollback capability if operations partially fail

## Decision Points

Key technical decisions made:

1. Using PowerShell commands for resource operations rather than direct SDK calls
2. Implementing a fallback to generic resource discovery to handle permission limitations
3. Organizing resources by tenant ID for logical grouping and simplified matching
4. Comprehensive logging at multiple levels to provide operational clarity
5. Resource group filtering to optimize discovery and focus on relevant resources
