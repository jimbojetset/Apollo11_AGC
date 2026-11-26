namespace Apollo11.Agc.Hardware;

public sealed class AgcMemory
{
    public const int ErasableBankCount = 8;
    public const int FixedBankCount = 36;
    public const int ErasableBankSize = 0x100;
    public const int FixedBankSize = 0x400;

    public const int A = 0x00;
    public const int L = 0x01;
    public const int Q = 0x02;
    public const int Eb = 0x03;
    public const int Fb = 0x04;
    public const int Z = 0x05;
    public const int Bb = 0x06;
    public const int Zero = 0x07;
    public const int Zrupt = 0x0d;
    public const int Bbrupt = 0x0e;
    public const int Brupt = 0x0f;
    public const int Cyr = 0x10;
    public const int Sr = 0x11;
    public const int Cyl = 0x12;
    public const int Edop = 0x13;
    public const int Time2 = 0x14;
    public const int Time1 = 0x15;
    public const int Time3 = 0x16;
    public const int Time4 = 0x17;
    public const int Time5 = 0x18;
    public const int Time6 = 0x19;
    public const int Inlink = 0x25; // 0045 octal

    private readonly ushort[][] _erasable = CreateBanks(ErasableBankCount, ErasableBankSize);
    private readonly ushort[][] _fixed = CreateBanks(FixedBankCount, FixedBankSize);
    private readonly bool[][] _fixedParity = CreateParityBanks();

    public bool CheckFixedParity { get; set; }
    public bool FixedParityFailure { get; private set; }
    public int Superbank { get; set; }

    public int this[int address]
    {
        get => Read(address);
        set => Write(address, value);
    }

    public int Read(int address)
    {
        address &= 0xfff;
        if (address < 0x100)
            return _erasable[0][address];
        if (address < 0x200)
            return _erasable[1][address & 0xff];
        if (address < 0x300)
            return _erasable[2][address & 0xff];
        if (address < 0x400)
            return _erasable[SelectedErasableBank][address & 0xff];

        var bank = address < 0x800 ? SelectedFixedBank : address < 0xc00 ? 2 : 3;
        var offset = address & 0x3ff;
        if (bank >= FixedBankCount)
        {
            FixedParityFailure = true;
            return 0;
        }

        var word = _fixed[bank][offset];
        if (CheckFixedParity && !HasOddParity((word << 1) | (_fixedParity[bank][offset] ? 1 : 0)))
            FixedParityFailure = true;
        return word;
    }

    public void Write(int address, int value)
    {
        address &= 0xfff;
        if (address >= 0x400)
            return; // Core rope is physically read-only.

        var bank = address < 0x100 ? 0 : address < 0x200 ? 1 : address < 0x300 ? 2 : SelectedErasableBank;
        var offset = address & 0xff;
        // A, L and Q keep the upper sign bit. Only bank zero has editing registers.
        if (bank == 0 && offset >= Zero)
            value = Edit(offset, value & AgcWord.WordMask);
        _erasable[bank][offset] = (ushort)(value & (bank == 0 && offset < 3 ? AgcWord.RegisterMask : AgcWord.WordMask));
        if (bank == 0)
            MirrorBankRegisters(offset);
    }

    public int ReadErasable(int bank, int offset)
    {
        return _erasable[bank][offset];
    }

    public void WriteErasable(int bank, int offset, int value)
    {
        _erasable[bank][offset] = (ushort)(value & AgcWord.WordMask);
    }

    public int ReadFixed(int bank, int offset)
    {
        return _fixed[bank][offset];
    }

    public void LoadFixedWord(int bank, int offset, int word, bool parity)
    {
        _fixed[bank][offset] = (ushort)(word & AgcWord.WordMask);
        _fixedParity[bank][offset] = parity;
    }

    public void ClearParityFailure()
    {
        FixedParityFailure = false;
    }

    public int SelectedErasableBank => (_erasable[0][Eb] >> 8) & 0x7;

    public int SelectedFixedBank
    {
        get
        {
            var bank = (_erasable[0][Fb] >> 10) & 0x1f;
            if ((bank & 0x18) == 0x18 && Superbank != 0)
                bank += 0x8;
            return bank;
        }
    }

    private static int Edit(int offset, int value)
    {
        return offset switch
        {
            Zero => 0,
            Cyr => (value >> 1) | ((value & 1) << 14),
            Sr => (value >> 1) | (value & AgcWord.SignBit),
            Cyl => ((value << 1) | ((value >> 14) & 1)) & AgcWord.WordMask,
            Edop => (value >> 7) & 0x7f,
            _ => value
        };
    }

    private void MirrorBankRegisters(int offset)
    {
        if (offset == Bb)
        {
            _erasable[0][Bb] &= 0x7c07;
            _erasable[0][Fb] = (ushort)(_erasable[0][Bb] & 0x7c00);
            _erasable[0][Eb] = (ushort)((_erasable[0][Bb] & 0x7) << 8);
        }
        else if (offset is Eb or Fb)
        {
            _erasable[0][Eb] &= 0x700;
            _erasable[0][Fb] &= 0x7c00;
            _erasable[0][Bb] = (ushort)(_erasable[0][Fb] | ((_erasable[0][Eb] >> 8) & 0x7));
        }
    }

    private static ushort[][] CreateBanks(int count, int size)
    {
        return Enumerable.Range(0, count).Select(_ => new ushort[size]).ToArray();
    }

    private static bool[][] CreateParityBanks()
    {
        return Enumerable.Range(0, FixedBankCount).Select(_ => new bool[FixedBankSize]).ToArray();
    }

    private static bool HasOddParity(int value)
    {
        value ^= value >> 8;
        value ^= value >> 4;
        value ^= value >> 2;
        value ^= value >> 1;
        return (value & 1) != 0;
    }
}
