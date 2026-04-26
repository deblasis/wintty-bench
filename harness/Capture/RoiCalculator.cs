namespace WinttyBench.Capture;

// Pure-logic mapper from (iteration index, cell grid, pixel-per-cell) to
// the ROI rect that iteration N will paint into. The latency runner uses
// this to ensure each iteration writes to a never-touched cell so the
// baseline frame for iter N is always blank, regardless of what iter N-1
// painted.
//
// Wrap order is left-to-right, top-to-bottom, matching the echo script's
// own row/col arithmetic.
public static class RoiCalculator
{
    // Pure: derives per-cell pixel size from client area + grid dimensions.
    // Caller passes session.ClientWidthPx (which came from GetClientRect at
    // session construction); this function does no Win32 calls itself.
    public static (int cellPxW, int cellPxH) MeasureCellPixSize(
        int clientWidthPx, int clientHeightPx, int cols, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(clientWidthPx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(clientHeightPx);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cols);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        return (clientWidthPx / cols, clientHeightPx / rows);
    }

    // Pure: maps iteration index to the ROI rect that iter will paint into.
    public static Roi For(int iter, int cellPxW, int cellPxH, int cols, int rows)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(iter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellPxW);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellPxH);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cols);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        if (iter >= cols * rows)
            throw new ArgumentOutOfRangeException(nameof(iter),
                $"iter {iter} exceeds grid capacity {cols * rows}");

        var col = iter % cols;
        var row = iter / cols;
        return new Roi(col * cellPxW, row * cellPxH, cellPxW, cellPxH);
    }
}
