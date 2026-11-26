using Apollo11.Agc.Dsky;
using Apollo11.Agc.Hardware;
using Microsoft.JSInterop;

namespace Apollo11.Agc.Web;

internal sealed class WebDsky
{
    public const int DownlinkFirstChannel = 0x1c; // 34 octal
    public const int DownlinkSecondChannel = 0x1d; // 35 octal

    private readonly DskyCanvas _canvas = new();
    private readonly Queue<DskyKey> _pendingKeys = new();
    private readonly HashSet<DskyKey> _heldKeys = [];
    private readonly char[] _display = new char[24];
    private AgcCpu _cpu;
    private DskyPanel _panel;
    private double _cycleTarget;
    private long _nextKeyCycle;
    private long _nextSystemsRefresh;
    private uint _renderedKeys;
    private DskyLamps _renderedLamps;
    private bool _renderedStandby;
    private DskyFace? _face;

    public event Action? SystemsUpdated;
    public ComancheOrbitUplink OrbitUplink { get; private set; }
    public int? DownlinkFirstWord { get; private set; }
    public int? DownlinkSecondWord { get; private set; }
    public long DownlinkWordCount { get; private set; }
    public bool UplinkActivity => _panel.Lamps.HasFlag(DskyLamps.UplinkActivity);

    public OrbitReadout? OrbitResult
    {
        get
        {
            if (!OrbitUplink.IsComplete || _cpu.Standby ||
                _panel.LatchedDigit(2) != '1' || _panel.LatchedDigit(3) != '6' ||
                _panel.LatchedDigit(4) != '4' || _panel.LatchedDigit(5) != '4' ||
                !TryReadHeight(0, out var apogee) || !TryReadHeight(1, out var perigee))
                return null;

            // Wait until the rope has written the time field as well as both heights.
            Span<char> time = stackalloc char[6];
            time[0] = _panel.Sign(2);
            for (var i = 1; i < time.Length; i++)
                time[i] = _panel.LatchedDigit(15 + i);
            if (time[0] is not ('+' or '-') || time[1] is < '0' or > '9' ||
                time[2] is < '0' or > '9' || time[4] is < '0' or > '9' || time[5] is < '0' or > '9')
                return null;

            return new OrbitReadout(apogee, perigee, new string(time));
        }
    }

    public int ChannelValue(int channel) => _cpu.Channels.Get(channel);

    public WebDsky(AgcCpu cpu, DskySpacecraft spacecraft)
    {
        _cpu = cpu;
        _panel = new DskyPanel(cpu, spacecraft);
        OrbitUplink = new ComancheOrbitUplink(cpu, _panel);
        _cpu.Channels.Output += RecordDownlink;
        _cycleTarget = cpu.CycleCount;
    }

    public void Reset(AgcCpu cpu, DskySpacecraft spacecraft)
    {
        ReleaseKeys();
        _cpu.Channels.Output -= RecordDownlink;
        _cpu = cpu;
        _panel = new DskyPanel(cpu, spacecraft);
        OrbitUplink = new ComancheOrbitUplink(cpu, _panel);
        _cpu.Channels.Output += RecordDownlink;
        DownlinkFirstWord = null;
        DownlinkSecondWord = null;
        DownlinkWordCount = 0;
        _cycleTarget = cpu.CycleCount;
        _nextKeyCycle = cpu.CycleCount;
        _nextSystemsRefresh = cpu.CycleCount;
        _face = null;
    }

    public void StartOrbitUplink()
    {
        if (OrbitUplink.TryStart())
            ReleaseKeys();
    }

    [JSInvokable]
    public void SetKey(int code, bool pressed)
    {
        if (OrbitUplink.IsTransmitting)
            return;

        var key = (DskyKey)code;
        if (pressed)
        {
            if (!_heldKeys.Add(key))
                return;
            if (key == DskyKey.Proceed)
                _panel.Press(key);
            else
                _pendingKeys.Enqueue(key);
        }
        else if (_heldKeys.Remove(key) && key == DskyKey.Proceed)
        {
            _panel.Release(key);
        }
    }

    [JSInvokable]
    public void ReleaseKeys()
    {
        _heldKeys.Clear();
        _pendingKeys.Clear();
        _panel.Release(DskyKey.Proceed);
    }

    [JSInvokable]
    public byte[]? AdvanceFrame(double elapsedSeconds)
    {
        _cycleTarget += elapsedSeconds * (1_024_000.0 / 12);
        var target = (long)_cycleTarget;
        while (_cpu.CycleCount < target)
        {
            if (OrbitUplink.IsTransmitting)
            {
                OrbitUplink.RunUntil(target);
                break;
            }
            if (_pendingKeys.Count == 0)
            {
                _cpu.Run(target - _cpu.CycleCount);
                break;
            }
            if (_cpu.CycleCount >= _nextKeyCycle)
            {
                _panel.Press(_pendingKeys.Dequeue());
                // KEYRUPT needs time to read the latch before the next key replaces it.
                _nextKeyCycle = _cpu.CycleCount + AgcCpu.MachineCyclesPerSecond / 50;
            }
            _cpu.Run(Math.Min(target, _nextKeyCycle) - _cpu.CycleCount);
        }

        if (_cpu.CycleCount >= _nextSystemsRefresh)
        {
            _nextSystemsRefresh = _cpu.CycleCount + AgcCpu.MachineCyclesPerSecond / 4;
            SystemsUpdated?.Invoke();
        }

        Span<char> display = stackalloc char[24];
        for (var i = 0; i < 21; i++)
            display[i] = _panel.Digit(i);
        for (var i = 0; i < 3; i++)
            display[21 + i] = _panel.Sign(i);
        uint keys = 0;
        foreach (var key in _heldKeys)
            keys |= 1u << (int)key;
        var lamps = _panel.Lamps;
        if (_face is not null && display.SequenceEqual(_display) && keys == _renderedKeys &&
            lamps == _renderedLamps && _cpu.Standby == _renderedStandby)
            return null;

        display.CopyTo(_display);
        _renderedKeys = keys;
        _renderedLamps = lamps;
        _renderedStandby = _cpu.Standby;
        _face ??= new DskyFace(_canvas);
        _face.Draw(_panel, _heldKeys, null);
        return _canvas.ReadFrame();
    }

    private void RecordDownlink(int channel, int value)
    {
        if (channel == DownlinkFirstChannel)
            DownlinkFirstWord = value;
        else if (channel == DownlinkSecondChannel)
            DownlinkSecondWord = value;
        else
            return;

        DownlinkWordCount++;
    }

    private bool TryReadHeight(int register, out OrbitHeight height)
    {
        height = default;
        var sign = _panel.Sign(register);
        if (sign is not ('+' or '-'))
            return false;

        var tenthsOfNauticalMile = 0;
        for (var position = 6 + 5 * register; position < 11 + 5 * register; position++)
        {
            var digit = _panel.LatchedDigit(position);
            if (digit is < '0' or > '9')
                return false;
            tenthsOfNauticalMile = 10 * tenthsOfNauticalMile + digit - '0';
        }

        height = new OrbitHeight(sign, tenthsOfNauticalMile);
        return true;
    }

    internal readonly record struct OrbitReadout(OrbitHeight Apogee, OrbitHeight Perigee, string TimeToReference);

    internal readonly record struct OrbitHeight(char Sign, int TenthsOfNauticalMile)
    {
        public string DskyText => $"{Sign}{TenthsOfNauticalMile:00000}";
        public decimal NauticalMiles => (Sign == '-' ? -TenthsOfNauticalMile : TenthsOfNauticalMile) / 10m;
        public decimal Kilometres => NauticalMiles * 1.852m;
    }
}
