namespace Apollo11.Agc.Dsky;

public interface DskyDrawing
{
    void Clear(int rgb);
    void Fill(DskyRect rect, int rgb);
    void Polygon(ReadOnlySpan<DskyPoint> points, float x, float y, int top, int bottom, float height, float skew);
}

public readonly record struct DskyPoint(float X, float Y);

public readonly record struct DskyRect(int X, int Y, int W, int H)
{
    public bool Contains(int x, int y) => x >= X && x < X + W && y >= Y && y < Y + H;
}
