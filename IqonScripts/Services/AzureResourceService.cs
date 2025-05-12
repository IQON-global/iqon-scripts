using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ServiceBus;
using IqonScripts.Models;
using IqonScripts.Utils;

namespace IqonScripts.Services;

/// <summary>
/// Service for working with Azure resources
/// </summary>
public class AzureResourceService
{
    private readonly ArmClient _armClient;
    private readonly LoggerService _logger;
    private ResourceGroupResource _sourceResourceGroup;
    private readonly HttpClient _httpClient;

    // Regular expressions for extracting tenant IDs from resource names
    private readonly Regex _keyVaultTenantIdRegex = new Regex(@"kv-iqonsticos(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _serviceBusTenantIdRegex = new Regex(@"sb-iqon-sticos-?(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly Regex _webAppTenantIdRegex = new Regex(@"app-iqon-sticos-?(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureResourceService"/> class.
    /// </summary>
    /// <param name="armClient">The ARM client</param>
    /// <param name="logger">The logger service</param>
    /// <param name="sourceResourceGroupName">The source resource group name</param>
    public AzureResourceService(ArmClient armClient, LoggerService logger, string sourceResourceGroupName)
    {
        _armClient = armClient;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Initializes the resource group and performs necessary setup
    /// </summary>
    /// <param name="sourceResourceGroupName">Source resource group name</param>
    /// <returns>Task representing the async operation</returns>
    public async Task InitializeAsync(string sourceResourceGroupName)
    {
        // Get the subscription
        var currentSubscription = _armClient.GetDefaultSubscription();
        _logger.LogInformation($"Using subscription: {currentSubscription.Data.DisplayName} (ID: {currentSubscription.Id.SubscriptionId})");
        
        // Get the source resource group using async method
        _logger.LogInformation($"Retrieving resource group: {sourceResourceGroupName}");
        _sourceResourceGroup = await currentSubscription.GetResourceGroups().GetAsync(sourceResourceGroupName);
        
        if (_sourceResourceGroup == null)
        {
            throw new ArgumentException($"Source resource group '{sourceResourceGroupName}' not found.");
        }
        
        _logger.LogSuccess($"Successfully retrieved resource group: {_sourceResourceGroup.Data.Name}");
    }

    /// <summary>
    /// Discovers resources to move in the source resource group
    /// </summary>
    /// <param name="targetResourceGroup">Optional fixed target resource group to use</param>
    /// <returns>A list of resources to move</returns>
    public async Task<List<ResourceInfo>> DiscoverResourcesToMoveAsync(string? targetResourceGroup = null)
    {
        var resourcesToMove = new List<ResourceInfo>();
        
        _logger.LogInformation($"Discovering resources in resource group '{_sourceResourceGroup.Data.Name}'...");
        
        // Discover KeyVaults
        await DiscoverKeyVaultsAsync(resourcesToMove);
        
        // Discover Service Buses
        await DiscoverServiceBusesAsync(resourcesToMove);
        
        // Find target resource groups for each resource
        await FindTargetResourceGroupsAsync(resourcesToMove, targetResourceGroup);
        
        return resourcesToMove;
    }

    /// <summary>
    /// Moves resources to their target resource groups
    /// </summary>
    /// <param name="resources">The resources to move</param>
    /// <param name="dryRun">Whether to run in dry run mode (no actual changes)</param>
    /// <returns>The result of the move operation</returns>
    public async Task<ScriptResult> MoveResourcesAsync(List<ResourceInfo> resources, bool dryRun)
    {
        var result = new ScriptResult
        {
            DryRun = dryRun,
            ProcessedResources = resources
        };
        
        if (dryRun)
        {
            _logger.LogInformation("Running in DRY RUN mode. No resources will be moved.");
            result.Success = true;
            return result;
        }
        
        _logger.LogInformation("Moving resources...");
        var startTime = DateTime.UtcNow;
        
        foreach (var resource in resources)
        {
            try
            {
                _logger.LogInformation($"Moving {resource.Name} to {resource.TargetResourceGroup}...");
                
                // Get the subscription
                var subscription = _armClient.GetDefaultSubscription();
                
                // Get the target resource group
                var targetResourceGroup = await subscription.GetResourceGroupAsync(resource.TargetResourceGroup);
                
                if (targetResourceGroup?.Value == null)
                {
                    var errorMessage = $"Target resource group '{resource.TargetResourceGroup}' not found.";
                    _logger.LogError(errorMessage);
                    result.Errors.Add(errorMessage);
                    continue;
                }
                
                // Move the resource
                var moveSuccess = await MoveResourceToResourceGroupAsync(
                    subscription.Id.SubscriptionId,
                    resource.SourceResourceGroup,
                    resource.TargetResourceGroup,
                    resource.Id);
                
                if (moveSuccess)
                {
                    _logger.LogSuccess($"Successfully moved {resource.Name} to {resource.TargetResourceGroup}");
                }
                else
                {
                    var errorMessage = $"Failed to move {resource.Name} to {resource.TargetResourceGroup}";
                    _logger.LogError(errorMessage);
                    result.Errors.Add(errorMessage);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Failed to move {resource.Name} to {resource.TargetResourceGroup}: {ex.Message}";
                _logger.LogError(errorMessage, ex);
                result.Errors.Add(errorMessage);
            }
        }
        
        var endTime = DateTime.UtcNow;
        result.ExecutionTimeMs = (long)(endTime - startTime).TotalMilliseconds;
        result.Success = result.Errors.Count == 0;
        
        return result;
    }

    /// <summary>
    /// Moves a resource to a different resource group using the Azure PowerShell cmdlet
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="sourceResourceGroup">The source resource group</param>
    /// <param name="targetResourceGroup">The target resource group</param>
    /// <param name="resourceId">The resource ID</param>
    /// <returns>Whether the move was successful</returns>
    private async Task<bool> MoveResourceToResourceGroupAsync(
        string subscriptionId,
        string sourceResourceGroup,
        string targetResourceGroup,
        string resourceId)
    {
        try
        {
            // Use PowerShell (via the Azure CLI) to move resources
            // This is a more reliable method than using the ARM REST API directly
            var powershellCommand = $"Move-AzResource -ResourceId \"{resourceId}\" -DestinationResourceGroupName \"{targetResourceGroup}\" -Force";
            var command = $"powershell -Command \"{powershellCommand}\"";
            
            // Create process to run PowerShell
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{powershellCommand}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            _logger.LogVerbose($"Executing PowerShell command: {powershellCommand}");
            
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            if (process.ExitCode != 0)
            {
                _logger.LogError($"PowerShell command failed with exit code {process.ExitCode}: {error}");
                return false;
            }
            
            _logger.LogVerbose($"PowerShell command output: {output}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to move resource: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Helper method to extract tenant ID from resource name
    /// </summary>
    /// <param name="resourceName">The resource name</param>
    /// <param name="regex">The regex pattern to use</param>
    /// <returns>The tenant ID if found, otherwise null</returns>
    private string ExtractTenantId(string resourceName, Regex regex)
    {
        var match = regex.Match(resourceName);
        _logger.LogInformation($"Matching '{resourceName}' against pattern '{regex}' - IsMatch: {match.Success}");
        
        if (match.Success)
        {
            var tenantId = match.Groups[1].Value;
            return tenantId;
        }
        
        return null;
    }

    /// <summary>
    /// Finds KeyVaults in the source resource group
    /// </summary>
    /// <param name="resources">The list to add found resources to</param>
    private async Task DiscoverKeyVaultsAsync(List<ResourceInfo> resources)
    {
        _logger.LogInformation($"Looking for KeyVaults in resource group '{_sourceResourceGroup.Data.Name}'...");
        _logger.LogInformation($"Using KeyVault pattern: {_keyVaultTenantIdRegex}");
        
        // Use strongly-typed collections which are more reliable than generic resources
        _logger.LogInformation("Looking for KeyVaults using specialized KeyVault collection...");
        int keyVaultCount = 0;
        
        // List all raw KeyVault names first for troubleshooting
        _logger.LogInformation("Listing all KeyVaults before filtering:");
        
        // Use the strongly-typed KeyVault collection
        await foreach (var vault in _sourceResourceGroup.GetKeyVaults().GetAllAsync())
        {
            keyVaultCount++;
            _logger.LogInformation($"Found KeyVault: {vault.Data.Name}");
            
            string tenantId = ExtractTenantId(vault.Data.Name, _keyVaultTenantIdRegex);
            if (tenantId != null)
            {
                resources.Add(new ResourceInfo
                {
                    Id = vault.Id,
                    Name = vault.Data.Name,
                    Type = "KeyVault",
                    TenantId = tenantId,
                    SourceResourceGroup = _sourceResourceGroup.Data.Name
                });
                
                _logger.LogInformation($"Found KeyVault: {vault.Data.Name} with tenant ID: {tenantId}");
            }
            else
            {
                _logger.LogInformation($"Skipping KeyVault {vault.Data.Name} as it doesn't match the expected naming pattern");
            }
        }
        
        if (keyVaultCount == 0)
        {
            _logger.LogWarning("No KeyVaults found in the resource group. This might indicate permission issues or the KeyVault doesn't exist.");
        }
    }

    /// <summary>
    /// Finds Service Buses in the source resource group
    /// </summary>
    /// <param name="resources">The list to add found resources to</param>
    private async Task DiscoverServiceBusesAsync(List<ResourceInfo> resources)
    {
        _logger.LogInformation($"Looking for Service Buses in resource group '{_sourceResourceGroup.Data.Name}'...");
        _logger.LogInformation($"Using Service Bus pattern: {_serviceBusTenantIdRegex}");
        
        // Use strongly-typed collections which are more reliable than generic resources
        _logger.LogInformation("Looking for Service Buses using specialized ServiceBus collection...");
        int serviceBusCount = 0;
        
        // List all raw Service Bus names first for troubleshooting
        _logger.LogInformation("Listing all Service Buses before filtering:");
        
        // Use the strongly-typed ServiceBus collection
        await foreach (var serviceBus in _sourceResourceGroup.GetServiceBusNamespaces().GetAllAsync())
        {
            serviceBusCount++;
            _logger.LogInformation($"Found Service Bus: {serviceBus.Data.Name}");
            
            string tenantId = ExtractTenantId(serviceBus.Data.Name, _serviceBusTenantIdRegex);
            if (tenantId != null)
            {
                resources.Add(new ResourceInfo
                {
                    Id = serviceBus.Id,
                    Name = serviceBus.Data.Name,
                    Type = "ServiceBus",
                    TenantId = tenantId,
                    SourceResourceGroup = _sourceResourceGroup.Data.Name
                });
                
                _logger.LogInformation($"Found Service Bus: {serviceBus.Data.Name} with tenant ID: {tenantId}");
            }
            else
            {
                _logger.LogInformation($"Skipping Service Bus {serviceBus.Data.Name} as it doesn't match the expected naming pattern");
            }
        }
        
        if (serviceBusCount == 0)
        {
            _logger.LogWarning("No Service Bus namespaces found in the resource group. This might indicate permission issues or the Service Bus doesn't exist.");
        }
    }

    /// <summary>
    /// Finds target resource groups for the resources to move
    /// </summary>
    /// <param name="resources">The resources to find target resource groups for</param>
    /// <param name="fixedTargetResourceGroup">Optional fixed target resource group to use</param>
    private async Task FindTargetResourceGroupsAsync(List<ResourceInfo> resources, string? fixedTargetResourceGroup = null)
    {
        if (resources.Count == 0)
        {
            _logger.LogWarning("No resources found to move.");
            return;
        }
        
        // Check if a fixed target resource group is specified
        if (!string.IsNullOrEmpty(fixedTargetResourceGroup))
        {
            _logger.LogInformation($"Using fixed target resource group: {fixedTargetResourceGroup}");
            
            // Get the default subscription
            var targetSubscription = _armClient.GetDefaultSubscription();
            
            // Verify the target resource group exists
            try
            {
                var targetResourceGroup = await targetSubscription.GetResourceGroups().GetAsync(fixedTargetResourceGroup);
                if (targetResourceGroup != null)
                {
                    _logger.LogSuccess($"Found target resource group: {fixedTargetResourceGroup}");
                    
                    // Set all resources to use the fixed target resource group
                    foreach (var resource in resources)
                    {
                        resource.TargetResourceGroup = fixedTargetResourceGroup;
                        _logger.LogInformation($"Resource {resource.Name} will be moved to {fixedTargetResourceGroup}");
                    }
                    
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Fixed target resource group '{fixedTargetResourceGroup}' not found or not accessible: {ex.Message}");
                _logger.LogWarning("Falling back to auto-discovery of target resource groups");
            }
        }
        
        _logger.LogInformation("Finding target resource groups by tenant ID...");
        
        // Get the default subscription
        var subscription = _armClient.GetDefaultSubscription();
        
        // Group resources by tenant ID
        var resourcesByTenantId = resources.GroupBy(r => r.TenantId);
        
        foreach (var group in resourcesByTenantId)
        {
            var tenantId = group.Key;
            
            if (string.IsNullOrEmpty(tenantId))
            {
                _logger.LogWarning($"Skipping resources with no tenant ID");
                continue;
            }
            
            // Look for resource groups with web apps matching the tenant ID pattern
            var webAppResourceGroupName = string.Empty;
            
            await foreach (var resourceGroup in subscription.GetResourceGroups().GetAllAsync())
            {
                // Check if this resource group contains a web app with the expected name pattern
                bool found = false;
                
                // Use Web-specific collection if available, otherwise fall back to generic resources
                _logger.LogVerbose($"Checking resource group {resourceGroup.Data.Name} for web apps with tenant ID {tenantId}");
                
                try 
                {
                    // First list all web apps for troubleshooting
                    _logger.LogVerbose($"Web apps in resource group {resourceGroup.Data.Name}:");
                    
                    // Try using specific web site collection (requires Microsoft.Web resource provider and permissions)
                    bool hasWebApps = false;
                    
                    // We're using GetGenericResourcesAsync with filtering since it's more widely accessible
                    await foreach (var webApp in resourceGroup.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Web/sites'"))
                    {
                        hasWebApps = true;
                        _logger.LogVerbose($"Found web app: {webApp.Data.Name}");
                        
                        string webAppTenantId = ExtractTenantId(webApp.Data.Name, _webAppTenantIdRegex);
                        if (webAppTenantId != null && webAppTenantId == tenantId)
                        {
                            webAppResourceGroupName = resourceGroup.Data.Name;
                            found = true;
                            _logger.LogInformation($"Found web app {webApp.Data.Name} with matching tenant ID {tenantId} in resource group {resourceGroup.Data.Name}");
                            break;
                        }
                    }
                    
                    if (!hasWebApps)
                    {
                        _logger.LogVerbose($"No web apps found in resource group {resourceGroup.Data.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error checking web apps in resource group {resourceGroup.Data.Name}: {ex.Message}");
                }
                
                if (found) break;
            }
            
            if (string.IsNullOrEmpty(webAppResourceGroupName))
            {
                _logger.LogWarning($"No target resource group found for tenant ID {tenantId}");
                continue;
            }
            
            // Set the target resource group for all resources with this tenant ID
            foreach (var resource in group)
            {
                resource.TargetResourceGroup = webAppResourceGroupName;
                _logger.LogInformation($"Resource {resource.Name} will be moved to {resource.TargetResourceGroup}");
            }
        }
        
        // Filter out resources with no target resource group
        var resourcesWithNoTarget = resources.Where(r => string.IsNullOrEmpty(r.TargetResourceGroup)).ToList();
        foreach (var resource in resourcesWithNoTarget)
        {
            _logger.LogWarning($"Removing {resource.Name} from move list as no target resource group was found");
            resources.Remove(resource);
        }
    }
}
