using Apollo11.Agc.Hardware;

namespace Apollo11.Agc.Dsky;

public enum DskyKey
{
    Proceed = 0,
    One = 1, Two, Three, Four, Five, Six, Seven, Eight, Nine,
    Zero = 16, Verb, Reset,
    KeyRelease = 25, Plus, Minus, Enter,
    Clear = 30, Noun
}

[Flags]
public enum DskyLamps
{
    None = 0,
    UplinkActivity = 1 << 0, NoAttitude = 1 << 1, Standby = 1 << 2,
    KeyRelease = 1 << 3, OperatorError = 1 << 4, Temperature = 1 << 5,
    GimbalLock = 1 << 6, Program = 1 << 7, Restart = 1 << 8,
    Tracker = 1 << 9, Altitude = 1 << 10, Velocity = 1 << 11,
    ComputerActivity = 1 << 12
}

public sealed class DskyPanel(AgcCpu cpu)
{
    private const int LampChannel = 0x09; // 11 octal

    internal bool DisplayPowered => !cpu.Standby;

    // Each relay row carries two five-bit character codes, with two exceptions:
    // row 8 only supplies R1's first digit, and row 3 straddles R2 and R3.
    private static readonly (int Row, bool Left)[] DigitRelays =
    [
        (11, true), (11, false), (10, true), (10, false), (9, true), (9, false),
        (8, false), (7, true), (7, false), (6, true), (6, false),
        (5, true), (5, false), (4, true), (4, false), (3, true),
        (3, false), (2, true), (2, false), (1, true), (1, false)
    ];

    // The scaler divides the 1.28-second flash period into four 320 ms phases.
    private bool FlashVisible => cpu.CycleCount * 3 / 81920 % 4 != 0;

    public DskyLamps Lamps
    {
        get
        {
            var lampWord = cpu.Channels.Get(LampChannel);
            var relayWord = cpu.Channels.GetLatchedRelayRow(12);
            var lampTest = (cpu.Channels.Get(0x0b) & 0x200) != 0;
            var lamps = DskyLamps.None;
            if ((lampWord & 0x02) != 0 && !cpu.Standby) lamps |= DskyLamps.ComputerActivity;
            if ((lampWord & 0x04) != 0) lamps |= DskyLamps.UplinkActivity;
            if ((lampWord & 0x08) != 0 || (cpu.Channels.Get(0x18) & 0x4000) != 0) lamps |= DskyLamps.Temperature;
            if ((lampWord & 0x10) != 0 && FlashVisible) lamps |= DskyLamps.KeyRelease;
            if ((lampWord & 0x40) != 0 && FlashVisible) lamps |= DskyLamps.OperatorError;
            if ((relayWord & 0x08) != 0) lamps |= DskyLamps.NoAttitude;
            if ((relayWord & 0x10) != 0) lamps |= DskyLamps.Altitude;
            if ((relayWord & 0x04) != 0) lamps |= DskyLamps.Velocity;
            if ((relayWord & 0x20) != 0) lamps |= DskyLamps.GimbalLock;
            if ((relayWord & 0x80) != 0) lamps |= DskyLamps.Tracker;
            if ((relayWord & 0x100) != 0) lamps |= DskyLamps.Program;
            if (cpu.Standby || lampTest) lamps |= DskyLamps.Standby;
            if (cpu.RestartLight || lampTest) lamps |= DskyLamps.Restart;
            return lamps;
        }
    }

    // PROG, VERB, NOUN, then the five digits of R1, R2 and R3.
    public char Digit(int position)
    {
        if (cpu.Standby || (position is >= 2 and < 6 && !FlashVisible &&
            (cpu.Channels.Get(LampChannel) & 0x20) != 0))
            return ' ';

        var (row, left) = DigitRelays[position];
        var word = cpu.Channels.GetLatchedRelayRow(row);
        return ((word >> (left ? 5 : 0)) & 31) switch
        {
            21 => '0', 3 => '1', 25 => '2', 27 => '3', 15 => '4',
            30 => '5', 28 => '6', 19 => '7', 29 => '8', 31 => '9', _ => ' '
        };
    }

    public char Sign(int register)
    {
        if (cpu.Standby)
            return ' ';

        var plusRow = register switch { 0 => 7, 1 => 5, 2 => 2, _ => throw new ArgumentOutOfRangeException(nameof(register)) };
        if ((cpu.Channels.GetLatchedRelayRow(plusRow) & 0x400) != 0)
            return '+';
        return (cpu.Channels.GetLatchedRelayRow(plusRow - 1) & 0x400) != 0 ? '-' : ' ';
    }

    public void Press(DskyKey key)
    {
        if (key == DskyKey.Proceed)
            cpu.SetDskyProceed(true);
        else
            cpu.PressDskyKey((int)key);
    }

    public void Release(DskyKey key)
    {
        if (key == DskyKey.Proceed)
            cpu.SetDskyProceed(false);
    }
}
