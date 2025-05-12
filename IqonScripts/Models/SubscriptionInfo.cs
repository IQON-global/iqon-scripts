namespace IqonScripts.Models;

/// <summary>
/// Represents Azure subscription information
/// </summary>
public class SubscriptionInfo
{
    /// <summary>
    /// Gets or sets the subscription ID
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the subscription name
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
