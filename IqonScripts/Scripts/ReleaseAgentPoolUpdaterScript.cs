using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using IqonScripts.Models;
using IqonScripts.Services;
using IqonScripts.Utils;
using Microsoft.Extensions.Logging;

namespace IqonScripts.Scripts;

/// <summary>
/// Script for updating Agent Pools in Azure DevOps release definitions
/// </summary>
public class ReleaseAgentPoolUpdaterScript
{
    private readonly CommandOptions _options;
    private readonly ILogger _logger;
    private readonly LoggerService _loggerService;
    private readonly AzureAuthenticationService _authService;
    private readonly string _projectName = "HK";
    private readonly string _newAgentPoolName = "Iqon Sticos VMSS 2";
    private readonly string _azureDevOpsUrl = "https://dev.azure.com/hkreklame/";

    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseAgentPoolUpdaterScript"/> class
    /// </summary>
    /// <param name="options">The command options</param>
    /// <param name="logger">The logger</param>
    public ReleaseAgentPoolUpdaterScript(CommandOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _loggerService = new LoggerService(logger, options.Verbose);
        _authService = new AzureAuthenticationService(_loggerService);
    }

    /// <summary>
    /// Runs the release agent pool updater script
    /// </summary>
    /// <returns>The script result</returns>
    public async Task<ScriptResult> RunAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ScriptResult
        {
            DryRun = _options.DryRun,
            Success = true
        };
        
        try
        {
            _loggerService.LogInformation("Starting Release Agent Pool Updater script");
            _loggerService.LogInformation($"Azure DevOps organization: {_azureDevOpsUrl}");
            _loggerService.LogInformation($"Project: {_projectName}");
            _loggerService.LogInformation($"New Agent Pool: {_newAgentPoolName}");
            _loggerService.LogInformation($"Dry run: {_options.DryRun}");
            
            // Get an Azure AD token for Azure DevOps
            string accessToken = await GetAzureDevOpsAccessTokenAsync();
            
            // Create the Azure DevOps service
            var devOpsService = new AzureDevOpsService(
                _loggerService, 
                _azureDevOpsUrl, 
                _newAgentPoolName);
            
            // Initialize the Azure DevOps service
            await devOpsService.InitializeAsync(accessToken);
            
            // Validate that the specified agent pool exists
            bool agentPoolExists = await devOpsService.ValidateAgentPoolExistsAsync(_projectName);
            if (!agentPoolExists)
            {
                _loggerService.LogError($"Agent pool '{_newAgentPoolName}' does not exist. Please check the name and try again.");
                result.Success = false;
                result.Errors.Add($"Agent pool '{_newAgentPoolName}' does not exist");
                return result;
            }
            
            // Get release definitions matching the pattern
            var releaseDefinitions = await devOpsService.GetMatchingReleaseDefinitionsAsync(
                _projectName, 
                _options.TenantId);
                
            if (releaseDefinitions.Count == 0)
            {
                if (!string.IsNullOrEmpty(_options.TenantId))
                {
                    _loggerService.LogWarning($"No release definitions found with tenant ID: {_options.TenantId}");
                }
                else
                {
                    _loggerService.LogWarning("No release definitions found matching the pattern 'iqon-sticos-{tenantId}'");
                }
                
                return result;
            }
            
            _loggerService.LogInformation($"Found {releaseDefinitions.Count} release definitions to update");
            
            // Apply max items limit if specified
            if (_options.MaxItems > 0 && releaseDefinitions.Count > _options.MaxItems)
            {
                _loggerService.LogInformation($"Limiting to {_options.MaxItems} release definitions (out of {releaseDefinitions.Count} discovered)");
                releaseDefinitions = releaseDefinitions.GetRange(0, _options.MaxItems);
            }
            
            // Update each release definition
            foreach (var definition in releaseDefinitions)
            {
                _loggerService.LogInformation($"Processing release definition: {definition.Name} (ID: {definition.Id})");
                result.ProcessedResources.Add(new ResourceInfo
                {
                    Id = definition.Id.ToString(),
                    Name = definition.Name,
                    Type = "ReleaseDefinition",
                    TenantId = definition.TenantId
                });
                
                bool updateSuccess = await devOpsService.UpdateReleaseAgentPoolAsync(
                    _projectName, 
                    definition, 
                    _options.DryRun);
                    
                if (!updateSuccess)
                {
                    result.Success = false;
                    result.Errors.Add($"Failed to update release definition: {definition.Name}");
                }
            }
            
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
            
            _loggerService.LogError("An error occurred while running the Release Agent Pool Updater script", ex);
            
            return new ScriptResult
            {
                Success = false,
                DryRun = _options.DryRun,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                Errors = new List<string> { ex.Message }
            };
        }
    }
    
    /// <summary>
    /// Gets a Personal Access Token (PAT) for Azure DevOps
    /// </summary>
    /// <returns>The PAT</returns>
    private Task<string> GetAzureDevOpsAccessTokenAsync()
    {
        try
        {
            // Check if a PAT was provided in the command options
            if (string.IsNullOrEmpty(_options.Pat))
            {
                // For demo purposes, use a fake PAT
                string fakePat = "demo_pat_for_testing_only";
                _loggerService.LogInformation("Using demo Personal Access Token for Azure DevOps");
                _loggerService.LogWarning("This is a DEMO PAT and will not work with real Azure DevOps. Please provide a valid PAT using the --pat option.");
                return Task.FromResult(fakePat);
            }
            
            _loggerService.LogInformation("Using provided Personal Access Token for Azure DevOps");
            _loggerService.LogSuccess("Successfully set up Personal Access Token for Azure DevOps");
            
            return Task.FromResult(_options.Pat);
        }
        catch (Exception ex)
        {
            _loggerService.LogError("Failed to set up Personal Access Token for Azure DevOps", ex);
            throw;
        }
    }
}
