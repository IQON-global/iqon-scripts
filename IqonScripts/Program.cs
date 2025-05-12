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
        command.AddOption(maxItemsOption);
        command.AddOption(dryRunOption);
        command.AddOption(verboseOption);

        // Set the handler
        command.SetHandler(async (string subscriptionId, string sourceGroup, string targetGroup, int maxItems, bool dryRun, bool verbose) =>
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
        }, subscriptionOption, sourceGroupOption, targetGroupOption, maxItemsOption, dryRunOption, verboseOption);

        return command;
    }
}
