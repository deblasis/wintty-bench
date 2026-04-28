using WinttyBench.Launchers;
using Xunit;

namespace WinttyBench.Tests.Launchers;

public class LauncherFactoryTests
{
    [Fact]
    public void For_Wintty_ReturnsWinttyLauncher()
    {
        var launcher = LauncherFactory.For("wintty");
        Assert.IsType<WinttyLauncher>(launcher);
        Assert.Equal("wintty", launcher.Name);
    }

    [Fact]
    public void For_Wt_ReturnsWtLauncher()
    {
        var launcher = LauncherFactory.For("wt");
        Assert.IsType<WtLauncher>(launcher);
        Assert.Equal("windows-terminal", launcher.Name);
    }

    [Fact]
    public void For_Unknown_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => LauncherFactory.For("nethack"));
    }
}
