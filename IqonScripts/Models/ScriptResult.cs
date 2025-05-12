using System.Collections.Generic;

namespace IqonScripts.Models;

/// <summary>
/// Represents the result of a script execution
/// </summary>
public class ScriptResult
{
    /// <summary>
    /// Gets or sets whether the script execution was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the list of resources that were processed
    /// </summary>
    public List<ResourceInfo> ProcessedResources { get; set; } = new();
    
    /// <summary>
    /// Gets or sets the list of errors that occurred during script execution
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Gets or sets whether the script was run in dry run mode
    /// </summary>
    public bool DryRun { get; set; }
    
    /// <summary>
    /// Gets or sets the execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }
    
    /// <summary>
    /// Returns a summary of the script execution
    /// </summary>
    public string GetSummary()
    {
        var mode = DryRun ? "DRY RUN" : "EXECUTION";
        var statusText = Success ? "Successfully completed" : "Completed with errors";
        
        return $"{mode} {statusText} in {ExecutionTimeMs}ms. " +
               $"Processed {ProcessedResources.Count} resources. " +
               $"Encountered {Errors.Count} errors.";
    }
}
