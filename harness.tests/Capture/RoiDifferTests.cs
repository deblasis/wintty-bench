using WinttyBench.Capture;
using Xunit;

namespace WinttyBench.Tests.Capture;

public class RoiDifferTests
{
    // Build a 4-byte BGRA pixel from the same gray value in B, G, R; alpha 255.
    private static byte[] SolidGray(int width, int height, byte gray)
    {
        var buf = new byte[width * height * 4];
        for (var i = 0; i < buf.Length; i += 4)
        {
            buf[i + 0] = gray;     // B
            buf[i + 1] = gray;     // G
            buf[i + 2] = gray;     // R
            buf[i + 3] = 255;      // A
        }
        return buf;
    }

    private static void SetPixelGray(byte[] bgra, int width, int x, int y, byte gray)
    {
        var idx = (y * width + x) * 4;
        bgra[idx + 0] = gray;
        bgra[idx + 1] = gray;
        bgra[idx + 2] = gray;
    }

    [Fact]
    public void AllEqual_ReturnsFalse()
    {
        var a = SolidGray(8, 8, 100);
        var b = SolidGray(8, 8, 100);
        Assert.False(RoiDiffer.IsChanged(a, b, new Roi(0, 0, 8, 8), 8));
    }

    [Fact]
    public void SinglePixel_LumDelta29_InsideRoi_ReturnsFalse()
    {
        var a = SolidGray(8, 8, 100);
        var b = SolidGray(8, 8, 100);
        SetPixelGray(b, 8, x: 3, y: 3, gray: 129); // delta 29
        Assert.False(RoiDiffer.IsChanged(a, b, new Roi(0, 0, 8, 8), 8));
    }

    [Fact]
    public void SinglePixel_LumDelta31_InsideRoi_ReturnsTrue()
    {
        var a = SolidGray(8, 8, 100);
        var b = SolidGray(8, 8, 100);
        SetPixelGray(b, 8, x: 3, y: 3, gray: 131); // delta 31
        Assert.True(RoiDiffer.IsChanged(a, b, new Roi(0, 0, 8, 8), 8));
    }

    [Fact]
    public void Diff_OutsideRoi_ReturnsFalse()
    {
        var a = SolidGray(8, 8, 100);
        var b = SolidGray(8, 8, 100);
        SetPixelGray(b, 8, x: 6, y: 6, gray: 200); // far from ROI
        Assert.False(RoiDiffer.IsChanged(a, b, new Roi(0, 0, 4, 4), 8));
    }

    [Fact]
    public void RoiSubrect_OfLargerFrame_DiffInside_ReturnsTrue()
    {
        var a = SolidGray(16, 16, 50);
        var b = SolidGray(16, 16, 50);
        SetPixelGray(b, 16, x: 9, y: 5, gray: 200);
        Assert.True(RoiDiffer.IsChanged(a, b, new Roi(8, 4, 4, 4), 16));
    }

    [Fact]
    public void AlphaOnlyDiff_ReturnsFalse()
    {
        var a = SolidGray(8, 8, 100);
        var b = SolidGray(8, 8, 100);
        b[(3 * 8 + 3) * 4 + 3] = 0; // mutate alpha only
        Assert.False(RoiDiffer.IsChanged(a, b, new Roi(0, 0, 8, 8), 8));
    }

    [Fact]
    public void EmptyRoi_Throws()
    {
        var a = SolidGray(8, 8, 100);
        var b = SolidGray(8, 8, 100);
        Assert.Throws<ArgumentException>(
            () => RoiDiffer.IsChanged(a, b, new Roi(0, 0, 0, 0), 8));
    }

    [Fact]
    public void MismatchedBufferSizes_Throws()
    {
        var a = SolidGray(8, 8, 100);
        var b = SolidGray(8, 4, 100);
        Assert.Throws<ArgumentException>(
            () => RoiDiffer.IsChanged(a, b, new Roi(0, 0, 4, 4), 8));
    }
}
