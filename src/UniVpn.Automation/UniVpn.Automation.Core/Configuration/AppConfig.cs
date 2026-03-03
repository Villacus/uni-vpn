using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace UniVpn.Automation.Core.Configuration;

/// <summary>
/// Root configuration for the UniVpn automation tool.
/// </summary>
public class AppConfig
{
    /// <summary>Full path to the FortiClient GUI executable.</summary>
    public string FortiClientPath { get; set; } =
        @"C:\Program Files\Fortinet\FortiClient\FortiClient.exe";

    /// <summary>One or more substrings that the FortiClient window title must contain.</summary>
    public string[] WindowTitleSubstrings { get; set; } = ["FortiClient"];

    /// <summary>Name of the VPN profile to connect to (e.g. "EHU").</summary>
    public string ProfileName { get; set; } = "EHU";

    /// <summary>Polling interval in milliseconds between state checks.</summary>
    public int PollingIntervalMs { get; set; } = 500;

    /// <summary>Seconds to wait for the FortiClient window to appear after launch.</summary>
    public int WindowWaitTimeoutSec { get; set; } = 30;

    /// <summary>Seconds to wait for a full connection to be established.</summary>
    public int ConnectionTimeoutSec { get; set; } = 120;

    /// <summary>Maximum number of retries for transient failures.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Credential retrieval mode.
    /// Supported values: "EnvironmentVariables", "WindowsCredentialManager".
    /// Credentials are NEVER stored in this file.
    /// </summary>
    public string CredentialMode { get; set; } = "EnvironmentVariables";

    /// <summary>
    /// Environment variable name for the VPN username.
    /// Used when CredentialMode is "EnvironmentVariables".
    /// </summary>
    public string UsernameEnvVar { get; set; } = "UNI_VPN_USERNAME";

    /// <summary>
    /// Environment variable name for the VPN password.
    /// Used when CredentialMode is "EnvironmentVariables".
    /// </summary>
    public string PasswordEnvVar { get; set; } = "UNI_VPN_PASSWORD";

    /// <summary>
    /// Windows Credential Manager target name.
    /// Used when CredentialMode is "WindowsCredentialManager".
    /// </summary>
    public string CredentialManagerTarget { get; set; } = "UniVpn/FortiClient";

    /// <summary>
    /// When true, enables the send-keys fallback for controls that cannot be
    /// reached via UIA. Keep false unless UIA discovery fails on your system.
    /// </summary>
    public bool UseFallbackSendKeys { get; set; } = false;

    /// <summary>UI element selectors used for state detection and interaction.</summary>
    public UiSelectors Selectors { get; set; } = new();

    /// <summary>Minimum log level. Values: Trace, Debug, Information, Warning, Error.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    // ── Factory ────────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Loads configuration from <paramref name="path"/>.
    /// Returns a default config if the file does not exist.
    /// </summary>
    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
            return new AppConfig();

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
    }
}
