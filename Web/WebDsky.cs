using Apollo11.Agc.Dsky;
using Apollo11.Agc.Hardware;
using Microsoft.JSInterop;

namespace Apollo11.Agc.Web;

internal sealed class WebDsky
{
    private readonly DskyCanvas _canvas = new();
    private readonly Queue<DskyKey> _pendingKeys = new();
    private readonly HashSet<DskyKey> _heldKeys = [];
    private readonly char[] _display = new char[24];
    private AgcCpu _cpu;
    private DskyPanel _panel;
    private double _cycleTarget;
    private long _nextKeyCycle;
    private uint _renderedKeys;
    private DskyLamps _renderedLamps;
    private bool _renderedStandby;
    private DskyFace? _face;

    public WebDsky(AgcCpu cpu, DskySpacecraft spacecraft)
    {
        _cpu = cpu;
        _panel = new DskyPanel(cpu, spacecraft);
        _cycleTarget = cpu.CycleCount;
    }

    public void Reset(AgcCpu cpu, DskySpacecraft spacecraft)
    {
        ReleaseKeys();
        _cpu = cpu;
        _panel = new DskyPanel(cpu, spacecraft);
        _cycleTarget = cpu.CycleCount;
        _nextKeyCycle = cpu.CycleCount;
        _face = null;
    }

    [JSInvokable]
    public void SetKey(int code, bool pressed)
    {
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
}
