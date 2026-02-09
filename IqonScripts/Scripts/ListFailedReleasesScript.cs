using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IqonScripts.Models;
using IqonScripts.Services;
using IqonScripts.Utils;
using Microsoft.Extensions.Logging;

namespace IqonScripts.Scripts;

/// <summary>
/// Script to list failed releases from Azure DevOps matching the iqon-sticos-* pattern
/// </summary>
public class ListFailedReleasesScript
{
    private readonly ListFailedReleasesOptions _options;
    private readonly LoggerService _logger;

    // Azure DevOps constants (same as ReleaseAgentPoolUpdaterScript)
    private readonly string _projectName = "HK";
    private readonly string _azureDevOpsUrl = "https://dev.azure.com/hkreklame/";

    /// <summary>
    /// Initializes a new instance of the <see cref="ListFailedReleasesScript"/> class
    /// </summary>
    /// <param name="options">The command options</param>
    /// <param name="logger">The logger</param>
    public ListFailedReleasesScript(ListFailedReleasesOptions options, ILogger logger)
    {
        _options = options;
        _logger = new LoggerService(logger, options.Verbose);
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
            // Check for PAT
            string? accessToken = _options.Pat;
            if (string.IsNullOrEmpty(accessToken))
            {
                accessToken = Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("No PAT provided. Using mock data for demo purposes.");
                _logger.LogInformation("To use real data, provide a PAT via --pat option or AZURE_DEVOPS_PAT environment variable.");
            }

            // Create the Azure DevOps service
            var devOpsService = new AzureDevOpsService(_logger, _azureDevOpsUrl, "unused");

            if (!string.IsNullOrEmpty(accessToken))
            {
                await devOpsService.InitializeAsync(accessToken);
            }
            
            // Get failed releases
            _logger.LogInformation($"Searching for failed releases...");
            if (!string.IsNullOrEmpty(_options.TenantId))
            {
                _logger.LogInformation($"Filtering by tenant ID: {_options.TenantId}");
            }

            var failedReleases = await devOpsService.GetFailedReleasesAsync(
                _projectName,
                _options.TenantId,
                _options.StartDate,
                _options.EndDate);

            // Log the result
            if (failedReleases.Count == 0)
            {
                _logger.LogInformation("No failed releases found matching the criteria.");
            }
            else
            {
                _logger.LogSuccess($"Found {failedReleases.Count} failed releases.");

                // Display as a table
                DisplayAsTable(failedReleases);

                // Add to result
                result.ProcessedResources = failedReleases.Select(r => new ResourceInfo
                {
                    Id = r.Id.ToString(),
                    Name = r.Name,
                    Type = "Release"
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
    /// Displays the failed releases as a formatted table
    /// </summary>
    /// <param name="releases">The list of failed releases</param>
    private void DisplayAsTable(List<FailedReleaseInfo> releases)
    {
        if (releases.Count == 0)
        {
            _logger.LogInformation("No failed releases to display.");
            return;
        }

        // Sort by creation date descending (most recent first)
        releases = releases.OrderByDescending(r => r.CreatedOn).ToList();

        // Determine column widths
        var idWidth = Math.Max(releases.Max(r => r.Id.ToString().Length), "ID".Length);
        var nameWidth = Math.Max(releases.Max(r => r.Name?.Length ?? 0), "Release Name".Length);
        var tenantWidth = Math.Max(releases.Max(r => r.TenantId?.Length ?? 0), "Tenant".Length);
        var dateWidth = Math.Max("Created".Length, 19); // YYYY-MM-DD HH:MM:SS
        var envWidth = Math.Max(releases.Max(r => string.Join(", ", r.FailedEnvironments).Length), "Failed Environments".Length);

        // Limit env width to avoid very long lines
        envWidth = Math.Min(envWidth, 40);

        // Print header
        var headerFormat = $"{{0,-{idWidth}}} | {{1,-{nameWidth}}} | {{2,-{tenantWidth}}} | {{3,-{dateWidth}}} | {{4,-{envWidth}}}";
        Console.WriteLine(string.Format(headerFormat, "ID", "Release Name", "Tenant", "Created", "Failed Environments"));

        // Print separator
        Console.WriteLine(new string('-', idWidth) + "-+-" +
                          new string('-', nameWidth) + "-+-" +
                          new string('-', tenantWidth) + "-+-" +
                          new string('-', dateWidth) + "-+-" +
                          new string('-', envWidth));

        // Print data
        var dataFormat = $"{{0,-{idWidth}}} | {{1,-{nameWidth}}} | {{2,-{tenantWidth}}} | {{3,-{dateWidth}}} | {{4,-{envWidth}}}";
        foreach (var release in releases)
        {
            var envList = string.Join(", ", release.FailedEnvironments);
            if (envList.Length > envWidth)
            {
                envList = envList.Substring(0, envWidth - 3) + "...";
            }

            Console.WriteLine(string.Format(dataFormat,
                release.Id,
                release.Name,
                release.TenantId,
                release.CreatedOn.ToString("yyyy-MM-dd HH:mm:ss"),
                envList));
        }

        // Print summary
        Console.WriteLine();
        Console.WriteLine($"Total failed releases: {releases.Count}");

        // Get unique tenant IDs
        var uniqueTenantIds = releases.Select(r => r.TenantId).Distinct().OrderBy(t => t).ToList();
        Console.WriteLine($"Affected tenants: {uniqueTenantIds.Count}");

        // Print tenant IDs for easy copying
        Console.WriteLine();
        Console.WriteLine("Tenant IDs (comma-separated):");
        Console.WriteLine(string.Join(",", uniqueTenantIds));

        Console.WriteLine();
        Console.WriteLine("Tenant IDs (one per line):");
        foreach (var tid in uniqueTenantIds)
        {
            Console.WriteLine(tid);
        }
    }
}
