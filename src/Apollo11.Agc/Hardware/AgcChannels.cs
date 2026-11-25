namespace Apollo11.Agc.Hardware;

public sealed class AgcChannels
{
    private readonly ushort[] _channels = new ushort[512];
    private readonly ushort[] _relayRows = new ushort[16];

    public AgcChannels()
    {
        _channels[0x18] = 0x3fff; // Channel 30 has fourteen inverted input lines.
        _channels[0x19] = AgcWord.WordMask;
        _channels[0x1a] = AgcWord.WordMask;
        _channels[0x1b] = AgcWord.WordMask;
    }

    public event Action<int, int>? Output;

    public int Read(int channel, AgcMemory memory)
    {
        channel &= 0x1ff;
        return channel is AgcMemory.L or AgcMemory.Q ? memory[channel] : _channels[channel];
    }

    public void Write(int channel, int value, AgcMemory memory)
    {
        channel &= 0x1ff;
        value &= AgcWord.WordMask;
        if (channel is AgcMemory.L or AgcMemory.Q)
            memory[channel] = value;

        if (channel == 0x07)
            value &= 0x70;
        else if (channel == 0x08)
            _relayRows[(value >> 11) & 0xf] = (ushort)value;
        else if (channel == 0x1b)
            value = (_channels[channel] & 0x7c00) | (value & 0x3ff);

        _channels[channel] = (ushort)value;
        if (channel == 0x07)
            memory.Superbank = value & 0x40;
        Output?.Invoke(channel, value);
    }

    public void SetInput(int channel, int value) => _channels[channel & 0x1ff] = (ushort)(value & AgcWord.WordMask);

    public int Get(int channel) => _channels[channel & 0x1ff];

    public int GetLatchedRelayRow(int row) => _relayRows[row & 0xf];
}
