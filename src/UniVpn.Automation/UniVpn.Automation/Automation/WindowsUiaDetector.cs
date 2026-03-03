using System.Windows.Automation;
using Microsoft.Extensions.Logging;
using UniVpn.Automation.Core.Configuration;
using UniVpn.Automation.Core.StateMachine;

namespace UniVpn.Automation.Automation;

/// <summary>
/// Windows UI Automation (UIA3) implementation of <see cref="IWindowDetector"/>.
/// Finds the FortiClient top-level window and inspects its child elements to
/// determine the current VPN state.
/// </summary>
public sealed class WindowsUiaDetector : IWindowDetector
{
    private readonly AppConfig _config;
    private readonly ILogger<WindowsUiaDetector> _logger;

    public WindowsUiaDetector(AppConfig config, ILogger<WindowsUiaDetector> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ── IWindowDetector ────────────────────────────────────────────────────────

    public bool IsWindowVisible() => FindWindow() is not null;

    public VpnState DetectState()
    {
        var window = FindWindow();
        if (window is null)
            return VpnState.NotRunning;

        try
        {
            return InspectWindow(window);
        }
        catch (ElementNotAvailableException ex)
        {
            _logger.LogDebug(ex, "Element not available during state detection.");
            return VpnState.NotRunning;
        }
    }

    public void BringToForeground()
    {
        var window = FindWindow();
        if (window is null)
        {
            _logger.LogWarning("Cannot bring FortiClient to foreground: window not found.");
            return;
        }

        try
        {
            if (window.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern)
                && pattern is WindowPattern wp)
            {
                wp.SetWindowVisualState(WindowVisualState.Normal);
            }

            // SetFocus raises the window and focuses it.
            window.SetFocus();
            _logger.LogDebug("Brought FortiClient window to foreground.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ElementNotAvailableException)
        {
            _logger.LogWarning(ex, "Failed to bring FortiClient window to foreground.");
        }
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    private AutomationElement? FindWindow()
    {
        foreach (var titleSubstring in _config.WindowTitleSubstrings)
        {
            // UIA PropertyCondition supports exact match only in the managed wrapper.
            // Walk top-level children and do a substring check in code.
            var children = AutomationElement.RootElement
                .FindAll(TreeScope.Children, Condition.TrueCondition);

            foreach (AutomationElement child in children)
            {
                try
                {
                    if (child.Current.Name.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase))
                        return child;
                }
                catch (ElementNotAvailableException) { /* window closed mid-search */ }
            }
        }
        return null;
    }

    private VpnState InspectWindow(AutomationElement window)
    {
        var s = _config.Selectors;

        // Priority: error > connected > connecting > 2FA > credentials > disconnected
        if (HasText(window, s.ErrorLabelText))
        {
            _logger.LogDebug("Detected state: Error (found error label).");
            return VpnState.Error;
        }

        if (HasText(window, s.ConnectedLabelText)
            && HasEnabledButton(window, s.DisconnectButtonName))
        {
            _logger.LogDebug("Detected state: Connected.");
            return VpnState.Connected;
        }

        if (HasText(window, s.ConnectingLabelText))
        {
            _logger.LogDebug("Detected state: Connecting.");
            return VpnState.Connecting;
        }

        if (HasEnabledEditField(window, s.TokenFieldName))
        {
            _logger.LogDebug("Detected state: TwoFactorRequired.");
            return VpnState.TwoFactorRequired;
        }

        if (HasEnabledEditField(window, s.UsernameFieldName)
            || HasEnabledEditField(window, s.PasswordFieldName))
        {
            _logger.LogDebug("Detected state: CredentialsRequired.");
            return VpnState.CredentialsRequired;
        }

        _logger.LogDebug("Detected state: Disconnected (default).");
        return VpnState.Disconnected;
    }

    /// <summary>Returns true when any descendant element's name contains <paramref name="text"/>.</summary>
    private static bool HasText(AutomationElement root, string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        foreach (AutomationElement el in all)
        {
            try
            {
                if (el.Current.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch (ElementNotAvailableException) { }
        }
        return false;
    }

    /// <summary>Returns true when an enabled Button whose name contains <paramref name="name"/> exists.</summary>
    private static bool HasEnabledButton(AutomationElement root, string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        var cond = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.IsEnabledProperty, true));

        var buttons = root.FindAll(TreeScope.Descendants, cond);
        foreach (AutomationElement btn in buttons)
        {
            try
            {
                if (btn.Current.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch (ElementNotAvailableException) { }
        }
        return false;
    }

    /// <summary>Returns true when an enabled Edit control whose name contains <paramref name="name"/> exists.</summary>
    private static bool HasEnabledEditField(AutomationElement root, string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        var cond = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
            new PropertyCondition(AutomationElement.IsEnabledProperty, true));

        var edits = root.FindAll(TreeScope.Descendants, cond);
        foreach (AutomationElement edit in edits)
        {
            try
            {
                if (edit.Current.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch (ElementNotAvailableException) { }
        }
        return false;
    }
}

