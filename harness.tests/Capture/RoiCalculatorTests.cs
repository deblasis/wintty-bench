using WinttyBench.Capture;
using Xunit;

namespace WinttyBench.Tests.Capture;

public class RoiCalculatorTests
{
    [Fact]
    public void MeasureCellPixSize_960x640_120x32_Returns_8x20()
    {
        var (w, h) = RoiCalculator.MeasureCellPixSize(960, 640, 120, 32);
        Assert.Equal(8, w);
        Assert.Equal(20, h);
    }

    [Fact]
    public void MeasureCellPixSize_RoundsTowardZero()
    {
        // 961 / 120 = 8 remainder 1; integer divide yields 8 (the cell
        // pixel size never grows past the true cell, so the ROI sits inside).
        var (w, h) = RoiCalculator.MeasureCellPixSize(961, 641, 120, 32);
        Assert.Equal(8, w);
        Assert.Equal(20, h);
    }

    [Fact]
    public void For_Iter0_Returns_TopLeftCell()
    {
        var roi = RoiCalculator.For(iter: 0, cellPxW: 8, cellPxH: 20, cols: 120, rows: 32);
        Assert.Equal(new Roi(0, 0, 8, 20), roi);
    }

    [Fact]
    public void For_Iter1_AdvancesOneColumn()
    {
        var roi = RoiCalculator.For(iter: 1, cellPxW: 8, cellPxH: 20, cols: 120, rows: 32);
        Assert.Equal(new Roi(8, 0, 8, 20), roi);
    }

    [Fact]
    public void For_LastColumnOfRow1()
    {
        var roi = RoiCalculator.For(iter: 119, cellPxW: 8, cellPxH: 20, cols: 120, rows: 32);
        Assert.Equal(new Roi(119 * 8, 0, 8, 20), roi);
    }

    [Fact]
    public void For_WrapsToRow2()
    {
        var roi = RoiCalculator.For(iter: 120, cellPxW: 8, cellPxH: 20, cols: 120, rows: 32);
        Assert.Equal(new Roi(0, 20, 8, 20), roi);
    }

    [Fact]
    public void For_NegativeIter_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RoiCalculator.For(iter: -1, cellPxW: 8, cellPxH: 20, cols: 120, rows: 32));
    }

    [Fact]
    public void For_IterBeyondGrid_Throws()
    {
        // 120 cols * 32 rows = 3840 cells. Iter 3840 is one past the grid.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RoiCalculator.For(iter: 120 * 32, cellPxW: 8, cellPxH: 20, cols: 120, rows: 32));
    }
}
