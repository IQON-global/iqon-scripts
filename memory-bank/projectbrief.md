# Azure Resource Mover Project Brief

## Project Purpose
Create a .NET 9 console application that can discover and move Azure resources between resource groups, specifically targeting resources that were left behind when web apps were moved to a new resource group.

## Core Requirements
1. Discover resources (KeyVaults and Service Buses) in source resource groups that follow specific naming patterns
2. Match these resources to their corresponding web apps by tenant ID
3. Move resources to the appropriate target resource groups where their web apps reside
4. Support dry-run mode for testing without making actual changes
5. Provide detailed logging throughout the process

## Naming Patterns
- KeyVault format: `kv-iqonsticos{tenantId}`
- Service Bus format: `sb-iqon-sticos-{tenantId}`
- Web App format: `app-iqon-sticos-{tenantId}`

## Success Criteria
- Resources are correctly identified using the specified naming patterns
- Resources are matched to the correct web app resource groups
- Resources can be moved with proper validation and error handling
- The application provides sufficient logging to understand the discovery and movement process
- The application safely handles permissions issues and provides appropriate warnings
