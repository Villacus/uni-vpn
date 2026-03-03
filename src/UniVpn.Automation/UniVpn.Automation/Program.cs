using System.IO;
using Microsoft.Extensions.Logging;
using UniVpn.Automation.Automation;
using UniVpn.Automation.Core.Configuration;
using UniVpn.Automation.Core.StateMachine;

namespace UniVpn.Automation;

/// <summary>
/// CLI entry point for UniVpn.Automation.
///
/// Usage:
///   UniVpn.Automation.exe [--dry-run] [--config path/to/config.json]
///
/// Options:
///   --dry-run        Detect and print the current FortiClient state; do NOT
///                    send any keystrokes or start a connection.
///   --config &lt;path&gt;  Path to the JSON configuration file.
///                    Defaults to "config.json" next to the executable.
/// </summary>
internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        // ── Parse arguments ──────────────────────────────────────────────────
        bool dryRun    = args.Contains("--dry-run",   StringComparer.OrdinalIgnoreCase);
        string? configArg = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--config", StringComparison.OrdinalIgnoreCase))
            {
                configArg = args[i + 1];
                break;
            }
        }

        string configPath = configArg
            ?? Path.Combine(AppContext.BaseDirectory, "config.json");

        // ── Load configuration ───────────────────────────────────────────────
        AppConfig config;
        try
        {
            config = AppConfig.Load(configPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to load config from '{configPath}': {ex.Message}");
            return 1;
        }

        // ── Set up logging ───────────────────────────────────────────────────
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(config.LogLevel)
                .AddSimpleConsole(opts =>
                {
                    opts.SingleLine = true;
                    opts.TimestampFormat = "HH:mm:ss ";
                });
        });

        var programLogger = loggerFactory.CreateLogger("UniVpn.Automation");
        programLogger.LogInformation("UniVpn.Automation starting. Config: {Path}", configPath);
        programLogger.LogInformation("Mode: {Mode}", dryRun ? "DRY-RUN" : "CONNECT");

        // ── Build object graph ───────────────────────────────────────────────
        var detector = new WindowsUiaDetector(
            config, loggerFactory.CreateLogger<WindowsUiaDetector>());

        var stateMachine = new VpnStateMachine(
            detector, config, loggerFactory.CreateLogger<VpnStateMachine>());

        var credentials = new CredentialProvider(
            config, loggerFactory.CreateLogger<CredentialProvider>());

        var automation = new FortiClientAutomation(
            config, stateMachine, detector, credentials,
            loggerFactory.CreateLogger<FortiClientAutomation>());

        // ── Bring window to foreground if it exists ──────────────────────────
        if (detector.IsWindowVisible())
            detector.BringToForeground();

        // ── Execute ──────────────────────────────────────────────────────────
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(config.ConnectionTimeoutSec + config.WindowWaitTimeoutSec + 60));

        try
        {
            if (dryRun)
            {
                var state = automation.DryRun();
                programLogger.LogInformation("Dry-run complete. Detected state: {State}", state);
                return 0;
            }

            bool connected = await automation.ConnectAsync(cts.Token);
            return connected ? 0 : 2;
        }
        catch (OperationCanceledException)
        {
            programLogger.LogError("Operation cancelled (global timeout).");
            return 3;
        }
        catch (Exception ex)
        {
            programLogger.LogError(ex, "Unhandled exception.");
            return 4;
        }
    }
}
