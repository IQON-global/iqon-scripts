namespace IqonScripts.Models;

/// <summary>
/// Options for the list-failed-releases command
/// </summary>
public class ListFailedReleasesOptions
{
    /// <summary>
    /// The subscription ID to use for Azure operations
    /// </summary>
    public string? SubscriptionId { get; set; }

    /// <summary>
    /// Specific tenant ID to filter releases by (from the release name pattern iqon-sticos-{tenantId})
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Start date for the time range filter
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date for the time range filter
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Personal Access Token for Azure DevOps authentication
    /// </summary>
    public string? Pat { get; set; }

    /// <summary>
    /// Whether to show verbose logging
    /// </summary>
    public bool Verbose { get; set; } = false;

    /// <summary>
    /// The type of script to run
    /// </summary>
    public string ScriptType { get; set; } = "list-failed-releases";
}
