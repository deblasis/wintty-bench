namespace WinttyBench.Capture;

// Pure-logic luminance diff over a rectangular region of two BGRA byte
// buffers. Returns true if any pixel inside the ROI exceeds the luminance
// threshold; ignores the alpha channel; uses BT.709 weights.
//
// Default threshold of 30 on the 0-255 scale is loose enough to absorb
// ClearType subpixel-positioning shifts (which can move a glyph edge by
// 1-2 px sub-cell) while still firing reliably when an empty cell takes
// any kind of paint.
public static class RoiDiffer
{
    private const byte DefaultLuminanceThreshold = 30;

    public static bool IsChanged(
        ReadOnlySpan<byte> baselineBgra,
        ReadOnlySpan<byte> currentBgra,
        Roi roi,
        int frameWidthPx,
        byte luminanceThreshold = DefaultLuminanceThreshold)
    {
        if (roi.W <= 0 || roi.H <= 0)
            throw new ArgumentException("ROI must have positive width and height", nameof(roi));
        if (baselineBgra.Length != currentBgra.Length)
            throw new ArgumentException("Baseline and current buffers must have equal length");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameWidthPx);

        var rowStrideBytes = frameWidthPx * 4;

        for (var dy = 0; dy < roi.H; dy++)
        {
            var rowOffset = (roi.Y + dy) * rowStrideBytes + roi.X * 4;
            for (var dx = 0; dx < roi.W; dx++)
            {
                var p = rowOffset + dx * 4;
                var lumA = Luminance(baselineBgra[p + 2], baselineBgra[p + 1], baselineBgra[p]);
                var lumB = Luminance(currentBgra[p + 2], currentBgra[p + 1], currentBgra[p]);
                var delta = lumA > lumB ? lumA - lumB : lumB - lumA;
                if (delta > luminanceThreshold)
                    return true;
            }
        }
        return false;
    }

    // BT.709: Y = 0.2126 R + 0.7152 G + 0.0722 B. Integer math: scale by 1024.
    // 218*R + 732*G + 74*B sums to 1024.
    private static int Luminance(byte r, byte g, byte b)
        => (218 * r + 732 * g + 74 * b) >> 10;
}
