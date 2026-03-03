namespace UniVpn.Automation.Automation;

/// <summary>
/// Abstracts credential retrieval so that passwords are never stored in source code
/// or configuration files.
/// </summary>
public interface ICredentialProvider
{
    /// <summary>Returns the (username, password) tuple for the VPN login.</summary>
    (string Username, string Password) GetCredentials();

    /// <summary>Returns the current one-time 2FA token, or <c>null</c> if unavailable.</summary>
    string? GetTwoFactorToken();
}
