namespace UniVpn.Automation.Core.StateMachine;

/// <summary>
/// Represents the detected connection state of FortiClient.
/// </summary>
public enum VpnState
{
    /// <summary>FortiClient process is not running or its window cannot be found.</summary>
    NotRunning,

    /// <summary>FortiClient was launched and we are waiting for its window to appear.</summary>
    Starting,

    /// <summary>FortiClient is open and in a disconnected / idle state.</summary>
    Disconnected,

    /// <summary>The credential input fields (username + password) are visible and active.</summary>
    CredentialsRequired,

    /// <summary>The 2FA / token input field is visible and active.</summary>
    TwoFactorRequired,

    /// <summary>A VPN connection attempt is in progress.</summary>
    Connecting,

    /// <summary>The VPN is fully connected.</summary>
    Connected,

    /// <summary>FortiClient is displaying an error message.</summary>
    Error,

    /// <summary>A wait operation exceeded its configured timeout.</summary>
    Timeout
}
