using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager;
using IqonScripts.Models;
using IqonScripts.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace IqonScripts.Utils;

/// <summary>
/// Interactive menu system for command selection
/// </summary>
public class MenuSystem
{
    private readonly RootCommand _rootCommand;
    private readonly List<SubscriptionInfo> _subscriptions = new List<SubscriptionInfo>();
    private readonly LoggerService? _logger;
    
    // Default subscription ID to use if none is selected
    private const string DefaultSubscriptionId = "5c0a77d0-2891-4e4a-a39d-38d29bf072a0";

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuSystem"/> class
    /// </summary>
    /// <param name="rootCommand">The root command containing all available commands</param>
    public MenuSystem(RootCommand rootCommand)
    {
        _rootCommand = rootCommand;
        
        // Create a simple logger for console output
        var factory = LoggerFactory.Create(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        var logger = factory.CreateLogger("MenuSystem");
        _logger = new LoggerService(logger, false);
    }

    /// <summary>
    /// Displays the interactive menu and executes the selected command
    /// </summary>
    /// <returns>The exit code from the executed command</returns>
    public async Task<int> DisplayMenuAndExecuteCommandAsync()
    {
        Console.Clear();
        Console.WriteLine("===== IqonScripts - Azure Resource Management Tools =====");
        Console.WriteLine();
        
        // Get Azure subscriptions first (to prepare for the subscription selection step)
        await LoadAzureSubscriptionsAsync();
        
        // Get available commands
        var commands = _rootCommand.Subcommands.ToList();
        
        if (commands.Count == 0)
        {
            Console.WriteLine("No commands available.");
            return 1;
        }

        // Display menu options
        Console.WriteLine("Please select a command:");
        for (int i = 0; i < commands.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {commands[i].Name} - {commands[i].Description}");
        }
        Console.WriteLine("0. Exit");
        Console.WriteLine();

        // Get user choice
        int choice = GetIntInput("Enter your choice [0-" + commands.Count + "]: ", 0, commands.Count);
        
        if (choice == 0)
        {
            return 0;
        }

        // Get selected command
        var selectedCommand = commands[choice - 1];
        Console.WriteLine();
        Console.WriteLine($"[{selectedCommand.Name} selected]");
        Console.WriteLine();
        
        // Select a subscription if available
        string subscriptionId = null;
        if (_subscriptions.Count > 0)
        {
            DisplaySubscriptions();
            int subscriptionChoice = GetIntInput($"Select a subscription [0-{_subscriptions.Count}]: ", 0, _subscriptions.Count);
            
            if (subscriptionChoice > 0)
            {
                var selectedSubscription = _subscriptions[subscriptionChoice - 1];
                subscriptionId = selectedSubscription.Id;
                Console.WriteLine($"Using subscription: {selectedSubscription.Name} (ID: {selectedSubscription.Id})");
            }
            else
            {
                Console.WriteLine("No subscription selected. Using default subscription.");
            }
            Console.WriteLine();
        }

        // Build arguments for the command
        var arguments = new List<string> { selectedCommand.Name };
        
        // Add subscription ID if selected, or default if none selected
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            arguments.Add("--subscription-id");
            arguments.Add(subscriptionId);
        }
        else
        {
            // Use default subscription ID
            arguments.Add("--subscription-id");
            arguments.Add(DefaultSubscriptionId);
            Console.WriteLine($"Using default subscription ID: {DefaultSubscriptionId}");
        }
        
        // First, check if user wants to filter by tenant ID
        var tenantIdOption = selectedCommand.Options.FirstOrDefault(o => o.Name == "tenant-id");
        string tenantId = null;
        if (tenantIdOption != null)
        {
            string prompt = "Filter resources by specific tenant ID (leave empty to get all): ";
            tenantId = GetStringInput(prompt, "");
            
            if (!string.IsNullOrEmpty(tenantId))
            {
                arguments.Add("--tenant-id");
                arguments.Add(tenantId);
                Console.WriteLine($"Will only process resources for tenant ID: {tenantId}");
            }
        }
        
        // Process each option
        foreach (var option in selectedCommand.Options)
        {
            // Skip help option, subscription option (already handled), and tenant ID (already handled)
            if (option.Name == "help" || option.Name == "subscription-id" || option.Name == "tenant-id")
                continue;
                
            // Skip max-items option if tenant ID is provided
            if (option.Name == "max-items" && !string.IsNullOrEmpty(tenantId))
                continue;
                
            // Handle different option types
            if (option.ValueType == typeof(bool))
            {
                // For boolean options, use a simpler approach
                string defaultVal = "n";
                
                // Check for common default options we know about
                if (option.Name == "dry-run" || option.Name == "verbose")
                {
                    defaultVal = "n";
                }
                
                bool value = GetBoolInput($"{option.Description} (y/n) [{defaultVal}]: ");
                if (value)
                {
                    arguments.Add($"--{option.Name}");
                }
            }
            else
            {
                // For string options, use a simpler approach
                string defaultValue = "";
                
                // Check for common default options we know about
                if (option.Name == "source-group")
                {
                    defaultValue = "rg-iqon-sticos";
                }
                
                string prompt = $"{option.Description} [{defaultValue}]: ";
                string value = GetStringInput(prompt, defaultValue);
                if (!string.IsNullOrEmpty(value))
                {
                    arguments.Add($"--{option.Name}");
                    arguments.Add(value);
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Executing command: {string.Join(" ", arguments)}");
        Console.WriteLine();

        // Execute command
        return await _rootCommand.InvokeAsync(arguments.ToArray());
    }

    /// <summary>
    /// Loads Azure subscriptions
    /// </summary>
    private async Task LoadAzureSubscriptionsAsync()
    {
        try
        {
            Console.WriteLine("Loading Azure subscriptions...");
            
            // Create a credential with the specific tenant ID
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions 
            {
                TenantId = "c5772ebb-4c35-4874-abb7-1eb6cbdc90d9"
            });
            
            // Create an ARM client
            var armClient = new ArmClient(credential);
            
            // Get subscriptions
            var subscriptions = armClient.GetSubscriptions();
            
            // Clear existing subscriptions
            _subscriptions.Clear();
            
            // Add subscriptions to the list
            await foreach (var subscription in subscriptions.GetAllAsync())
            {
                _subscriptions.Add(new SubscriptionInfo
                {
                    Id = subscription.Data.SubscriptionId,
                    Name = subscription.Data.DisplayName
                });
                
                Console.WriteLine($"Found subscription: {subscription.Data.DisplayName}");
            }
            
            if (_subscriptions.Count == 0)
            {
                Console.WriteLine("No subscriptions found.");
            }
            else
            {
                Console.WriteLine($"Found {_subscriptions.Count} subscriptions.");
            }
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load Azure subscriptions: {ex.Message}", ex);
            Console.WriteLine($"Failed to load Azure subscriptions: {ex.Message}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Displays the available Azure subscriptions
    /// </summary>
    private void DisplaySubscriptions()
    {
        Console.WriteLine("Available Azure subscriptions:");
        for (int i = 0; i < _subscriptions.Count; i++)
        {
            Console.WriteLine($"{i + 1}. {_subscriptions[i].Name} (ID: {_subscriptions[i].Id})");
        }
        Console.WriteLine("0. Use default subscription");
        Console.WriteLine();
    }

    /// <summary>
    /// Gets a string input from the user
    /// </summary>
    /// <param name="prompt">The prompt to display</param>
    /// <param name="defaultValue">The default value if the user presses enter</param>
    /// <returns>The user input or default value</returns>
    private string GetStringInput(string prompt, string defaultValue = "")
    {
        Console.Write(prompt);
        string input = Console.ReadLine() ?? "";
        
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }
        
        return input;
    }

    /// <summary>
    /// Gets an integer input from the user within a specified range
    /// </summary>
    /// <param name="prompt">The prompt to display</param>
    /// <param name="min">The minimum allowed value</param>
    /// <param name="max">The maximum allowed value</param>
    /// <returns>The user input as an integer</returns>
    private int GetIntInput(string prompt, int min, int max)
    {
        while (true)
        {
            Console.Write(prompt);
            string input = Console.ReadLine() ?? "";
            
            if (int.TryParse(input, out int result) && result >= min && result <= max)
            {
                return result;
            }
            
            Console.WriteLine($"Please enter a number between {min} and {max}.");
        }
    }

    /// <summary>
    /// Gets a boolean input from the user
    /// </summary>
    /// <param name="prompt">The prompt to display</param>
    /// <returns>The user input as a boolean</returns>
    private bool GetBoolInput(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string input = Console.ReadLine()?.ToLower() ?? "";
            
            if (string.IsNullOrWhiteSpace(input) || input == "n" || input == "no")
            {
                return false;
            }
            
            if (input == "y" || input == "yes")
            {
                return true;
            }
            
            Console.WriteLine("Please enter 'y' or 'n'.");
        }
    }
}
