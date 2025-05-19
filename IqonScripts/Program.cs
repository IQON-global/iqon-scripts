using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Threading.Tasks;
using IqonScripts.Models;
using IqonScripts.Scripts;
using IqonScripts.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace IqonScripts;

/// <summary>
/// Main program entry point
/// </summary>
class Program
{
    /// <summary>
    /// Main entry point
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code</returns>
    static async Task<int> Main(string[] args)
    {
        // Create a root command
        var rootCommand = new RootCommand("IqonScripts - Collection of Azure resource management scripts");

        // Add the move-resources command
        var moveResourcesCommand = CreateMoveResourcesCommand();
        rootCommand.AddCommand(moveResourcesCommand);
        
        // Add the update-release-agent-pools command
        var updateReleaseAgentPoolsCommand = CreateUpdateReleaseAgentPoolsCommand();
        rootCommand.AddCommand(updateReleaseAgentPoolsCommand);
        
        // Add the update-keyvault-access-policies command
        var updateKeyVaultAccessPoliciesCommand = CreateUpdateKeyVaultAccessPoliciesCommand();
        rootCommand.AddCommand(updateKeyVaultAccessPoliciesCommand);
        
        // Add the list-app-service-plans command
        var listAppServicePlansCommand = CreateListAppServicePlansCommand();
        rootCommand.AddCommand(listAppServicePlansCommand);

        // If no arguments provided, show the interactive menu
        if (args.Length == 0)
        {
            var menuSystem = new MenuSystem(rootCommand);
            return await menuSystem.DisplayMenuAndExecuteCommandAsync();
        }
        
        // Otherwise, parse the command line arguments and execute the command
        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Creates the update-release-agent-pools command
    /// </summary>
    private static Command CreateUpdateReleaseAgentPoolsCommand()
    {
        var command = new Command("update-release-agent-pools", "Update Agent Pools in Azure DevOps release definitions");

        // Add options
        var subscriptionOption = new Option<string>(
            new string[] { "--subscription-id", "-i" },
            description: "Azure subscription ID") 
            { IsRequired = false };

        var tenantIdOption = new Option<string>(
            new string[] { "--tenant-id", "-id" },
            description: "Filter releases by specific tenant ID") 
            { IsRequired = false };
        
        var maxItemsOption = new Option<int>(
            new string[] { "--max-items", "-m" },
            description: "Maximum number of releases to update (for testing, 0 means no limit)") 
            { IsRequired = false };
        maxItemsOption.SetDefaultValue(0);
        
        var dryRunOption = new Option<bool>(
            new string[] { "--dry-run", "-d" },
            description: "Run in dry run mode (no changes will be made)") 
            { IsRequired = false };
        dryRunOption.SetDefaultValue(false);

        var verboseOption = new Option<bool>(
            new string[] { "--verbose", "-v" },
            description: "Enable verbose logging") 
            { IsRequired = false };
        verboseOption.SetDefaultValue(false);
        
        var patOption = new Option<string>(
            new string[] { "--pat", "-p" },
            description: "Personal Access Token (PAT) for Azure DevOps authentication")
            { IsRequired = false };

        command.AddOption(subscriptionOption);
        command.AddOption(tenantIdOption);
        command.AddOption(maxItemsOption);
        command.AddOption(dryRunOption);
        command.AddOption(verboseOption);
        command.AddOption(patOption);

        // Set the handler
        command.SetHandler(async (string subscriptionId, string tenantId, int maxItems, bool dryRun, bool verbose, string pat) =>
        {
            // Create the logger
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<Program>();

            try
            {
                // Create command options
                var options = new CommandOptions
                {
                    SubscriptionId = subscriptionId,
                    TenantId = tenantId,
                    MaxItems = maxItems,
                    DryRun = dryRun,
                    Verbose = verbose,
                    Pat = pat,
                    ScriptType = "update-release-agent-pools"
                };

                // Create and run the script
                var script = new ReleaseAgentPoolUpdaterScript(options, logger);
                var result = await script.RunAsync();

                // Return success or failure
                Environment.ExitCode = result.Success ? 0 : 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unhandled exception occurred");
                Environment.ExitCode = 1;
            }
        }, subscriptionOption, tenantIdOption, maxItemsOption, dryRunOption, verboseOption, patOption);

        return command;
    }

    /// <summary>
    /// Creates the move-resources command
    /// </summary>
    private static Command CreateMoveResourcesCommand()
    {
        var command = new Command("move-resources", "Move Azure resources between resource groups");

        // Add options
        var subscriptionOption = new Option<string>(
            new string[] { "--subscription-id", "-i" },
            description: "Azure subscription ID") 
            { IsRequired = false };
        
        var sourceGroupOption = new Option<string>(
            new string[] { "--source-group", "-s" },
            description: "Source resource group name") 
            { IsRequired = false };
        sourceGroupOption.SetDefaultValue("rg-iqon-sticos");
        
        var targetGroupOption = new Option<string>(
            new string[] { "--target-group", "-t" },
            description: "Target resource group name (if not specified, it will be discovered based on tenant ID)") 
            { IsRequired = false };
        targetGroupOption.SetDefaultValue("rg-iqon-sticos-2");

        var tenantIdOption = new Option<string>(
            new string[] { "--tenant-id", "-id" },
            description: "Filter resources by specific tenant ID") 
            { IsRequired = false };
        
        var maxItemsOption = new Option<int>(
            new string[] { "--max-items", "-m" },
            description: "Maximum number of resources to move (for testing, 0 means no limit)") 
            { IsRequired = false };
        maxItemsOption.SetDefaultValue(0);
        
        var dryRunOption = new Option<bool>(
            new string[] { "--dry-run", "-d" },
            description: "Run in dry run mode (no changes will be made)") 
            { IsRequired = false };
        dryRunOption.SetDefaultValue(false);

        var verboseOption = new Option<bool>(
            new string[] { "--verbose", "-v" },
            description: "Enable verbose logging") 
            { IsRequired = false };
        verboseOption.SetDefaultValue(false);

        command.AddOption(subscriptionOption);
        command.AddOption(sourceGroupOption);
        command.AddOption(targetGroupOption);
        command.AddOption(tenantIdOption);
        command.AddOption(maxItemsOption);
        command.AddOption(dryRunOption);
        command.AddOption(verboseOption);

        // Set the handler
        command.SetHandler(async (string subscriptionId, string sourceGroup, string targetGroup, string tenantId, int maxItems, bool dryRun, bool verbose) =>
        {
            // Create the logger
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<Program>();

            try
            {
                // Create command options
                var options = new CommandOptions
                {
                    SubscriptionId = subscriptionId,
                    SourceResourceGroup = sourceGroup,
                    TargetResourceGroup = targetGroup,
                    TenantId = tenantId,
                    MaxItems = maxItems,
                    DryRun = dryRun,
                    Verbose = verbose,
                    ScriptType = "move-resources"
                };

                // Create and run the script
                var script = new ResourceMoverScript(options, logger);
                var result = await script.RunAsync();

                // Return success or failure
                Environment.ExitCode = result.Success ? 0 : 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unhandled exception occurred");
                Environment.ExitCode = 1;
            }
        }, subscriptionOption, sourceGroupOption, targetGroupOption, tenantIdOption, maxItemsOption, dryRunOption, verboseOption);

        return command;
    }
    
    /// <summary>
    /// Creates the update-keyvault-access-policies command
    /// </summary>
    private static Command CreateUpdateKeyVaultAccessPoliciesCommand()
    {
        var command = new Command("update-keyvault-access-policies", "Update access policies in Azure Key Vaults matching a naming pattern");

        // Add options
        var subscriptionOption = new Option<string>(
            new string[] { "--subscription-id", "-i" },
            description: "Azure subscription ID") 
            { IsRequired = false };

        var tenantIdOption = new Option<string>(
            new string[] { "--tenant-id", "-id" },
            description: "Filter key vaults by specific tenant ID") 
            { IsRequired = false };
            
        var objectIdOption = new Option<string>(
            new string[] { "--object-id", "-o" },
            description: "Entra Object ID to add to the access policy") 
            { IsRequired = false };
        objectIdOption.SetDefaultValue("a7351a1e-ad4a-4c4a-a4ca-bea0c51d9b2a");
        
        var accessLevelOption = new Option<AccessLevel>(
            new string[] { "--access-level", "-a" },
            description: "Access level to grant (SecretsReadOnly, SecretsReadWrite, CertificatesReadOnly, KeysReadOnly, FullAccess)") 
            { IsRequired = false };
        accessLevelOption.SetDefaultValue(AccessLevel.SecretsReadOnly);
        
        var dryRunOption = new Option<bool>(
            new string[] { "--dry-run", "-d" },
            description: "Run in dry run mode (no changes will be made)") 
            { IsRequired = false };
        dryRunOption.SetDefaultValue(false);

        var verboseOption = new Option<bool>(
            new string[] { "--verbose", "-v" },
            description: "Enable verbose logging") 
            { IsRequired = false };
        verboseOption.SetDefaultValue(false);

        command.AddOption(subscriptionOption);
        command.AddOption(tenantIdOption);
        command.AddOption(objectIdOption);
        command.AddOption(accessLevelOption);
        command.AddOption(dryRunOption);
        command.AddOption(verboseOption);

        // Set the handler
        command.SetHandler(async (string subscriptionId, string tenantId, string objectId, AccessLevel accessLevel, bool dryRun, bool verbose) =>
        {
            // Create the logger
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<Program>();

            try
            {
                // Create command options
                var options = new KeyVaultAccessPolicyOptions
                {
                    SubscriptionId = subscriptionId,
                    TenantId = tenantId,
                    ObjectId = objectId,
                    AccessLevel = accessLevel,
                    DryRun = dryRun,
                    Verbose = verbose,
                    ScriptType = "update-keyvault-access-policies"
                };

                // Create and run the script
                var script = new KeyVaultAccessPolicyScript(options, logger);
                var result = await script.RunAsync();

                // Return success or failure
                Environment.ExitCode = result.Success ? 0 : 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unhandled exception occurred");
                Environment.ExitCode = 1;
            }
        }, subscriptionOption, tenantIdOption, objectIdOption, accessLevelOption, dryRunOption, verboseOption);

        return command;
    }
    
    /// <summary>
    /// Creates the list-app-service-plans command
    /// </summary>
    private static Command CreateListAppServicePlansCommand()
    {
        var command = new Command("list-app-service-plans", "List App Service Plans with their app count and resource group");

        // Add options
        var subscriptionOption = new Option<string>(
            new string[] { "--subscription-id", "-i" },
            description: "Azure subscription ID") 
            { IsRequired = false };

        var resourceGroupFilterOption = new Option<string>(
            new string[] { "--resource-group-filter", "-f" },
            description: "Pattern to filter resource groups") 
            { IsRequired = false };
        resourceGroupFilterOption.SetDefaultValue("rg-iqon-sticos");
            
        var tenantIdOption = new Option<string>(
            new string[] { "--tenant-id", "-id" },
            description: "Filter by specific tenant ID") 
            { IsRequired = false };
        
        var verboseOption = new Option<bool>(
            new string[] { "--verbose", "-v" },
            description: "Enable verbose logging") 
            { IsRequired = false };
        verboseOption.SetDefaultValue(false);

        command.AddOption(subscriptionOption);
        command.AddOption(resourceGroupFilterOption);
        command.AddOption(tenantIdOption);
        command.AddOption(verboseOption);

        // Set the handler
        command.SetHandler(async (string subscriptionId, string resourceGroupFilter, string tenantId, bool verbose) =>
        {
            // Create the logger
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<Program>();

            try
            {
                // Create command options
                var options = new AppServicePlanListOptions
                {
                    SubscriptionId = subscriptionId,
                    ResourceGroupFilter = resourceGroupFilter,
                    TenantId = tenantId,
                    Verbose = verbose,
                    ScriptType = "list-app-service-plans"
                };

                // Create and run the script
                var script = new AppServicePlanListScript(options, logger);
                var result = await script.RunAsync();

                // Return success or failure
                Environment.ExitCode = result.Success ? 0 : 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unhandled exception occurred");
                Environment.ExitCode = 1;
            }
        }, subscriptionOption, resourceGroupFilterOption, tenantIdOption, verboseOption);

        return command;
    }
}
