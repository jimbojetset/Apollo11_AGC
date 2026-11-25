namespace Apollo11.Agc.Hardware;

public static class AgcWord
{
    public const int WordMask = 0b111_111_111_111_111;
    public const int RegisterMask = 0b1_111_111_111_111_111;
    public const int SignBit = 0b100_000_000_000_000;
    public const int RegisterSignBit = 0b1_000_000_000_000_000;
    public const int NegativeZero = WordMask;

    public static int SignExtend(int word)
    {
        word &= WordMask;
        return word | ((word << 1) & RegisterSignBit);
    }

    public static int OverflowCorrect(int value) =>
        (value & 0b011_111_111_111_111) | ((value >> 1) & SignBit);

    public static int Overflow(int value) => (value & 0xc000) switch
    {
        0x4000 => 1,
        0x8000 => WordMask - 1,
        _ => 0
    };

    public static int Add(int left, int right)
    {
        var sum = (left & RegisterMask) + (right & RegisterMask);
        if ((sum & 0x10000) != 0)
            sum++;

        return sum & RegisterMask;
    }

    public static int Negate(int value) => ~value & WordMask;

    public static int ToInt32(int value)
    {
        value &= WordMask;
        return (value & SignBit) == 0 ? value : -(~value & (SignBit - 1));
    }

    public static int FromInt32(int value) =>
        value < 0 ? ~(-value) & WordMask : value & WordMask;

    public static bool Increment(ref ushort counter)
    {
        if (counter == SignBit - 1)
        {
            counter = 0;
            return true;
        }

        var value = (counter + 1) & WordMask;
        if (value == 0)
            value = 1; // PINC crosses from minus zero to plus one.
        counter = (ushort)value;
        return false;
    }

    public static bool Diminish(ref ushort counter)
    {
        if (counter is 0 or NegativeZero)
            return true;

        counter = (ushort)((counter & SignBit) != 0
            ? Add(SignExtend(counter), SignExtend(1)) & WordMask
            : Add(SignExtend(counter), SignExtend(WordMask - 1)) & WordMask);
        return false;
    }
}
