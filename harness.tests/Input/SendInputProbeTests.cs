using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using WinttyBench.Input;
using Xunit;

namespace WinttyBench.Tests.Input;

[SupportedOSPlatform("windows")]
public class SendInputProbeTests
{
    [Fact]
    [Trait("OS", "Windows")]
    public void Inject_Returns_NonZero_Qpc()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "SendInput is Windows-only");
        var qpc = SendInputProbe.Inject(SendInputInterop.VK_SPACE);
        Assert.True(qpc > 0);
    }

    [Fact]
    [Trait("OS", "Windows")]
    public void Inject_Two_Calls_Have_MonotonicallyIncreasing_Qpc()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "SendInput is Windows-only");
        var a = SendInputProbe.Inject(SendInputInterop.VK_SPACE);
        var b = SendInputProbe.Inject(SendInputInterop.VK_SPACE);
        Assert.True(b > a);
    }

    [Fact]
    [Trait("OS", "Windows")]
    public void EnsureForeground_Of_Self_Does_Not_Throw()
    {
        // Smoke test: calling EnsureForeground on whatever is currently
        // foreground should be a no-op. Skip on headless agents where
        // GetForegroundWindow returns NULL.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Win32 input is Windows-only");
        var fg = SendInputInterop.GetForegroundWindow();
        if (fg == nint.Zero) Assert.Skip("headless agent: no foreground window");
        SendInputProbe.EnsureForeground(fg);
    }
}
