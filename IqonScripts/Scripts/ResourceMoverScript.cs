using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Azure.ResourceManager;
using IqonScripts.Models;
using IqonScripts.Services;
using IqonScripts.Utils;
using Microsoft.Extensions.Logging;

namespace IqonScripts.Scripts;

/// <summary>
/// Script for moving Azure resources between resource groups
/// </summary>
public class ResourceMoverScript
{
    private readonly CommandOptions _options;
    private readonly ILogger _logger;
    private readonly LoggerService _loggerService;
    private readonly AzureAuthenticationService _authService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceMoverScript"/> class
    /// </summary>
    /// <param name="options">The command options</param>
    /// <param name="logger">The logger</param>
    public ResourceMoverScript(CommandOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _loggerService = new LoggerService(logger, options.Verbose);
        _authService = new AzureAuthenticationService(_loggerService);
    }

    /// <summary>
    /// Runs the resource mover script
    /// </summary>
    /// <returns>The script result</returns>
    public async Task<ScriptResult> RunAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _loggerService.LogInformation("Starting Resource Mover script");
            _loggerService.LogInformation($"Source resource group: {_options.SourceResourceGroup}");
            _loggerService.LogInformation($"Dry run: {_options.DryRun}");
            
            // Authenticate with Azure
            var armClient = await _authService.GetArmClientAsync(_options.SubscriptionId);
            
            // Create the resource service
            var resourceService = new AzureResourceService(armClient, _loggerService, _options.SourceResourceGroup);
            
            // Initialize the resource service
            await resourceService.InitializeAsync(_options.SourceResourceGroup);
            
            // Discover resources to move, passing the optional target resource group
            var resourcesToMove = await resourceService.DiscoverResourcesToMoveAsync(_options.TargetResourceGroup);
            
            // Apply max items limit if specified
            if (_options.MaxItems > 0 && resourcesToMove.Count > _options.MaxItems)
            {
                _loggerService.LogInformation($"Limiting to {_options.MaxItems} resources (out of {resourcesToMove.Count} discovered)");
                resourcesToMove = resourcesToMove.Take(_options.MaxItems).ToList();
            }
            
            if (resourcesToMove.Count == 0)
            {
                _loggerService.LogWarning("No resources found to move");
                return new ScriptResult
                {
                    Success = true,
                    DryRun = _options.DryRun,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            
            _loggerService.LogInformation($"Found {resourcesToMove.Count} resources to move");
            
            // Display resources to move
            foreach (var resource in resourcesToMove)
            {
                _loggerService.LogInformation($"  {resource}");
            }
            
            // Move resources
            var result = await resourceService.MoveResourcesAsync(resourcesToMove, _options.DryRun);
            
            // Stop the stopwatch
            stopwatch.Stop();
            
            // Set the execution time
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            
            // Log the summary
            _loggerService.LogInformation(result.GetSummary());
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _loggerService.LogError("An error occurred while running the Resource Mover script", ex);
            
            return new ScriptResult
            {
                Success = false,
                DryRun = _options.DryRun,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
