using System.Text.Json;
using UniVpn.Automation.Core.Configuration;

namespace UniVpn.Automation.Tests;

/// <summary>
/// Unit tests for <see cref="AppConfig"/> loading and default values.
/// </summary>
public class AppConfigTests
{
    // ── Default values ─────────────────────────────────────────────────────────

    [Fact]
    public void DefaultConfig_HasExpectedFortiClientPath()
    {
        var config = new AppConfig();
        Assert.Equal(
            @"C:\Program Files\Fortinet\FortiClient\FortiClientConsole.exe",
            config.FortiClientPath);
    }

    [Fact]
    public void DefaultConfig_HasEhuProfileName()
    {
        var config = new AppConfig();
        Assert.Equal("EHU", config.ProfileName);
    }

    [Fact]
    public void DefaultConfig_HasPositivePollingInterval()
    {
        var config = new AppConfig();
        Assert.True(config.PollingIntervalMs > 0);
    }

    [Fact]
    public void DefaultConfig_HasPositiveTimeouts()
    {
        var config = new AppConfig();
        Assert.True(config.WindowWaitTimeoutSec > 0);
        Assert.True(config.ConnectionTimeoutSec > 0);
    }

    [Fact]
    public void DefaultConfig_SelectorsHaveExpectedDefaults()
    {
        var s = new AppConfig().Selectors;
        Assert.NotEmpty(s.ConnectButtonName);
        Assert.NotEmpty(s.DisconnectButtonName);
        Assert.NotEmpty(s.UsernameFieldName);
        Assert.NotEmpty(s.PasswordFieldName);
        Assert.NotEmpty(s.TokenFieldName);
    }

    // ── Load from file ─────────────────────────────────────────────────────────

    [Fact]
    public void Load_MissingFile_ReturnsDefaultConfig()
    {
        var config = AppConfig.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));
        Assert.NotNull(config);
        Assert.Equal("EHU", config.ProfileName);
    }

    [Fact]
    public void Load_ValidJson_OverridesDefaults()
    {
        var json = """
            {
              "ProfileName": "MYORG",
              "PollingIntervalMs": 1000,
              "WindowWaitTimeoutSec": 60,
              "MaxRetries": 5,
              "UseFallbackSendKeys": true
            }
            """;

        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            var config = AppConfig.Load(path);
            Assert.Equal("MYORG", config.ProfileName);
            Assert.Equal(1000, config.PollingIntervalMs);
            Assert.Equal(60, config.WindowWaitTimeoutSec);
            Assert.Equal(5, config.MaxRetries);
            Assert.True(config.UseFallbackSendKeys);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_CaseInsensitiveKeys_Parses()
    {
        var json = """
            {
              "profilename": "LOWER",
              "pollingintervalms": 250
            }
            """;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            var config = AppConfig.Load(path);
            Assert.Equal("LOWER", config.ProfileName);
            Assert.Equal(250, config.PollingIntervalMs);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_JsonWithComments_Parses()
    {
        var json = """
            {
              // This is a comment
              "ProfileName": "COMMENTED",
              "MaxRetries": 2  // trailing comma allowed too
            }
            """;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            var config = AppConfig.Load(path);
            Assert.Equal("COMMENTED", config.ProfileName);
            Assert.Equal(2, config.MaxRetries);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_NestedSelectors_Parses()
    {
        var json = """
            {
              "Selectors": {
                "ConnectButtonName": "Go",
                "TokenFieldName": "OTP"
              }
            }
            """;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            var config = AppConfig.Load(path);
            Assert.Equal("Go", config.Selectors.ConnectButtonName);
            Assert.Equal("OTP", config.Selectors.TokenFieldName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_WindowTitleSubstrings_ParsesArray()
    {
        var json = """
            {
              "WindowTitleSubstrings": ["FortiClient", "VPN Client"]
            }
            """;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            var config = AppConfig.Load(path);
            Assert.Equal(2, config.WindowTitleSubstrings.Length);
            Assert.Contains("FortiClient", config.WindowTitleSubstrings);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
