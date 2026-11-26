using static Apollo11.Agc.Dsky.Sdl;

namespace Apollo11.Agc.Dsky;

internal sealed class SdlDskyDrawing(nint renderer) : DskyDrawing
{
    public void Clear(int rgb)
    {
        Color(rgb);
        _ = SDL_RenderClear(renderer);
    }

    public void Fill(DskyRect rect, int rgb)
    {
        Color(rgb);
        _ = SDL_RenderFillRect(renderer, new Rect(rect.X, rect.Y, rect.W, rect.H));
    }

    public void Polygon(ReadOnlySpan<DskyPoint> points, float x, float y, int top, int bottom, float height, float skew)
    {
        Span<Vertex> vertices = stackalloc Vertex[points.Length];
        Span<int> indices = stackalloc int[(points.Length - 2) * 3];
        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            var blend = Math.Clamp(point.Y / height, 0, 1);
            vertices[i] = new Vertex(new Point(x + point.X - point.Y * skew, y + point.Y),
                Shade(top >> 16, bottom >> 16, blend), Shade(top >> 8, bottom >> 8, blend),
                Shade(top, bottom, blend), 255, default);
        }
        for (var i = 0; i < points.Length - 2; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = i + 1;
            indices[i * 3 + 2] = i + 2;
        }
        _ = SDL_RenderGeometry(renderer, 0, in vertices[0], vertices.Length, in indices[0], indices.Length);
    }

    private static byte Shade(int from, int to, float blend) =>
        (byte)((from & 255) * (1 - blend) + (to & 255) * blend + 0.5f);

    private void Color(int rgb) =>
        _ = SDL_SetRenderDrawColor(renderer, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
}
