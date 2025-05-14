using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using IqonScripts.Models;
using IqonScripts.Services;
using IqonScripts.Utils;
using Microsoft.Extensions.Logging;

namespace IqonScripts.Scripts;

/// <summary>
/// Script for updating Azure Key Vault access policies
/// </summary>
public class KeyVaultAccessPolicyScript
{
    private readonly KeyVaultAccessPolicyOptions _options;
    private readonly ILogger _logger;
    private readonly LoggerService _loggerService;
    private readonly AzureAuthenticationService _authService;
    private readonly HttpClient _httpClient;
    
    // Regular expression for extracting tenant IDs from key vault names
    private readonly Regex _keyVaultTenantIdRegex = new Regex(@"kv-iqonsticos(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Regular expression for filtering resource groups
    private readonly Regex _resourceGroupNameRegex = new Regex(@"^rg-iqon-sticos(-\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    // Azure tenant ID from AzureAuthenticationService
    private const string AzureTenantId = "c5772ebb-4c35-4874-abb7-1eb6cbdc90d9";
    
    // Azure API version for Key Vault
    private const string ApiVersion = "2022-07-01";

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyVaultAccessPolicyScript"/> class
    /// </summary>
    /// <param name="options">The command options</param>
    /// <param name="logger">The logger</param>
    public KeyVaultAccessPolicyScript(KeyVaultAccessPolicyOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _loggerService = new LoggerService(logger, options.Verbose);
        _authService = new AzureAuthenticationService(_loggerService);
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Runs the key vault access policy script
    /// </summary>
    /// <returns>The script result</returns>
    public async Task<ScriptResult> RunAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _loggerService.LogInformation("Starting Key Vault Access Policy Update script");
            _loggerService.LogInformation($"Object ID to add: {_options.ObjectId}");
            _loggerService.LogInformation($"Access Level: {_options.AccessLevel}");
            _loggerService.LogInformation($"Dry run: {_options.DryRun}");
            
            // If tenant ID is specified, log it
            if (!string.IsNullOrEmpty(_options.TenantId))
            {
                _loggerService.LogInformation($"Filtering by Tenant ID: {_options.TenantId}");
            }
            
            // Authenticate with Azure
            var armClient = await _authService.GetArmClientAsync(_options.SubscriptionId);
            
            // Discover key vaults matching the pattern
            var keyVaults = await DiscoverKeyVaultsAsync(armClient, _options.TenantId);
            
            if (keyVaults.Count == 0)
            {
                _loggerService.LogWarning("No key vaults found matching the pattern 'kv-iqonsticos{tenantId}'");
                if (!string.IsNullOrEmpty(_options.TenantId))
                {
                    _loggerService.LogWarning($"No key vaults found with tenant ID: {_options.TenantId}");
                }
                
                return new ScriptResult
                {
                    Success = true,
                    DryRun = _options.DryRun,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds
                };
            }
            
            _loggerService.LogInformation($"Found {keyVaults.Count} key vaults to update");
            
            // Display key vaults to update
            foreach (var keyVault in keyVaults)
            {
                _loggerService.LogInformation($"  {keyVault.Name} in {keyVault.ResourceGroup}");
            }
            
            // Update access policies
            var result = await UpdateKeyVaultAccessPoliciesAsync(armClient, keyVaults, _options.ObjectId, _options.AccessLevel, _options.DryRun);
            
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
            
            _loggerService.LogError("An error occurred while running the Key Vault Access Policy Update script", ex);
            
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
    /// Discovers key vaults matching the specified pattern
    /// </summary>
    /// <param name="armClient">The ARM client</param>
    /// <param name="tenantIdFilter">Optional tenant ID filter</param>
    /// <returns>A list of key vaults</returns>
    private async Task<List<KeyVaultInfo>> DiscoverKeyVaultsAsync(ArmClient armClient, string tenantIdFilter = null)
    {
        var keyVaults = new List<KeyVaultInfo>();
        _loggerService.LogInformation("Discovering key vaults...");
        
        // Get the subscription
        var subscription = armClient.GetDefaultSubscription();
        _loggerService.LogInformation($"Using subscription: {subscription.Data.DisplayName} (ID: {subscription.Id.SubscriptionId})");
        
        // Get all resource groups
        _loggerService.LogInformation("Searching all resource groups for key vaults...");
        
        await foreach (var resourceGroup in subscription.GetResourceGroups().GetAllAsync())
        {
            // Skip resource groups that don't match our naming pattern
            if (!_resourceGroupNameRegex.IsMatch(resourceGroup.Data.Name))
            {
                _loggerService.LogVerbose($"Skipping resource group {resourceGroup.Data.Name} as it doesn't match the naming pattern");
                continue;
            }
            
            _loggerService.LogVerbose($"Checking resource group: {resourceGroup.Data.Name}");
            
            try
            {
                await foreach (var keyVault in resourceGroup.GetKeyVaults().GetAllAsync())
                {
                    _loggerService.LogVerbose($"Found key vault: {keyVault.Data.Name}");
                    
                    var match = _keyVaultTenantIdRegex.Match(keyVault.Data.Name);
                    if (match.Success)
                    {
                        var extractedTenantId = match.Groups[1].Value;
                        
                        // Skip if a specific tenant ID is provided and this resource doesn't match
                        if (!string.IsNullOrEmpty(tenantIdFilter) && extractedTenantId != tenantIdFilter)
                        {
                            _loggerService.LogVerbose($"Skipping key vault {keyVault.Data.Name} as it doesn't match the specified tenant ID: {tenantIdFilter}");
                            continue;
                        }
                        
                        keyVaults.Add(new KeyVaultInfo
                        {
                            Id = keyVault.Id,
                            Name = keyVault.Data.Name,
                            ResourceGroup = resourceGroup.Data.Name,
                            SubscriptionId = subscription.Id.SubscriptionId,
                            TenantId = extractedTenantId,
                            AzureTenantId = keyVault.Data.Properties.TenantId.ToString()
                        });
                        
                        _loggerService.LogInformation($"Found key vault: {keyVault.Data.Name} with tenant ID: {extractedTenantId}");
                    }
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogWarning($"Error accessing key vaults in resource group {resourceGroup.Data.Name}: {ex.Message}");
            }
        }
        
        return keyVaults;
    }
    
    /// <summary>
    /// Updates access policies on the specified key vaults using REST API
    /// </summary>
    /// <param name="armClient">The ARM client</param>
    /// <param name="keyVaults">The key vaults to update</param>
    /// <param name="objectId">The object ID to add</param>
    /// <param name="accessLevel">The access level to grant</param>
    /// <param name="isDryRun">Whether this is a dry run</param>
    /// <returns>The result of the update operation</returns>
    private async Task<ScriptResult> UpdateKeyVaultAccessPoliciesAsync(
        ArmClient armClient,
        List<KeyVaultInfo> keyVaults,
        string objectId,
        AccessLevel accessLevel,
        bool isDryRun)
    {
        var result = new ScriptResult
        {
            DryRun = isDryRun,
            Success = true,
            ProcessedResources = keyVaults.Select(kv => new ResourceInfo
            {
                Id = kv.Id,
                Name = kv.Name,
                Type = "KeyVault",
                TenantId = kv.TenantId,
                SourceResourceGroup = kv.ResourceGroup
            }).ToList()
        };

        // Counters for summary statistics
        int policiesAlreadyExist = 0;
        int policiesNeeded = 0;
        int policiesAdded = 0;
        
        _loggerService.LogInformation($"Updating access policies on {keyVaults.Count} key vaults...");
        
        // Get permissions based on access level
        var permissions = GetPermissionsForAccessLevel(accessLevel);
        string permissionsDescription = GetPermissionsDescription(accessLevel);
        
        // Setup token credential for REST calls with the specific tenant ID
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            // Use the same tenant ID as in AzureAuthenticationService
            TenantId = AzureTenantId
        });
        var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
        var accessToken = await credential.GetTokenAsync(tokenRequestContext);
        
        foreach (var keyVault in keyVaults)
        {
            try
            {
                _loggerService.LogInformation($"Processing key vault: {keyVault.Name}");
                
                if (isDryRun)
                {
                    _loggerService.LogInformation($"[DRY RUN] Would add access policy for object ID {objectId} to {keyVault.Name} with permissions: {permissionsDescription}");
                    continue;
                }
                
                _loggerService.LogVerbose($"First checking if access policy already exists");
                
                // Get the current key vault configuration to check existing policies
                string getVaultUrl = $"https://management.azure.com/subscriptions/{keyVault.SubscriptionId}/resourceGroups/{keyVault.ResourceGroup}/providers/Microsoft.KeyVault/vaults/{keyVault.Name}?api-version={ApiVersion}";
                
                // Create HTTP request to get key vault
                var getRequest = new HttpRequestMessage(HttpMethod.Get, getVaultUrl);
                getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
                
                // Send the request
                _loggerService.LogVerbose($"Sending request to {getVaultUrl}");
                var getResponse = await _httpClient.SendAsync(getRequest);
                
                if (!getResponse.IsSuccessStatusCode)
                {
                    string errorContent = await getResponse.Content.ReadAsStringAsync();
                    _loggerService.LogError($"Failed to get key vault {keyVault.Name}: {getResponse.StatusCode} - {errorContent}");
                    result.Errors.Add($"Failed to get key vault {keyVault.Name}: {getResponse.StatusCode} - {errorContent}");
                    result.Success = false;
                    continue;
                }
                
                // Parse the response to get the current access policies
                string vaultContent = await getResponse.Content.ReadAsStringAsync();
                var vaultJson = JsonDocument.Parse(vaultContent);
                var accessPoliciesElement = vaultJson.RootElement
                    .GetProperty("properties")
                    .GetProperty("accessPolicies");
                
                // Check if the Object ID already exists in any access policy
                bool policyExists = false;
                foreach (var policy in accessPoliciesElement.EnumerateArray())
                {
                    if (policy.TryGetProperty("objectId", out var policyObjectId) && 
                        policyObjectId.GetString().Equals(objectId, StringComparison.OrdinalIgnoreCase))
                    {
                        policyExists = true;
                        break;
                    }
                }
                
                if (policyExists)
                {
                    _loggerService.LogInformation($"Access policy for Object ID {objectId} already exists in key vault {keyVault.Name} - skipping");
                    policiesAlreadyExist++;
                    continue;
                }
                
                policiesNeeded++;
                
                _loggerService.LogVerbose($"Access policy doesn't exist yet - adding new policy");
                
                // Create access policy request
                string apiUrl = $"https://management.azure.com/subscriptions/{keyVault.SubscriptionId}/resourceGroups/{keyVault.ResourceGroup}/providers/Microsoft.KeyVault/vaults/{keyVault.Name}/accessPolicies/add?api-version={ApiVersion}";
                
                // Build the JSON request body
                var requestBody = new
                {
                    properties = new
                    {
                        accessPolicies = new[]
                        {
                            new
                            {
                                tenantId = keyVault.AzureTenantId,
                                objectId,
                                permissions = new
                                {
                                    keys = permissions.KeyPermissions,
                                    secrets = permissions.SecretPermissions,
                                    certificates = permissions.CertificatePermissions,
                                    storage = permissions.StoragePermissions
                                }
                            }
                        }
                    }
                };
                
                // Log the request details for debugging
                _loggerService.LogVerbose($"Using tenant ID: {keyVault.AzureTenantId}");
                _loggerService.LogVerbose($"Using object ID: {objectId}");
                
                // Serialize the request body to JSON
                string requestBodyJson = JsonSerializer.Serialize(requestBody);
                _loggerService.LogVerbose($"Request body: {requestBodyJson}");
                
                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Put, apiUrl)
                {
                    Content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json")
                };
                
                // Add authorization header
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
                
                // Send the request
                _loggerService.LogVerbose($"Sending request to {apiUrl}");
                var response = await _httpClient.SendAsync(request);
                
                // Check response
                if (response.IsSuccessStatusCode)
                {
                    _loggerService.LogSuccess($"Successfully added access policy to key vault {keyVault.Name}");
                    policiesAdded++;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _loggerService.LogError($"Failed to add access policy to key vault {keyVault.Name}: {response.StatusCode} - {errorContent}");
                    result.Errors.Add($"Failed to add access policy to key vault {keyVault.Name}: {response.StatusCode} - {errorContent}");
                    result.Success = false;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError($"Failed to update access policy for key vault {keyVault.Name}: {ex.Message}");
                result.Errors.Add($"Failed to update access policy for key vault {keyVault.Name}: {ex.Message}");
                result.Success = false;
            }
        }
        
        // Add the statistics to the result
        var stats = $"{policiesAdded} of {policiesNeeded} access policies added successfully. " +
                   $"{policiesAlreadyExist} key vaults already had the access policy.";
        
        if (isDryRun)
        {
            stats = $"Would add access policy to {policiesNeeded} key vaults. " +
                   $"{policiesAlreadyExist} key vaults already have the access policy.";
        }
        
        _loggerService.LogInformation(stats);
        
        return result;
    }
    
    /// <summary>
    /// Data structure to hold permission settings
    /// </summary>
    private class PermissionSet
    {
        public List<string> SecretPermissions { get; } = new List<string>();
        public List<string> KeyPermissions { get; } = new List<string>();
        public List<string> CertificatePermissions { get; } = new List<string>();
        public List<string> StoragePermissions { get; } = new List<string>();
    }
    
    /// <summary>
    /// Gets permissions for the selected access level
    /// </summary>
    /// <param name="accessLevel">The access level</param>
    /// <returns>The permissions</returns>
    private PermissionSet GetPermissionsForAccessLevel(AccessLevel accessLevel)
    {
        var permissions = new PermissionSet();
        
        switch (accessLevel)
        {
            case AccessLevel.SecretsReadOnly:
                permissions.SecretPermissions.AddRange(new[] { "get", "list" });
                break;
                
            case AccessLevel.SecretsReadWrite:
                permissions.SecretPermissions.AddRange(new[] { "get", "list", "set", "delete" });
                break;
                
            case AccessLevel.CertificatesReadOnly:
                permissions.CertificatePermissions.AddRange(new[] { "get", "list" });
                break;
                
            case AccessLevel.KeysReadOnly:
                permissions.KeyPermissions.AddRange(new[] { "get", "list" });
                break;
                
            case AccessLevel.FullAccess:
                // Add all secret permissions
                permissions.SecretPermissions.AddRange(new[] { 
                    "get", "list", "set", "delete", "backup", "restore", "recover", "purge" 
                });
                
                // Add all key permissions
                permissions.KeyPermissions.AddRange(new[] { 
                    "get", "list", "create", "import", "delete", "backup", "restore", "recover", "purge"
                });
                
                // Add all certificate permissions
                permissions.CertificatePermissions.AddRange(new[] { 
                    "get", "list", "create", "import", "delete", "backup", "restore", "recover", "purge"
                });
                break;
        }
        
        return permissions;
    }
    
    /// <summary>
    /// Gets a human-readable description of the permissions for the access level
    /// </summary>
    /// <param name="accessLevel">The access level</param>
    /// <returns>A description of the permissions</returns>
    private string GetPermissionsDescription(AccessLevel accessLevel)
    {
        switch (accessLevel)
        {
            case AccessLevel.SecretsReadOnly:
                return "Secrets: get, list";
                
            case AccessLevel.SecretsReadWrite:
                return "Secrets: get, list, set, delete";
                
            case AccessLevel.CertificatesReadOnly:
                return "Certificates: get, list";
                
            case AccessLevel.KeysReadOnly:
                return "Keys: get, list";
                
            case AccessLevel.FullAccess:
                return "Secrets: get, list, set, delete, backup, restore, recover, purge; " +
                       "Keys: get, list, create, import, delete, backup, restore, recover, purge; " +
                       "Certificates: get, list, create, import, delete, backup, restore, recover, purge";
                
            default:
                return "Secrets: get, list"; // Default to secrets read-only
        }
    }
    
    /// <summary>
    /// Key vault information for simplified processing
    /// </summary>
    private class KeyVaultInfo
    {
        /// <summary>
        /// Gets or sets the ID of the key vault
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Gets or sets the name of the key vault
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the resource group of the key vault
        /// </summary>
        public string ResourceGroup { get; set; }
        
        /// <summary>
        /// Gets or sets the subscription ID of the key vault
        /// </summary>
        public string SubscriptionId { get; set; }
        
        /// <summary>
        /// Gets or sets the tenant ID extracted from the key vault name (numerical ID in the name)
        /// </summary>
        public string TenantId { get; set; }
        
        /// <summary>
        /// Gets or sets the Azure tenant ID (guid) of the key vault from Azure
        /// </summary>
        public string AzureTenantId { get; set; }
    }
}
