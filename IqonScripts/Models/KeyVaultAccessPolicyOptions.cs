using System;
using System.Collections.Generic;

namespace IqonScripts.Models;

/// <summary>
/// Access level for Key Vault access policies
/// </summary>
public enum AccessLevel
{
    /// <summary>
    /// Get and List permissions for secrets only
    /// </summary>
    SecretsReadOnly,
    
    /// <summary>
    /// Get, List, Set, Delete permissions for secrets
    /// </summary>
    SecretsReadWrite,
    
    /// <summary>
    /// Get and List permissions for certificates only
    /// </summary>
    CertificatesReadOnly,
    
    /// <summary>
    /// Get and List permissions for keys only
    /// </summary>
    KeysReadOnly,
    
    /// <summary>
    /// All permissions for secrets, keys, and certificates
    /// </summary>
    FullAccess
}

/// <summary>
/// Options for Key Vault access policy operations
/// </summary>
public class KeyVaultAccessPolicyOptions : CommandOptions
{
    /// <summary>
    /// The Entra object ID to add to the access policy
    /// </summary>
    public string ObjectId { get; set; } = "a7351a1e-ad4a-4c4a-a4ca-bea0c51d9b2a";
    
    /// <summary>
    /// The access level to grant
    /// </summary>
    public AccessLevel AccessLevel { get; set; } = AccessLevel.SecretsReadOnly;
}
