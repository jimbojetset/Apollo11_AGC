using System.Buffers;
using System.Runtime.InteropServices;
using Apollo11.Agc.Dsky;

namespace Apollo11.Agc.Web;

internal sealed class DskyCanvas : DskyDrawing
{
    private readonly ArrayBufferWriter<float> _frame = new(8192);

    public byte[] ReadFrame() => MemoryMarshal.AsBytes(_frame.WrittenSpan).ToArray();

    // Commands start with 0 for clear, 1 for rectangle, or a polygon's vertex count.
    public void Clear(int rgb)
    {
        _frame.ResetWrittenCount();
        Write([0, rgb]);
    }

    public void Fill(DskyRect rect, int rgb) => Write([1, rgb, rect.X, rect.Y, rect.W, rect.H]);

    public void Polygon(ReadOnlySpan<DskyPoint> points, float x, float y, int top, int bottom, float height, float skew)
    {
        Write([points.Length, top, bottom, y, height]);
        var vertices = _frame.GetSpan(points.Length * 2);
        for (var i = 0; i < points.Length; i++)
        {
            var point = points[i];
            vertices[i * 2] = x + point.X - point.Y * skew;
            vertices[i * 2 + 1] = y + point.Y;
        }
        _frame.Advance(points.Length * 2);
    }

    private void Write(ReadOnlySpan<float> values)
    {
        values.CopyTo(_frame.GetSpan(values.Length));
        _frame.Advance(values.Length);
    }
}
