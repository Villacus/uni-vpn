namespace UniVpn.Automation.Core.Configuration;

/// <summary>
/// Configurable UI element selectors for FortiClient state detection and interaction.
/// These names/substrings are matched against UIA automation names and window titles.
/// </summary>
public class UiSelectors
{
    /// <summary>Automation name (or substring) of the "Connect" button.</summary>
    public string ConnectButtonName { get; set; } = "Connect";

    /// <summary>Automation name (or substring) of the "Disconnect" button.</summary>
    public string DisconnectButtonName { get; set; } = "Disconnect";

    /// <summary>Automation name (or substring) of the username / user edit field.</summary>
    public string UsernameFieldName { get; set; } = "Username";

    /// <summary>Automation name (or substring) of the password edit field.</summary>
    public string PasswordFieldName { get; set; } = "Password";

    /// <summary>Automation name (or substring) of the 2FA / token edit field.</summary>
    public string TokenFieldName { get; set; } = "Token";

    /// <summary>Text (or substring) visible in the window when a connection is in progress.</summary>
    public string ConnectingLabelText { get; set; } = "Connecting";

    /// <summary>Text (or substring) visible in the window when fully connected.</summary>
    public string ConnectedLabelText { get; set; } = "Connected";

    /// <summary>Text (or substring) visible in the window when disconnected / ready.</summary>
    public string DisconnectedLabelText { get; set; } = "Connect";

    /// <summary>Text (or substring) in error labels shown by FortiClient.</summary>
    public string ErrorLabelText { get; set; } = "Error";
}
