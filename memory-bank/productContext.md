# Azure Resource Mover Product Context

## Problem Statement
During the migration of Azure web applications to new resource groups, certain connected resources (specifically KeyVaults and Service Buses) can be left behind in the original resource groups. This creates a scenario where:

1. The web apps reside in new resource groups
2. Their dependent KeyVaults and Service Buses remain in the old resource groups
3. When deployment scripts attempt to update the web apps, they look for resources in the same resource group
4. Not finding the required resources, the scripts attempt to create new ones
5. This leads to resource naming conflicts and failed deployments

This disjointed state causes deployment failures, increased maintenance overhead, and potential production issues.

## Solution Overview
The Azure Resource Mover is a specialized .NET 9 console application that:

1. Identifies orphaned resources in source resource groups based on naming patterns
2. Matches these resources to their corresponding web apps by tenant ID
3. Automatically determines the correct target resource group for each resource
4. Provides a safe migration path with dry-run capabilities
5. Moves the resources to reunite them with their web apps

## Intended Workflow
The solution is designed to be run by a DevOps engineer or Azure administrator who needs to:

1. First run the tool in dry-run mode to identify what resources would be moved
2. Review the proposed changes for correctness
3. Execute the actual migration with full logging
4. Verify that deployments succeed after resource relocation

## User Experience Goals
The application prioritizes:

1. **Safety**: Dry-run mode, detailed logging, and validation before any moves
2. **Clarity**: Providing clear information about what is happening and why
3. **Reliability**: Using robust methods (PowerShell commands) to ensure successful resource moves
4. **Efficiency**: Automating what would otherwise be a manual, error-prone process
5. **Resilience**: Handling missing resources or permission issues gracefully with appropriate warnings

## Implementation Approach
The implementation uses:

1. Azure SDK for .NET to discover and interact with resources
2. Regular expression matching for resource identification
3. PowerShell command execution for reliable resource movement
4. Comprehensive logging with multiple severity levels
5. Tenant ID-based resource grouping for logical organization
