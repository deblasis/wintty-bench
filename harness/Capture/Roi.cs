namespace WinttyBench.Capture;

// Pixel-space rectangle inside a captured frame. X/Y are top-left corner,
// W/H are width/height in pixels. Used by the ROI diff and the iteration
// position calculator.
public readonly record struct Roi(int X, int Y, int W, int H);
