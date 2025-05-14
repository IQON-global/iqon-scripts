namespace IqonScripts.Models;

public class CommandOptions
{
    /// <summary>
    /// The subscription ID to use for Azure operations
    /// </summary>
    public string? SubscriptionId { get; set; }
    
    /// <summary>
    /// The source resource group where orphaned resources are located
    /// </summary>
    public string SourceResourceGroup { get; set; } = "rg-iqon-sticos";
    
    /// <summary>
    /// The target resource group where resources should be moved to (optional)
    /// If not specified, it will be discovered automatically based on tenant ID
    /// </summary>
    public string? TargetResourceGroup { get; set; } = "rg-iqon-sticos-2";
    
    /// <summary>
    /// Maximum number of resources to move in a single operation (for testing)
    /// If not specified or set to 0, all discovered resources will be moved
    /// </summary>
    public int MaxItems { get; set; } = 0;
    
    /// <summary>
    /// Specific tenant ID to filter resources by
    /// If specified, only resources for this tenant ID will be included
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// Whether to run in dry run mode (preview only)
    /// </summary>
    public bool DryRun { get; set; } = false;
    
    /// <summary>
    /// Whether to show verbose logging
    /// </summary>
    public bool Verbose { get; set; } = false;
    
    /// <summary>
    /// Personal Access Token (PAT) for Azure DevOps authentication
    /// </summary>
    public string? Pat { get; set; }
    
    /// <summary>
    /// The type of script to run
    /// </summary>
    public string ScriptType { get; set; } = "move-resources";
}
