using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UniVpn.Automation.Core.Configuration;
using UniVpn.Automation.Core.StateMachine;

namespace UniVpn.Automation.Tests;

/// <summary>
/// Unit tests for <see cref="VpnStateMachine"/> using a mocked <see cref="IWindowDetector"/>.
/// </summary>
public class VpnStateMachineTests
{
    private static VpnStateMachine CreateSm(IWindowDetector detector, AppConfig? config = null)
    {
        config ??= new AppConfig { PollingIntervalMs = 10 };
        return new VpnStateMachine(detector, config, NullLogger<VpnStateMachine>.Instance);
    }

    // ── Constructor guards ─────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullDetector_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new VpnStateMachine(null!, new AppConfig(), NullLogger<VpnStateMachine>.Instance));
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        var detector = new Mock<IWindowDetector>().Object;
        Assert.Throws<ArgumentNullException>(() =>
            new VpnStateMachine(detector, null!, NullLogger<VpnStateMachine>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var detector = new Mock<IWindowDetector>().Object;
        Assert.Throws<ArgumentNullException>(() =>
            new VpnStateMachine(detector, new AppConfig(), null!));
    }

    // ── Initial state ──────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsNotRunning()
    {
        var detector = new Mock<IWindowDetector>();
        var sm = CreateSm(detector.Object);
        Assert.Equal(VpnState.NotRunning, sm.CurrentState);
    }

    // ── Refresh ────────────────────────────────────────────────────────────────

    [Fact]
    public void Refresh_WindowNotVisible_ReturnsNotRunning()
    {
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.IsWindowVisible()).Returns(false);

        var sm = CreateSm(detector.Object);
        var state = sm.Refresh();
        Assert.Equal(VpnState.NotRunning, state);
    }

    [Fact]
    public void Refresh_WindowVisible_ReturnsDetectedState()
    {
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.IsWindowVisible()).Returns(true);
        detector.Setup(d => d.DetectState()).Returns(VpnState.Disconnected);

        var sm = CreateSm(detector.Object);
        var state = sm.Refresh();
        Assert.Equal(VpnState.Disconnected, state);
    }

    [Theory]
    [InlineData(VpnState.Connected)]
    [InlineData(VpnState.Connecting)]
    [InlineData(VpnState.CredentialsRequired)]
    [InlineData(VpnState.TwoFactorRequired)]
    [InlineData(VpnState.Error)]
    public void Refresh_ReflectsDetectorOutput(VpnState expected)
    {
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.IsWindowVisible()).Returns(true);
        detector.Setup(d => d.DetectState()).Returns(expected);

        var sm = CreateSm(detector.Object);
        Assert.Equal(expected, sm.Refresh());
    }

    // ── WaitForWindow ──────────────────────────────────────────────────────────

    [Fact]
    public void WaitForWindow_WindowAppearsImmediately_ReturnsTrue()
    {
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.IsWindowVisible()).Returns(true);

        var sm = CreateSm(detector.Object);
        var result = sm.WaitForWindow(TimeSpan.FromSeconds(5));
        Assert.True(result);
    }

    [Fact]
    public void WaitForWindow_WindowNeverAppears_ReturnsFalseAndSetsTimeout()
    {
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.IsWindowVisible()).Returns(false);

        var sm = CreateSm(detector.Object, new AppConfig { PollingIntervalMs = 10 });
        var result = sm.WaitForWindow(TimeSpan.FromMilliseconds(50));
        Assert.False(result);
        Assert.Equal(VpnState.Timeout, sm.CurrentState);
    }

    [Fact]
    public void WaitForWindow_WindowAppearsAfterDelay_ReturnsTrue()
    {
        int callCount = 0;
        var detector = new Mock<IWindowDetector>();
        // Returns false for first 3 calls, then true
        detector.Setup(d => d.IsWindowVisible())
                .Returns(() => ++callCount > 3);

        var sm = CreateSm(detector.Object, new AppConfig { PollingIntervalMs = 10 });
        var result = sm.WaitForWindow(TimeSpan.FromSeconds(5));
        Assert.True(result);
        Assert.True(callCount > 3);
    }

    // ── WaitForState ───────────────────────────────────────────────────────────

    [Fact]
    public void WaitForState_TargetReachedImmediately_ReturnsTrue()
    {
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.DetectState()).Returns(VpnState.Connected);

        var sm = CreateSm(detector.Object);
        var result = sm.WaitForState(VpnState.Connected, TimeSpan.FromSeconds(5));
        Assert.True(result);
        Assert.Equal(VpnState.Connected, sm.CurrentState);
    }

    [Fact]
    public void WaitForState_TargetNeverReached_ReturnsFalseAndSetsTimeout()
    {
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.DetectState()).Returns(VpnState.Disconnected);

        var sm = CreateSm(detector.Object, new AppConfig { PollingIntervalMs = 10 });
        var result = sm.WaitForState(VpnState.Connected, TimeSpan.FromMilliseconds(50));
        Assert.False(result);
        Assert.Equal(VpnState.Timeout, sm.CurrentState);
    }

    [Fact]
    public void WaitForState_ErrorStateAborts_ReturnsFalse()
    {
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.DetectState()).Returns(VpnState.Error);

        var sm = CreateSm(detector.Object);
        var result = sm.WaitForState(VpnState.Connected, TimeSpan.FromSeconds(5));
        Assert.False(result);
        Assert.Equal(VpnState.Error, sm.CurrentState);
    }

    [Fact]
    public void WaitForState_TransitionsThrough_Connecting_To_Connected()
    {
        int call = 0;
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.DetectState()).Returns(() => call++ < 3
            ? VpnState.Connecting
            : VpnState.Connected);

        var sm = CreateSm(detector.Object, new AppConfig { PollingIntervalMs = 10 });
        var result = sm.WaitForState(VpnState.Connected, TimeSpan.FromSeconds(5));
        Assert.True(result);
        Assert.Equal(VpnState.Connected, sm.CurrentState);
    }

    [Fact]
    public void WaitForState_AlreadyAtTargetState_ReturnsTrueWithoutPolling()
    {
        // Arrange: detector always returns Connecting, but state machine is already
        // at Disconnected. WaitForState should return true immediately without ever
        // calling DetectState().
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.DetectState()).Returns(VpnState.Connecting);
        detector.Setup(d => d.IsWindowVisible()).Returns(true);

        var sm = CreateSm(detector.Object, new AppConfig { PollingIntervalMs = 10 });
        // Force current state to Disconnected.
        sm.Refresh();  // IsWindowVisible→true, DetectState→Connecting  → sets Connecting
        // Re-configure so DetectState returns Disconnected, then Refresh again.
        detector.Setup(d => d.DetectState()).Returns(VpnState.Disconnected);
        sm.Refresh();  // → sets Disconnected

        // Act: ask for Disconnected while already at Disconnected.
        // DetectState should NOT be called again (reconfigure it to return a wrong value).
        detector.Setup(d => d.DetectState()).Returns(VpnState.NotRunning);
        var result = sm.WaitForState(VpnState.Disconnected, TimeSpan.FromSeconds(5));

        Assert.True(result);
        Assert.Equal(VpnState.Disconnected, sm.CurrentState);
    }

    [Fact]
    public void WaitForState_WindowDisappears_AbortsAndReturnsFalse()
    {
        // Arrange: detector reports NotRunning immediately (window disappeared).
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.DetectState()).Returns(VpnState.NotRunning);

        var sm = CreateSm(detector.Object, new AppConfig { PollingIntervalMs = 10 });
        // Give the state machine a non-NotRunning starting state to simulate
        // the window being visible before the wait starts.
        detector.Setup(d => d.IsWindowVisible()).Returns(true);
        detector.Setup(d => d.DetectState()).Returns(VpnState.Disconnected);
        sm.Refresh();  // CurrentState = Disconnected

        // Now the window "disappears"
        detector.Setup(d => d.DetectState()).Returns(VpnState.NotRunning);

        var result = sm.WaitForState(VpnState.Connected, TimeSpan.FromSeconds(5));

        Assert.False(result);
        Assert.Equal(VpnState.NotRunning, sm.CurrentState);
    }

    // ── Cancellation ───────────────────────────────────────────────────────────

    [Fact]
    public void WaitForWindow_Cancelled_ReturnsFalse()
    {
        var detector = new Mock<IWindowDetector>();
        detector.Setup(d => d.IsWindowVisible()).Returns(false);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var sm = CreateSm(detector.Object, new AppConfig { PollingIntervalMs = 10 });
        var result = sm.WaitForWindow(TimeSpan.FromSeconds(5), cts.Token);
        Assert.False(result);
    }
}
