using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using IqonScripts.Utils;

namespace IqonScripts.Utils;

/// <summary>
/// Service for authenticating with Azure
/// </summary>
public class AzureAuthenticationService
{
    private readonly LoggerService _logger;
    private TokenCredential? _credential;
    private ArmClient? _armClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureAuthenticationService"/> class.
    /// </summary>
    /// <param name="logger">The logger service</param>
    public AzureAuthenticationService(LoggerService logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Default subscription ID to use if none is specified
    /// </summary>
    private const string DefaultSubscriptionId = "5c0a77d0-2891-4e4a-a39d-38d29bf072a0";

    /// <summary>
    /// Authenticates with Azure and returns an ARM client
    /// </summary>
    /// <param name="subscriptionId">Optional subscription ID to use</param>
    /// <returns>An authenticated ARM client</returns>
    public async Task<ArmClient> GetArmClientAsync(string? subscriptionId = null)
    {
        if (_armClient != null)
        {
            return _armClient;
        }

        try
        {
            _logger.LogInformation("Authenticating with Azure...");
            
            // Use DefaultAzureCredential which supports interactive login
            _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                // Set the specific tenant ID for authentication
                TenantId = "c5772ebb-4c35-4874-abb7-1eb6cbdc90d9",
                
                // Uncomment for troubleshooting authentication issues
                // ExcludeInteractiveBrowserCredential = false,
                // ExcludeManagedIdentityCredential = true,
                // ExcludeSharedTokenCacheCredential = true,
                // ExcludeVisualStudioCredential = true,
                // ExcludeVisualStudioCodeCredential = true,
                // ExcludeAzureCliCredential = false,
                // ExcludeEnvironmentCredential = true
            });

            // Use specific subscription ID when creating the ArmClient
            var effectiveSubscriptionId = subscriptionId ?? DefaultSubscriptionId;
            _logger.LogInformation($"Creating ArmClient with subscription ID: {effectiveSubscriptionId}");
            _armClient = new ArmClient(_credential, effectiveSubscriptionId);
            _logger.LogSuccess("Successfully authenticated with Azure");
            
            // Test authentication by getting subscriptions
            var subscriptions = _armClient.GetSubscriptions();
            int count = 0;
            SubscriptionResource selectedSubscription = null;
            
            // List available subscriptions
            await foreach (var subscription in subscriptions.GetAllAsync())
            {
                count++;
                _logger.LogVerbose($"Found subscription: {subscription.Data.DisplayName} (ID: {subscription.Data.SubscriptionId})");
                
                // Select the subscription if it matches the provided ID or the default if none provided
                bool isRequestedSubscription = 
                    (subscriptionId != null && subscription.Data.SubscriptionId.Equals(subscriptionId, StringComparison.OrdinalIgnoreCase)) ||
                    (subscriptionId == null && subscription.Data.SubscriptionId.Equals(DefaultSubscriptionId, StringComparison.OrdinalIgnoreCase));
                
                if (isRequestedSubscription)
                {
                    selectedSubscription = subscription;
                    _logger.LogInformation($"Using subscription: {subscription.Data.DisplayName} (ID: {subscription.Data.SubscriptionId})");
                }
            }
            
            if (count == 0)
            {
                _logger.LogWarning("No subscriptions found. Make sure you have the appropriate permissions.");
            }
            else
            {
                _logger.LogVerbose("Successfully retrieved subscriptions");
                
                // If a subscription ID was provided but not found, warn the user
                if (subscriptionId != null && selectedSubscription == null)
                {
                    _logger.LogWarning($"Subscription with ID '{subscriptionId}' not found. Using default subscription.");
                }
            }
            
            return _armClient;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to authenticate with Azure", ex);
            throw;
        }
    }
}
