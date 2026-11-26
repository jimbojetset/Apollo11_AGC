using Point = Apollo11.Agc.Dsky.DskyPoint;
using Rect = Apollo11.Agc.Dsky.DskyRect;

namespace Apollo11.Agc.Dsky;

public sealed partial class DskyFace(DskyDrawing drawing)
{
    public const int Width = 624;
    public const int Height = 702;
    private const int KeySize = 76;
    private const int KeyPitch = 83;
    private const int KeyTop = 438;
    private const int HalfRow = 41;
    private const int LitSegment = 0xa0efd0;
    private const int LitLegend = 0x87cdb0;
    private const int DarkSegment = 0x454b39;

    private static readonly (DskyKey Key, string Label, int Column, int Y)[] Keys =
    [
        (DskyKey.Verb, "VERB", 0, HalfRow), (DskyKey.Noun, "NOUN", 0, HalfRow + KeyPitch),
        (DskyKey.Plus, "+", 1, 0), (DskyKey.Minus, "-", 1, KeyPitch), (DskyKey.Zero, "0", 1, KeyPitch * 2),
        (DskyKey.Seven, "7", 2, 0), (DskyKey.Four, "4", 2, KeyPitch), (DskyKey.One, "1", 2, KeyPitch * 2),
        (DskyKey.Eight, "8", 3, 0), (DskyKey.Five, "5", 3, KeyPitch), (DskyKey.Two, "2", 3, KeyPitch * 2),
        (DskyKey.Nine, "9", 4, 0), (DskyKey.Six, "6", 4, KeyPitch), (DskyKey.Three, "3", 4, KeyPitch * 2),
        (DskyKey.Clear, "CLR", 5, 0), (DskyKey.Proceed, "PRO", 5, KeyPitch), (DskyKey.KeyRelease, "KEY\nREL", 5, KeyPitch * 2),
        (DskyKey.Enter, "ENTR", 6, HalfRow), (DskyKey.Reset, "RSET", 6, HalfRow + KeyPitch)
    ];

    private static readonly (string Label, DskyLamps Lamp)[] LeftLamps =
    [
        ("UPLINK\nACTY", DskyLamps.UplinkActivity), ("NO ATT", DskyLamps.NoAttitude),
        ("STBY", DskyLamps.Standby), ("KEY REL", DskyLamps.KeyRelease),
        ("OPR ERR", DskyLamps.OperatorError), ("", DskyLamps.None), ("", DskyLamps.None)
    ];

    private static readonly (string Label, DskyLamps Lamp)[] RightLamps =
    [
        ("TEMP", DskyLamps.Temperature), ("GIMBAL\nLOCK", DskyLamps.GimbalLock),
        ("PROG", DskyLamps.Program), ("RESTART", DskyLamps.Restart),
        ("TRACKER", DskyLamps.Tracker), ("ALT", DskyLamps.Altitude), ("VEL", DskyLamps.Velocity)
    ];

    public static DskyKey? KeyAt(int x, int y)
    {
        foreach (var key in Keys)
            if (KeyBounds(key.Column, key.Y).Contains(x, y))
                return key.Key;
        return null;
    }

    public static IEnumerable<(DskyKey Key, string Label, DskyRect Bounds)> KeyPositions =>
        Keys.Select(key => (key.Key, key.Label, KeyBounds(key.Column, key.Y)));

    public void Draw(DskyPanel panel, IEnumerable<DskyKey> pressedKeys, DskyKey? mouseKey)
    {
        drawing.Clear(0x242625);
        Chamfer(new Rect(5, 8, 614, 690), 11, 0x151818);
        Chamfer(new Rect(11, 5, 602, 687), 12, 0x626665);
        Chamfer(new Rect(13, 7, 598, 680), 10, 0x979b98, 0x747976);

        var annunciators = new Rect(50, 30, 234, 391);
        var display = new Rect(340, 30, 234, 391);
        MountWindow(annunciators, 0x888c80, 0x656a60);
        MountWindow(display, 0x5c5d47, 0x363f32);

        Screw(24, 19);
        Screw(312, 17);
        Screw(600, 19);
        Screw(24, 427);
        Screw(312, 427);
        Screw(600, 427);
        Screw(24, 678);
        Screw(600, 678);

        var lamps = panel.Lamps;
        for (var row = 0; row < LeftLamps.Length; row++)
        {
            var y = 43 + row * 52;
            Lamp(63, y, LeftLamps[row].Label, (lamps & LeftLamps[row].Lamp) != 0, false);
            Lamp(169, y, RightLamps[row].Label, (lamps & RightLamps[row].Lamp) != 0, true);
        }

        var activity = (lamps & DskyLamps.ComputerActivity) != 0;
        var activityWindow = new Rect(361, 64, 65, 39);
        Fill(activityWindow, activity ? LitLegend : 0x555744);
        Legend("COMP\nACTY", activityWindow, 10, activity ? 0x1e3828 : 0x282e24);
        DisplayPair(panel, "PROG", 0, 504, 48);
        DisplayPair(panel, "VERB", 2, 394, 130);
        DisplayPair(panel, "NOUN", 4, 504, 130);

        ReadOnlySpan<int> centerDots = [49, 117, 185];
        foreach (var y in centerDots)
            Disc(456, y, 2, 0xc6c5a8);

        for (var row = 0; row < 3; row++)
        {
            var y = 217 + row * 65;
            Fill(new Rect(370, y - 7, 173, 4), panel.DisplayPowered ? LitLegend : 0x5b6659);
            Disc(361, y - 5, 2, 0xc6c5a8);
            Disc(552, y - 5, 2, 0xc6c5a8);
            DrawSign(panel.Sign(row), 372, y + 26);
            for (var digit = 0; digit < 5; digit++)
                Digit(panel.Digit(6 + row * 5 + digit), 397 + digit * 30, y + 6);
        }

        foreach (var key in Keys)
        {
            var bounds = KeyBounds(key.Column, key.Y);
            var pressed = key.Key == mouseKey || pressedKeys.Contains(key.Key);
            Chamfer(Outset(bounds, 1), 5, 0x181b19);
            var cap = Inset(bounds, pressed ? 3 : 1);
            Chamfer(cap, 4, pressed ? 0x2c2f2b : 0x5a5c55, 0x171a18);
            Chamfer(Inset(cap, 2), 3, pressed ? 0x272a26 : 0x353833, 0x252824);
            Legend(key.Label, bounds with { Y = bounds.Y + (pressed ? 1 : -1) },
                key.Label.Length == 1 ? 22 : 11, pressed ? 0xe7ead7 : 0xd4d6c3);
        }
    }

    private static Rect KeyBounds(int column, int y)
    {
        return new(25 + column * KeyPitch, KeyTop + y, KeySize, KeySize);
    }

    private void MountWindow(Rect bounds, int top, int bottom)
    {
        Chamfer(bounds, 7, 0x363b38);
        Chamfer(Inset(bounds, 2), 5, 0xb7b9ae, 0x65695f);
        Chamfer(Inset(bounds, 6), 4, 0x252a26);
        Chamfer(Inset(bounds, 8), 3, top, bottom);
    }

    private void Screw(int x, int y)
    {
        Disc(x, y, 8, 0x4b504c);
        Disc(x - 1, y - 1, 6.5f, 0xb3b4aa);
        Disc(x, y, 5.5f, 0x767a72);
        Disc(x, y, 3, 0x292d27, 6);
    }

    private void Lamp(int x, int y, string label, bool lit, bool caution)
    {
        var bounds = new Rect(x, y, 101, 48);
        Fill(bounds, 0x73786c);
        var top = lit ? caution ? 0xffe08a : 0xf7f3dc : 0xc7c8b9;
        var bottom = lit ? caution ? 0xeab955 : 0xe1ddc7 : 0xadafa0;
        Chamfer(Inset(bounds, 2), 1, top, bottom);
        Legend(label, bounds, 11, lit ? 0x2b2d24 : 0x565a4f);
    }

    private void DisplayPair(DskyPanel panel, string label, int position, int x, int y)
    {
        // The captions are dark lettering over their own illuminated areas.
        var legend = new Rect(x - 27, y, 54, 17);
        Fill(legend, panel.DisplayPowered ? LitLegend : 0x555744);
        Legend(label, legend, 10, 0x24312a);
        Digit(panel.Digit(position), x - 25, y + 21);
        Digit(panel.Digit(position + 1), x + 3, y + 21);
    }

    private void Digit(char digit, int x, int y)
    {
        ReadOnlySpan<byte> segments = [0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f];
        var mask = digit is >= '0' and <= '9' ? segments[digit - '0'] : 0;
        ReadOnlySpan<(int X, int Y, bool Across)> electrodes =
        [
            (4, 0, true), (20, 2, false), (20, 23, false), (4, 42, true),
            (0, 23, false), (0, 2, false), (4, 21, true)
        ];
        for (var i = 0; i < electrodes.Length; i++)
        {
            var (column, row, across) = electrodes[i];
            var ink = (mask & (1 << i)) != 0 ? LitSegment : DarkSegment;
            // The Sylvania mask skews the whole seven-segment character to the right.
            if (across)
                Polygon([new(0, 2), new(2, 0), new(14, 0), new(16, 2), new(14, 4), new(2, 4)],
                    x + column + (44 - row) * 0.17f, y + row, ink, ink, 4, 0.17f);
            else
                Polygon([new(2, 0), new(4, 2), new(4, 18), new(2, 20), new(0, 18), new(0, 2)],
                    x + column + (44 - row) * 0.17f, y + row, ink, ink, 20, 0.17f);
        }
    }

    private void DrawSign(char sign, int x, int y)
    {
        Fill(new Rect(x - 9, y - 2, 18, 4), sign is '+' or '-' ? LitSegment : DarkSegment);
        var vertical = sign == '+' ? LitSegment : DarkSegment;
        Fill(new Rect(x - 2, y - 9, 4, 7), vertical);
        Fill(new Rect(x - 2, y + 2, 4, 7), vertical);
    }

    private static Rect Inset(Rect rect, int inset)
    {
        return new(rect.X + inset, rect.Y + inset, rect.W - 2 * inset, rect.H - 2 * inset);
    }

    private static Rect Outset(Rect rect, int outset)
    {
        return Inset(rect, -outset);
    }

    private void Chamfer(Rect rect, int corner, int rgb)
    {
        Chamfer(rect, corner, rgb, rgb);
    }

    private void Chamfer(Rect rect, int corner, int top, int bottom)
    {
        Polygon(
        [new(corner, 0), new(rect.W - corner, 0), new(rect.W, corner), new(rect.W, rect.H - corner),
         new(rect.W - corner, rect.H), new(corner, rect.H), new(0, rect.H - corner), new(0, corner)],
        rect.X, rect.Y, top, bottom, rect.H);
    }

    private void Disc(float x, float y, float radius, int color, int sides = 16)
    {
        Span<Point> points = stackalloc Point[sides];
        for (var i = 0; i < sides; i++)
        {
            var angle = i * MathF.Tau / sides;
            points[i] = new Point(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
        }
        Polygon(points, x, y, color, color, 1);
    }

    private void Polygon(ReadOnlySpan<Point> points, float x, float y, int top, int bottom,
        float height, float skew = 0) => drawing.Polygon(points, x, y, top, bottom, height, skew);

    private void Fill(Rect rect, int rgb) => drawing.Fill(rect, rgb);
}
