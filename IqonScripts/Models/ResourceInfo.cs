namespace IqonScripts.Models;

/// <summary>
/// Represents information about an Azure resource
/// </summary>
public class ResourceInfo
{
    /// <summary>
    /// The resource ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// The resource name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The resource type
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// The extracted tenant ID from the resource name
    /// </summary>
    public string? TenantId { get; set; }
    
    /// <summary>
    /// The source resource group
    /// </summary>
    public string SourceResourceGroup { get; set; } = string.Empty;
    
    /// <summary>
    /// The target resource group
    /// </summary>
    public string TargetResourceGroup { get; set; } = string.Empty;

    /// <summary>
    /// Returns a string that represents the current object
    /// </summary>
    public override string ToString()
    {
        return $"{Name} ({Type}) -> {TargetResourceGroup}";
    }
}
