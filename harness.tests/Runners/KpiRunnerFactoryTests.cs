using WinttyBench.Cells;
using WinttyBench.Runners;
using Xunit;

namespace WinttyBench.Tests.Runners;

public class KpiRunnerFactoryTests
{
    [Fact]
    public void For_Throughput_Cell_Returns_ThroughputRunner()
    {
        var cell = new Cell(
            Id: "C1",
            Shell: "pwsh-7.4",
            Workload: "vtebench_dense_cells",
            Kpi: "throughput_bytes_per_sec",
            FixturePath: "fixtures/vtebench/dense_cells.txt",
            FixtureKey: null,
            WinttyConfigOverrides: new Dictionary<string, string>());
        var runner = KpiRunnerFactory.For(cell);
        Assert.IsType<ThroughputRunner>(runner);
    }

    [Fact]
    public void For_Unknown_Kpi_Throws()
    {
        var cell = new Cell(
            Id: "X",
            Shell: "pwsh-7.4",
            Workload: "w",
            Kpi: "bogus",
            FixturePath: "fixtures/vtebench/dense_cells.txt",
            FixtureKey: null,
            WinttyConfigOverrides: new Dictionary<string, string>());
        Assert.Throws<NotSupportedException>(() => KpiRunnerFactory.For(cell));
    }
}
