using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using IqonScripts.Models;
using IqonScripts.Utils;
using Microsoft.Extensions.Logging;

namespace IqonScripts.Scripts;

/// <summary>
/// Script to list App Service Plans along with their app count and resource group
/// </summary>
public class AppServicePlanListScript
{
    private readonly AppServicePlanListOptions _options;
    private readonly LoggerService _logger;
    private readonly ArmClient _armClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppServicePlanListScript"/> class
    /// </summary>
    /// <param name="options">The command options</param>
    /// <param name="logger">The logger</param>
    public AppServicePlanListScript(AppServicePlanListOptions options, ILogger logger)
    {
        _options = options;
        _logger = new LoggerService(logger, options.Verbose);
        
        // Create a credential
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions 
        {
            TenantId = "c5772ebb-4c35-4874-abb7-1eb6cbdc90d9" // Same tenant ID used in other scripts
        });
        
        // Create the ARM client
        _armClient = new ArmClient(credential);
    }
    
    /// <summary>
    /// Runs the script
    /// </summary>
    /// <returns>The script result</returns>
    public async Task<ScriptResult> RunAsync()
    {
        _logger.LogInformation($"Running {_options.ScriptType} script");
        var result = new ScriptResult();
        
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Get the subscription
            _logger.LogInformation("Getting subscription...");
            SubscriptionResource subscription;
            
            if (!string.IsNullOrEmpty(_options.SubscriptionId))
            {
                _logger.LogInformation($"Using provided subscription ID: {_options.SubscriptionId}");
                var subscriptionId = _options.SubscriptionId;
                
                try
                {
                    subscription = await _armClient.GetSubscriptions()
                        .GetAsync(subscriptionId);
                    
                    _logger.LogSuccess($"Using subscription: {subscription.Data.DisplayName} (ID: {subscription.Id.SubscriptionId})");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to get subscription with ID {subscriptionId}: {ex.Message}");
                    result.Errors.Add($"Failed to get subscription: {ex.Message}");
                    result.Success = false;
                    return result;
                }
            }
            else
            {
                _logger.LogInformation("No subscription ID provided, using default subscription");
                
                try
                {
                    subscription = _armClient.GetDefaultSubscription();
                    _logger.LogSuccess($"Using default subscription: {subscription.Data.DisplayName} (ID: {subscription.Id.SubscriptionId})");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to get default subscription: {ex.Message}");
                    result.Errors.Add($"Failed to get default subscription: {ex.Message}");
                    result.Success = false;
                    return result;
                }
            }
            
            // Get app service plans
            var appServicePlans = await ListAppServicePlansWithAppCountsAsync(subscription);
            
            // Log the result
            if (appServicePlans.Count == 0)
            {
                _logger.LogInformation("No App Service Plans found matching the criteria.");
            }
            else
            {
                _logger.LogSuccess($"Found {appServicePlans.Count} App Service Plans.");
                
                // Display as a table
                DisplayAsTable(appServicePlans);
                
                // Add to result
                result.ProcessedResources = appServicePlans.Select(p => new ResourceInfo
                {
                    Id = p.Id,
                    Name = p.Name,
                    Type = "AppServicePlan",
                    SourceResourceGroup = p.ResourceGroup
                }).ToList();
            }
            
            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error: {ex.Message}", ex);
            result.Errors.Add($"Error: {ex.Message}");
            result.Success = false;
        }
        
        var endTime = DateTime.UtcNow;
        result.ExecutionTimeMs = (long)(endTime - startTime).TotalMilliseconds;
        
        return result;
    }
    
    /// <summary>
    /// Lists all App Service Plans in the subscription with app counts
    /// </summary>
    /// <param name="subscription">The subscription</param>
    /// <returns>A list of App Service Plan info objects</returns>
    private async Task<List<AppServicePlanInfo>> ListAppServicePlansWithAppCountsAsync(SubscriptionResource subscription)
    {
        var result = new List<AppServicePlanInfo>();
        
        _logger.LogInformation("Retrieving App Service Plans...");
        
        try
        {
            // Get all App Service Plans in the subscription
            var plans = new List<AppServicePlanResource>();
            await foreach (var plan in subscription.GetAppServicePlansAsync())
            {
                plans.Add(plan);
            }
            
            _logger.LogInformation($"Found {plans.Count} App Service Plans in subscription.");
            
            // Create a dictionary to map resource groups to apps for faster lookup
            var appsByResourceGroup = new Dictionary<string, List<WebSiteResource>>(StringComparer.OrdinalIgnoreCase);
            
            _logger.LogInformation("Retrieving Web Apps (this might take a moment)...");
            
            // Get all web apps and organize them by resource group for faster lookup
            await foreach (var app in subscription.GetWebSitesAsync())
            {
                var rgName = ExtractResourceGroupNameFromId(app.Id.ToString());
                
                if (!appsByResourceGroup.ContainsKey(rgName))
                {
                    appsByResourceGroup[rgName] = new List<WebSiteResource>();
                }
                
                appsByResourceGroup[rgName].Add(app);
            }
            
            _logger.LogInformation($"Found web apps in {appsByResourceGroup.Count} resource groups.");
            
            // Process each App Service Plan
            foreach (var plan in plans)
            {
                var planId = plan.Id;
                string resourceGroupName = ExtractResourceGroupNameFromId(planId.ToString());
                
                // Check if resource group name matches filter
                if (!string.IsNullOrEmpty(_options.ResourceGroupFilter) && 
                    !resourceGroupName.StartsWith(_options.ResourceGroupFilter, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogVerbose($"Skipping plan {plan.Data.Name} because resource group {resourceGroupName} doesn't match filter {_options.ResourceGroupFilter}");
                    continue;
                }
                
                // Check if tenant ID filter is applied
                if (!string.IsNullOrEmpty(_options.TenantId) &&
                    !plan.Data.Name.Contains(_options.TenantId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogVerbose($"Skipping plan {plan.Data.Name} because it doesn't match tenant ID filter {_options.TenantId}");
                    continue;
                }
                
                // Count apps for this plan
                int appCount = CountAppsForPlan(appsByResourceGroup, plan);
                
                _logger.LogVerbose($"App Service Plan {plan.Data.Name} has {appCount} apps");
                
                // Create AppServicePlanInfo
                var planInfo = new AppServicePlanInfo
                {
                    Id = planId.ToString(),
                    Name = plan.Data.Name,
                    ResourceGroup = resourceGroupName,
                    AppCount = appCount,
                    Sku = plan.Data.Sku.Name
                };
                
                result.Add(planInfo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving App Service Plans: {ex.Message}", ex);
        }
        
        return result;
    }
    
    /// <summary>
    /// Counts the number of apps that belong to a specific App Service Plan using ID matching only
    /// </summary>
    /// <param name="appsByResourceGroup">Dictionary mapping resource groups to their apps</param>
    /// <param name="appServicePlan">The App Service Plan</param>
    /// <returns>The count of apps in the plan</returns>
    private int CountAppsForPlan(Dictionary<string, List<WebSiteResource>> appsByResourceGroup, AppServicePlanResource appServicePlan)
    {
        int count = 0;
        string planId = appServicePlan.Id.ToString();
        
        // Check all apps in all resource groups to ensure accurate counting
        foreach (var appsInRG in appsByResourceGroup.Values)
        {
            foreach (var app in appsInRG)
            {
                try
                {
                    // Use ONLY the direct server farm ID comparison
                    // This is the most accurate method and matches how the Azure Portal counts apps
                    if (app.Data.AppServicePlanId != null && 
                        app.Data.AppServicePlanId.ToString().Equals(planId, StringComparison.OrdinalIgnoreCase))
                    {
                        count++;
                    }
                }
                catch
                {
                    // Silently continue if there are errors
                }
            }
        }
        
        return count;
    }
    
    /// <summary>
    /// Extracts the resource group name from a resource ID
    /// </summary>
    /// <param name="id">The resource ID</param>
    /// <returns>The resource group name</returns>
    private string ExtractResourceGroupNameFromId(string id)
    {
        // Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Web/serverfarms/{plan}
        var parts = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var rgIndex = Array.IndexOf(parts, "resourceGroups");
        return (rgIndex >= 0 && rgIndex + 1 < parts.Length) ? parts[rgIndex + 1] : "unknown";
    }
    
    /// <summary>
    /// Displays the App Service Plans as a formatted table
    /// </summary>
    /// <param name="plans">The list of App Service Plans</param>
    private void DisplayAsTable(List<AppServicePlanInfo> plans)
    {
        if (plans.Count == 0)
        {
            _logger.LogInformation("No App Service Plans to display.");
            return;
        }
        
        // Determine column widths
        var nameWidth = Math.Max(plans.Max(p => p.Name?.Length ?? 0), "App Service Plan".Length);
        var rgWidth = Math.Max(plans.Max(p => p.ResourceGroup?.Length ?? 0), "Resource Group".Length);
        var skuWidth = Math.Max(plans.Max(p => p.Sku?.Length ?? 0), "SKU".Length);
        var appCountWidth = Math.Max("App Count".Length, plans.Max(p => p.AppCount.ToString().Length));
        
        // Print header
        var headerFormat = $"{{0,-{nameWidth}}} | {{1,-{rgWidth}}} | {{2,-{skuWidth}}} | {{3,{appCountWidth}}}";
        Console.WriteLine(string.Format(headerFormat, "App Service Plan", "Resource Group", "SKU", "App Count"));
        
        // Print separator
        Console.WriteLine(new string('-', nameWidth) + "-+-" + 
                          new string('-', rgWidth) + "-+-" + 
                          new string('-', skuWidth) + "-+-" + 
                          new string('-', appCountWidth));
        
        // Print data
        var dataFormat = $"{{0,-{nameWidth}}} | {{1,-{rgWidth}}} | {{2,-{skuWidth}}} | {{3,{appCountWidth}}}";
        foreach (var plan in plans)
        {
            Console.WriteLine(string.Format(dataFormat, 
                plan.Name, 
                plan.ResourceGroup, 
                plan.Sku ?? "Unknown", 
                plan.AppCount));
        }
        
        // Print summary
        Console.WriteLine();
        Console.WriteLine($"Total App Service Plans: {plans.Count}");
        Console.WriteLine($"Total Apps: {plans.Sum(p => p.AppCount)}");
    }
}

/// <summary>
/// Information about an App Service Plan
/// </summary>
public class AppServicePlanInfo
{
    /// <summary>
    /// The App Service Plan ID
    /// </summary>
    public string Id { get; set; }
    
    /// <summary>
    /// The App Service Plan name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// The resource group containing the App Service Plan
    /// </summary>
    public string ResourceGroup { get; set; }
    
    /// <summary>
    /// The number of apps in the App Service Plan
    /// </summary>
    public int AppCount { get; set; }
    
    /// <summary>
    /// The SKU of the App Service Plan
    /// </summary>
    public string Sku { get; set; }
}
