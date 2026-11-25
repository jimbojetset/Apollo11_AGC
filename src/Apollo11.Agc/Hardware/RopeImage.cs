namespace Apollo11.Agc.Hardware;

public static class RopeImage
{
    private static readonly int[] FirstBanks = [2, 3, 0, 1];

    public static void Load(Stream stream, AgcMemory memory)
    {
        if (!stream.CanSeek)
            throw new ArgumentException("A rope image stream must be seekable.", nameof(stream));
        if ((stream.Length & 1) != 0 || stream.Length > AgcMemory.FixedBankCount * AgcMemory.FixedBankSize * 2)
            throw new InvalidDataException("The rope image must contain at most 36 banks of 16-bit words.");

        var bytes = new byte[2];
        var wordCount = checked((int)stream.Length / 2);
        var hasParity = false;
        for (var index = 0; index < wordCount; index++)
        {
            stream.ReadExactly(bytes);
            var raw = (bytes[0] << 8) | bytes[1];
            var fileBank = index / AgcMemory.FixedBankSize;
            var bank = fileBank < FirstBanks.Length ? FirstBanks[fileBank] : fileBank;
            var parity = (raw & 1) != 0;
            memory.LoadFixedWord(bank, index & (AgcMemory.FixedBankSize - 1), raw >> 1, parity);
            hasParity |= parity;
        }

        memory.CheckFixedParity = hasParity;
    }

    public static void Load(string path, AgcMemory memory)
    {
        using var stream = File.OpenRead(path);
        Load(stream, memory);
    }
}
