using System.Diagnostics;
using Xunit;

namespace WinttyBench.Tests.Scripts;

public class LatencyEchoScriptTests
{
    private static string ScriptPath => Path.Combine(
        AppContext.BaseDirectory, "scripts", "latency-echo.ps1");

    [Fact]
    public void Script_Is_Bundled_With_Test_Output()
    {
        Assert.True(File.Exists(ScriptPath),
            $"Expected echo script at {ScriptPath}; check CopyToOutputDirectory.");
    }

    [Fact]
    [Trait("OS", "Windows")]
    public void Script_FirstByte_PaintsAt_Row1_Col1()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "pwsh-7 deterministic on Windows only");
        var output = RunScriptWithBytes(new byte[] { 0x20 }); // single space
        Assert.Contains("\x1b[1;1H*", output);
    }

    [Fact]
    [Trait("OS", "Windows")]
    public void Script_121stByte_WrapsTo_Row2_Col1()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "pwsh-7 deterministic on Windows only");
        var bytes = new byte[121];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = 0x20;
        var output = RunScriptWithBytes(bytes);
        Assert.Contains("\x1b[2;1H*", output);
    }

    private static string RunScriptWithBytes(byte[] stdinBytes)
    {
        var psi = new ProcessStartInfo("pwsh.exe",
            $"-NoLogo -NoProfile -File \"{ScriptPath}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        p.StandardInput.BaseStream.Write(stdinBytes, 0, stdinBytes.Length);
        p.StandardInput.BaseStream.Flush();
        p.StandardInput.Close(); // EOF triggers script exit
        p.WaitForExit(5000);
        return p.StandardOutput.ReadToEnd();
    }
}
