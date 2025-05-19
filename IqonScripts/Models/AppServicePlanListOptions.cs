namespace IqonScripts.Models;

/// <summary>
/// Options for the list-app-service-plans command
/// </summary>
public class AppServicePlanListOptions
{
    /// <summary>
    /// The subscription ID to use for Azure operations
    /// </summary>
    public string? SubscriptionId { get; set; }
    
    /// <summary>
    /// Pattern to filter resource groups (default: "rg-iqon-sticos")
    /// </summary>
    public string ResourceGroupFilter { get; set; } = "rg-iqon-sticos";
    
    /// <summary>
    /// Specific tenant ID to filter resources by
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// Whether to show verbose logging
    /// </summary>
    public bool Verbose { get; set; } = false;
    
    /// <summary>
    /// The type of script to run
    /// </summary>
    public string ScriptType { get; set; } = "list-app-service-plans";
}
