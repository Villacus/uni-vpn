using UniVpn.Automation.Core.Configuration;

namespace UniVpn.Automation.Core.StateMachine;

/// <summary>
/// Describes the detected UI state of a FortiClient window.
/// Implementations are provided per platform (Windows UIA, mock, etc.).
/// </summary>
public interface IWindowDetector
{
    /// <summary>
    /// Returns <c>true</c> when at least one window whose title contains one
    /// of the configured <see cref="AppConfig.WindowTitleSubstrings"/> is visible.
    /// </summary>
    bool IsWindowVisible();

    /// <summary>
    /// Inspects the current UI and returns the best-matching <see cref="VpnState"/>.
    /// </summary>
    VpnState DetectState();

    /// <summary>
    /// Brings the FortiClient window to the foreground.
    /// </summary>
    void BringToForeground();
}
