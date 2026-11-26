using Point = Apollo11.Agc.Dsky.DskyPoint;
using Rect = Apollo11.Agc.Dsky.DskyRect;

namespace Apollo11.Agc.Dsky;

public sealed partial class DskyFace
{
    private static readonly Point[][] Engravings = Enumerable.Range(0, 'Z' + 1)
        .Select(i => Engraving((char)i)).ToArray();

    private void Legend(string text, Rect bounds, float capHeight, int ink)
    {
        if (text.Length == 0)
            return;

        var scale = capHeight / 12;
        var lines = text.AsSpan();
        var lineBreak = lines.IndexOf('\n');
        var y = bounds.Y + (bounds.H - (lineBreak < 0 ? capHeight : capHeight * 2.35f)) / 2;
        if (lineBreak < 0)
        {
            LegendLine(lines, bounds.X + bounds.W / 2f, y, scale, ink);
            return;
        }

        LegendLine(lines[..lineBreak], bounds.X + bounds.W / 2f, y, scale, ink);
        LegendLine(lines[(lineBreak + 1)..], bounds.X + bounds.W / 2f, y + capHeight * 1.35f, scale, ink);
    }

    private void LegendLine(ReadOnlySpan<char> line, float center, float y, float scale, int ink)
    {
        const float spacing = 3;
        var width = -spacing;
        foreach (var letter in line)
            width += LegendWidth(letter) + spacing;
        var x = center - width * scale / 2;

        foreach (var letter in line)
        {
            var path = Engravings[letter];
            for (var i = 1; i < path.Length; i++)
            {
                if (path[i - 1].X < 0 || path[i].X < 0)
                    continue;

                var a = new Point(path[i - 1].X * scale, path[i - 1].Y * scale);
                var b = new Point(path[i].X * scale, path[i].Y * scale);
                LegendStroke(a, b, x, y, scale * 1.8f, ink);
            }
            x += (LegendWidth(letter) + spacing) * scale;
        }
    }

    private void LegendStroke(Point start, Point end, float x, float y, float width, int ink)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var radius = width / 2;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        var ux = dx / length * radius;
        var uy = dy / length * radius;
        Polygon(
            [new(start.X + uy, start.Y - ux), new(end.X + uy, end.Y - ux),
             new(end.X + ux, end.Y + uy), new(end.X - uy, end.Y + ux),
             new(start.X - uy, start.Y + ux), new(start.X - ux, start.Y - uy)],
            x, y, ink, ink, 1);
    }

    private static int LegendWidth(char letter)
    {
        return letter switch
        {
            'I' or '1' => 0,
            ' ' => 4,
            'M' => 10,
            _ => 8
        };
    }

    // The engraved legends use upright strokes and rounded bowls. The display digits use
    // a separate, slanted electrode mask. A negative X lifts the engraving stroke.
    private static Point[] Engraving(char letter)
    {
        return letter switch
        {
            'A' => [new(0, 12), new(4, 0), new(8, 12), new(-1, 0), new(1.8f, 7), new(6.2f, 7)],
            'B' => [new(0, 6), new(5.5f, 6), new(7.5f, 7), new(8, 9), new(7, 11.5f), new(5, 12),
                new(0, 12), new(0, 0), new(5, 0), new(7, 1), new(7.5f, 3), new(6.5f, 5), new(5.5f, 6)],
            'C' => [new(8, 2), new(6, 0.5f), new(3, 0), new(1, 2), new(0, 5), new(0, 8), new(1.2f, 10.7f),
                new(3, 12), new(6, 11.7f), new(8, 10)],
            'E' => [new(8, 0), new(0, 0), new(0, 12), new(8, 12), new(-1, 0), new(0, 5.8f), new(6.8f, 5.8f)],
            'G' => [new(8, 2), new(6, 0.5f), new(3, 0), new(1, 2), new(0, 5), new(0, 8), new(1.2f, 10.7f),
                new(3, 12), new(6, 11.7f), new(8, 10), new(8, 6.6f), new(4.8f, 6.6f)],
            'I' => [new(0, 0), new(0, 12)],
            'K' => [new(0, 0), new(0, 12), new(-1, 0), new(7.6f, 0), new(0, 7), new(-1, 0), new(3, 4.3f), new(8, 12)],
            'L' => [new(0, 0), new(0, 12), new(8, 12)],
            'M' => [new(0, 12), new(0, 0), new(5, 8), new(10, 0), new(10, 12)],
            'N' => [new(0, 12), new(0, 0), new(8, 12), new(8, 0)],
            'O' or '0' => [new(4, 0), new(6.2f, 0.7f), new(7.5f, 2.5f), new(8, 6), new(7.5f, 9.5f), new(6.2f, 11.3f),
                       new(4, 12), new(1.8f, 11.3f), new(0.5f, 9.5f), new(0, 6), new(0.5f, 2.5f), new(1.8f, 0.7f), new(4, 0)],
            'P' => [new(0, 12), new(0, 0), new(5.5f, 0), new(7.5f, 1.2f), new(8, 3.2f), new(7.5f, 5.4f),
                new(5.5f, 6.4f), new(0, 6.4f)],
            'R' => [new(0, 12), new(0, 0), new(5.5f, 0), new(7.5f, 1.2f), new(8, 3.2f), new(7.5f, 5.4f),
                new(5.5f, 6.4f), new(0, 6.4f), new(-1, 0), new(4.2f, 6.4f), new(8, 12)],
            'S' => [new(8, 1.7f), new(6.2f, 0.4f), new(3, 0), new(1, 1), new(0.4f, 3), new(1.4f, 4.6f),
                new(6.5f, 6.5f), new(7.8f, 8.2f), new(7.4f, 10.5f), new(5.5f, 12), new(2, 11.7f), new(0, 10)],
            'T' => [new(0, 0), new(8, 0), new(-1, 0), new(4, 0), new(4, 12)],
            'U' => [new(0, 0), new(0, 8.4f), new(0.8f, 10.7f), new(3.1f, 12), new(5, 12), new(7.2f, 10.7f), new(8, 8.4f), new(8, 0)],
            'V' => [new(0, 0), new(4, 12), new(8, 0)],
            'Y' => [new(0, 0), new(4, 6.2f), new(8, 0), new(-1, 0), new(4, 6.2f), new(4, 12)],
            '+' => [new(0, 6), new(8, 6), new(-1, 0), new(4, 2), new(4, 10)],
            '-' => [new(0, 6), new(8, 6)],
            '1' => [new(0, 0), new(0, 12)],
            '2' => [new(0.3f, 2), new(1.7f, 0.5f), new(4.7f, 0), new(6.8f, 0.9f), new(7.8f, 2.7f),
                new(7.5f, 4.5f), new(5.6f, 6.3f), new(1.6f, 9), new(0, 12), new(8, 12)],
            '3' => [new(0.3f, 1.7f), new(2.2f, 0.3f), new(5.2f, 0), new(7.5f, 1.5f), new(7.6f, 3.4f),
                new(6.1f, 5.1f), new(3, 5.5f), new(6, 5.7f), new(7.7f, 7), new(8, 9.2f), new(6.4f, 11.4f),
                new(3.5f, 12), new(1.1f, 11.4f), new(0, 10)],
            '4' => [new(5.2f, 0), new(0, 8), new(8, 8), new(-1, 0), new(6, 4.7f), new(6, 12)],
            '5' => [new(7.3f, 0), new(1.3f, 0), new(0.7f, 5.7f), new(2.6f, 4.7f), new(5.4f, 4.9f),
                new(7.4f, 6.4f), new(8, 8.6f), new(7.1f, 10.7f), new(4.7f, 12), new(1.8f, 11.7f), new(0, 10.2f)],
            '6' => [new(7.4f, 1.6f), new(5.4f, 0), new(3, 0.4f), new(1.1f, 2.3f), new(0.2f, 5.2f),
                new(0, 8.1f), new(1, 10.8f), new(3.2f, 12), new(5.5f, 11.8f), new(7.6f, 10.2f),
                new(8, 7.6f), new(6.7f, 5.6f), new(4.2f, 5), new(1.8f, 5.7f), new(0.2f, 7.2f)],
            '7' => [new(0, 0), new(8, 0), new(5.4f, 4), new(3.8f, 8), new(3.1f, 12)],
            '8' => [new(4, 0), new(6.5f, 0.6f), new(7.6f, 2.3f), new(7, 4.2f), new(4, 5.7f), new(1, 4.2f),
                new(0.4f, 2.3f), new(1.5f, 0.6f), new(4, 0), new(-1, 0), new(4, 5.7f), new(7, 7), new(8, 9),
                new(7.2f, 10.8f), new(5, 12), new(3, 12), new(0.8f, 10.8f), new(0, 9), new(1, 7), new(4, 5.7f)],
            '9' => [new(7.8f, 4.8f), new(6.2f, 6.3f), new(3.8f, 7), new(1.3f, 6.4f), new(0, 4.4f),
                new(0.4f, 1.8f), new(2.5f, 0.2f), new(4.8f, 0), new(7, 1.2f), new(8, 3.9f),
                new(7.8f, 6.8f), new(6.9f, 9.7f), new(5, 11.6f), new(2.6f, 12), new(0.6f, 10.4f)],
            _ => []
        };
    }
}
