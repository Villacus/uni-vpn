using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Microsoft.Extensions.Logging;
using UniVpn.Automation.Core.Configuration;
using UniVpn.Automation.Core.StateMachine;

namespace UniVpn.Automation.Automation;

/// <summary>
/// Orchestrates the end-to-end FortiClient VPN connection flow:
/// launch → wait for window → enter credentials → enter 2FA → confirm connected.
/// </summary>
public sealed class FortiClientAutomation
{
    private readonly AppConfig _config;
    private readonly VpnStateMachine _stateMachine;
    private readonly IWindowDetector _detector;
    private readonly ICredentialProvider _credentials;
    private readonly ILogger<FortiClientAutomation> _logger;

    public FortiClientAutomation(
        AppConfig config,
        VpnStateMachine stateMachine,
        IWindowDetector detector,
        ICredentialProvider credentials,
        ILogger<FortiClientAutomation> logger)
    {
        _config       = config       ?? throw new ArgumentNullException(nameof(config));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _detector     = detector     ?? throw new ArgumentNullException(nameof(detector));
        _credentials  = credentials  ?? throw new ArgumentNullException(nameof(credentials));
        _logger       = logger       ?? throw new ArgumentNullException(nameof(logger));
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Detects and logs the current FortiClient state without sending any keystrokes.
    /// </summary>
    public VpnState DryRun()
    {
        _logger.LogInformation("[DRY-RUN] Detecting FortiClient state…");
        var state = _stateMachine.Refresh();
        _logger.LogInformation("[DRY-RUN] Detected state: {State}", state);
        return state;
    }

    /// <summary>
    /// Full connection flow with retries.
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= _config.MaxRetries; attempt++)
        {
            _logger.LogInformation("Connection attempt {Attempt}/{Max}…", attempt, _config.MaxRetries);
            try
            {
                if (await TryConnectOnceAsync(ct))
                    return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Connection attempt cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during attempt {Attempt}.", attempt);
            }

            if (attempt < _config.MaxRetries)
            {
                _logger.LogInformation("Retrying in 2 seconds…");
                await Task.Delay(2000, ct);
            }
        }

        _logger.LogError("All {Max} connection attempts failed.", _config.MaxRetries);
        return false;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task<bool> TryConnectOnceAsync(CancellationToken ct)
    {
        // 1. Ensure FortiClient is running.
        EnsureFortiClientRunning();

        // 2. Wait for window.
        var windowTimeout = TimeSpan.FromSeconds(_config.WindowWaitTimeoutSec);
        if (!_stateMachine.WaitForWindow(windowTimeout, ct))
        {
            _logger.LogError("FortiClient window did not appear within {Sec}s.", _config.WindowWaitTimeoutSec);
            return false;
        }

        _stateMachine.Refresh();
        _logger.LogInformation("FortiClient window visible. Current state: {State}", _stateMachine.CurrentState);

        if (_stateMachine.CurrentState == VpnState.Connected)
        {
            _logger.LogInformation("Already connected. Nothing to do.");
            return true;
        }

        // 3. Bring window to foreground.
        BringToForeground();
        await Task.Delay(300, ct); // brief pause to let the window settle

        // 4. Wait for the app to reach a state where we can act.
        var stateTimeout = TimeSpan.FromSeconds(_config.WindowWaitTimeoutSec);
        _stateMachine.Refresh();
        bool ready = _stateMachine.CurrentState is VpnState.Disconnected or VpnState.CredentialsRequired;
        if (!ready)
        {
            ready = _stateMachine.WaitForState(VpnState.Disconnected, stateTimeout, ct);
            // WaitForState transitions CurrentState to Timeout on expiry, so we re-detect
            // the actual UI state to check whether we ended up at CredentialsRequired.
            if (!ready && _stateMachine.Refresh() == VpnState.CredentialsRequired)
                ready = true;
        }

        if (!ready)
        {
            _logger.LogError("FortiClient did not reach a ready state. Current: {State}", _stateMachine.CurrentState);
            return false;
        }

        // 5. If disconnected, click Connect.
        if (_stateMachine.CurrentState == VpnState.Disconnected)
        {
            _logger.LogInformation("Clicking Connect button…");
            ClickConnectButton();
            await Task.Delay(500, ct);
            _stateMachine.Refresh();
        }

        // 6. Fill credentials if required.
        if (_stateMachine.CurrentState == VpnState.CredentialsRequired
            || _stateMachine.WaitForState(VpnState.CredentialsRequired, TimeSpan.FromSeconds(10), ct))
        {
            _logger.LogInformation("Entering credentials…");
            EnterCredentials();
            await Task.Delay(500, ct);
            _stateMachine.Refresh();
        }

        // 7. Fill 2FA token if required.
        if (_stateMachine.CurrentState == VpnState.TwoFactorRequired
            || _stateMachine.WaitForState(VpnState.TwoFactorRequired, TimeSpan.FromSeconds(30), ct))
        {
            _logger.LogInformation("Entering 2FA token…");
            EnterTwoFactorToken();
        }

        // 8. Wait for connection to complete.
        var connTimeout = TimeSpan.FromSeconds(_config.ConnectionTimeoutSec);
        if (!_stateMachine.WaitForState(VpnState.Connected, connTimeout, ct))
        {
            _logger.LogError("Connection did not complete within {Sec}s. State: {State}",
                _config.ConnectionTimeoutSec, _stateMachine.CurrentState);
            return false;
        }

        _logger.LogInformation("VPN connected successfully.");
        return true;
    }

    private void EnsureFortiClientRunning()
    {
        if (_stateMachine.Refresh() != VpnState.NotRunning)
            return;

        _logger.LogInformation("Launching FortiClient: {Path}", _config.FortiClientPath);
        var psi = new ProcessStartInfo(_config.FortiClientPath) { UseShellExecute = true };
        Process.Start(psi);
    }

    private void BringToForeground()
    {
        _detector.BringToForeground();
    }

    private void ClickConnectButton()
    {
        var window = FindFortiClientWindow();
        if (window is null) return;

        var button = FindDescendantByName(window, _config.Selectors.ConnectButtonName, ControlType.Button);
        if (button is null)
        {
            if (_config.UseFallbackSendKeys)
            {
                _logger.LogWarning("Connect button not found via UIA; using send-keys fallback.");
                SendKeys(window, "{ENTER}");
            }
            else
            {
                _logger.LogError("Connect button not found and UseFallbackSendKeys is false.");
            }
            return;
        }

        if (button.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern)
            && pattern is InvokePattern ip)
        {
            ip.Invoke();
        }
    }

    private void EnterCredentials()
    {
        var window = FindFortiClientWindow();
        if (window is null) return;

        var (username, password) = _credentials.GetCredentials();

        SetEditFieldValue(window, _config.Selectors.UsernameFieldName, username);
        SetEditFieldValue(window, _config.Selectors.PasswordFieldName, password);

        // Submit with Enter on the password field (common FortiClient behaviour).
        var pwdField = FindDescendantByName(window, _config.Selectors.PasswordFieldName, ControlType.Edit);
        if (pwdField is not null)
            SendReturnKey(pwdField);
        else if (_config.UseFallbackSendKeys)
        {
            _logger.LogWarning("Password field not found via UIA; pressing Enter via send-keys.");
            SendKeys(window, "{ENTER}");
        }
    }

    private void EnterTwoFactorToken()
    {
        // Prompt the credential provider for the token (it reads from env var UNI_VPN_2FA_TOKEN
        // or via Windows Credential Manager based on configuration).
        var token = _credentials.GetTwoFactorToken();
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("No 2FA token available. Set UNI_VPN_2FA_TOKEN or configure a token provider.");
            return;
        }

        var window = FindFortiClientWindow();
        if (window is null) return;

        SetEditFieldValue(window, _config.Selectors.TokenFieldName, token);

        var tokenField = FindDescendantByName(window, _config.Selectors.TokenFieldName, ControlType.Edit);
        if (tokenField is not null)
            SendReturnKey(tokenField);
        else if (_config.UseFallbackSendKeys)
        {
            _logger.LogWarning("Token field not found via UIA; pressing Enter via send-keys.");
            SendKeys(window, "{ENTER}");
        }
    }

    // ── UIA utilities ──────────────────────────────────────────────────────────

    private AutomationElement? FindFortiClientWindow()
    {
        foreach (var title in _config.WindowTitleSubstrings)
        {
            var children = AutomationElement.RootElement
                .FindAll(TreeScope.Children, Condition.TrueCondition);
            foreach (AutomationElement child in children)
            {
                try
                {
                    if (child.Current.Name.Contains(title, StringComparison.OrdinalIgnoreCase))
                        return child;
                }
                catch (ElementNotAvailableException) { }
            }
        }
        return null;
    }

    private static AutomationElement? FindDescendantByName(
        AutomationElement root, string name, ControlType controlType)
    {
        var cond = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
        var elements = root.FindAll(TreeScope.Descendants, cond);
        foreach (AutomationElement el in elements)
        {
            try
            {
                if (el.Current.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return el;
            }
            catch (ElementNotAvailableException) { }
        }
        return null;
    }

    private void SetEditFieldValue(AutomationElement root, string fieldName, string value)
    {
        var field = FindDescendantByName(root, fieldName, ControlType.Edit);
        if (field is null)
        {
            _logger.LogWarning("Edit field '{Name}' not found.", fieldName);
            return;
        }

        if (field.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)
            && pattern is ValuePattern vp)
        {
            vp.SetValue(value);
            return;
        }

        if (_config.UseFallbackSendKeys)
        {
            _logger.LogWarning("ValuePattern not available for '{Name}'; using send-keys fallback.", fieldName);
            field.SetFocus();
            // Select all existing text then type the new value.
            System.Windows.Forms.SendKeys.SendWait("^a");
            System.Windows.Forms.SendKeys.SendWait(EscapeForSendKeys(value));
        }
        else
        {
            _logger.LogError(
                "Cannot set value for '{Name}': ValuePattern unavailable and UseFallbackSendKeys is false.",
                fieldName);
        }
    }

    private static void SendReturnKey(AutomationElement element)
    {
        element.SetFocus();
        System.Windows.Forms.SendKeys.SendWait("{ENTER}");
    }

    private static void SendKeys(AutomationElement window, string keys)
    {
        window.SetFocus();
        System.Windows.Forms.SendKeys.SendWait(keys);
    }

    /// <summary>Escapes special characters for <see cref="System.Windows.Forms.SendKeys"/>.</summary>
    private static string EscapeForSendKeys(string input)
    {
        // SendKeys special chars: + ^ % ~ ( ) [ ] { }
        return System.Text.RegularExpressions.Regex.Replace(input, @"([+^%~()[\]{}])", "{$1}");
    }
}
