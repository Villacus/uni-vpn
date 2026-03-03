using Microsoft.Extensions.Logging;
using UniVpn.Automation.Core.Configuration;

namespace UniVpn.Automation.Core.StateMachine;

/// <summary>
/// Drives the FortiClient connection flow by polling the UI state through
/// an <see cref="IWindowDetector"/> and logging all transitions.
/// </summary>
public sealed class VpnStateMachine
{
    private readonly IWindowDetector _detector;
    private readonly AppConfig _config;
    private readonly ILogger<VpnStateMachine> _logger;

    public VpnState CurrentState { get; private set; } = VpnState.NotRunning;

    public VpnStateMachine(
        IWindowDetector detector,
        AppConfig config,
        ILogger<VpnStateMachine> logger)
    {
        _detector = detector ?? throw new ArgumentNullException(nameof(detector));
        _config   = config   ?? throw new ArgumentNullException(nameof(config));
        _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Polls the detector until the window is visible or <paramref name="timeout"/> elapses.
    /// Updates <see cref="CurrentState"/> accordingly.
    /// </summary>
    public bool WaitForWindow(TimeSpan timeout, CancellationToken ct = default)
    {
        _logger.LogInformation("Waiting for FortiClient window (timeout: {Sec}s)…", timeout.TotalSeconds);
        TransitionTo(VpnState.Starting);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (_detector.IsWindowVisible())
            {
                _logger.LogInformation("FortiClient window found.");
                return true;
            }
            Thread.Sleep(_config.PollingIntervalMs);
        }

        TransitionTo(VpnState.Timeout);
        _logger.LogWarning("Timed out waiting for FortiClient window.");
        return false;
    }

    /// <summary>
    /// Polls the detector until the detected state matches <paramref name="targetState"/>
    /// or <paramref name="timeout"/> elapses.
    /// </summary>
    public bool WaitForState(VpnState targetState, TimeSpan timeout, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Waiting for state {Target} (timeout: {Sec}s)…",
            targetState, timeout.TotalSeconds);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var state = _detector.DetectState();
            if (state != CurrentState)
                TransitionTo(state);

            if (CurrentState == targetState)
            {
                _logger.LogInformation("Reached target state {State}.", targetState);
                return true;
            }

            if (CurrentState is VpnState.Error or VpnState.Timeout)
            {
                _logger.LogError("Aborting wait: state is {State}.", CurrentState);
                return false;
            }

            Thread.Sleep(_config.PollingIntervalMs);
        }

        TransitionTo(VpnState.Timeout);
        _logger.LogWarning("Timed out waiting for state {Target}.", targetState);
        return false;
    }

    /// <summary>
    /// Refreshes <see cref="CurrentState"/> by polling the detector once.
    /// </summary>
    public VpnState Refresh()
    {
        var detected = _detector.IsWindowVisible()
            ? _detector.DetectState()
            : VpnState.NotRunning;

        if (detected != CurrentState)
            TransitionTo(detected);

        return CurrentState;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void TransitionTo(VpnState next)
    {
        if (next == CurrentState) return;
        _logger.LogDebug("State {From} → {To}", CurrentState, next);
        CurrentState = next;
    }
}
