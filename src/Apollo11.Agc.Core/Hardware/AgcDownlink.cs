namespace Apollo11.Agc.Hardware;

public sealed class AgcDownlink
{
    private const int OrderChannel = 0x0b; // 013 octal
    private const int FirstChannel = 0x1c; // 034 octal
    private const int SecondChannel = 0x1d; // 035 octal
    private const int SyncWord = 0x7ee0; // 77340 octal
    private const int ClockPair = 51;
    private const int StandardPairs = 100;
    private const int DumpPairs = 130;

    public const int CoastAndAlign = 0x7fff;
    public const int EntryOrAgsUpdate = 0x7ffe;
    public const int RendezvousAndPrethrust = 0x7ffd;
    public const int PoweredOrOrbitalManeuvers = 0x7ffc;
    public const int Program22OrDescentAndAscent = 0x7ffb;
    public const int LunarSurfaceAlign = 0x7ffa;
    public const int CommandModuleDump = 0x03ff;
    public const int LunarModuleDump = 0x03fe;

    private readonly ushort[] _words = new ushort[2 * DumpPairs];
    private int _wordIndex;
    private int _expectedWords;
    private bool _orderBit;

    public int? FirstWord { get; private set; }
    public int? SecondWord { get; private set; }
    public long WordCount { get; private set; }
    public long ListCount { get; private set; }
    public Downlist? Latest { get; private set; }

    public void Record(int channel, int value, long cycle)
    {
        switch (channel)
        {
            case OrderChannel:
                _orderBit = (value & 0x40) != 0;
                return;
            case FirstChannel:
                FirstWord = value;
                break;
            case SecondChannel:
                SecondWord = value;
                break;
            default:
                return;
        }
        WordCount++;

        // The order bit is low at the header, and again on the clock pair of a standard downlist.
        var clockWord = _expectedWords == 2 * StandardPairs && _wordIndex / 2 == ClockPair - 1;
        if (channel == FirstChannel && !_orderBit && (!clockWord || (_wordIndex & 1) != 0))
        {
            _wordIndex = 0;
            _expectedWords = DownlistLength(value);
        }

        if (_expectedWords == 0)
            return;

        var expectsFirst = (_wordIndex & 1) == 0;
        var expectsMarker = _wordIndex < 2 ||
            (_expectedWords == 2 * StandardPairs && _wordIndex / 2 == ClockPair - 1);
        if ((channel == FirstChannel) != expectsFirst || _orderBit == expectsMarker ||
            (_wordIndex == 1 && value != SyncWord))
        {
            _expectedWords = 0;
            _wordIndex = 0;
            return;
        }

        // A rope restart can send a new header in the slot where we were waiting for TIME2/TIME1.
        if (clockWord && channel == SecondChannel && value == SyncWord &&
            DownlistLength(_words[_wordIndex - 1]) is int newLength and > 0)
        {
            _words[0] = _words[_wordIndex - 1];
            _expectedWords = newLength;
            _wordIndex = 1;
        }

        _words[_wordIndex++] = (ushort)value;
        if (_wordIndex == _expectedWords)
        {
            Latest = new Downlist(_words[.._wordIndex], cycle);
            ListCount++;
            _expectedWords = 0;
            _wordIndex = 0;
        }
    }

    private static int DownlistLength(int id) => id switch
    {
        >= LunarSurfaceAlign and <= CoastAndAlign => 2 * StandardPairs,
        CommandModuleDump or LunarModuleDump => 2 * DumpPairs,
        _ => 0
    };

    public sealed class Downlist
    {
        private readonly ushort[] _words;

        internal Downlist(ushort[] words, long receivedCycle)
        {
            _words = words;
            ReceivedCycle = receivedCycle;
        }

        public int Id => _words[0];
        public int PairCount => _words.Length / 2;
        public bool IsMemoryDump => PairCount == DumpPairs;
        public long ReceivedCycle { get; }
        public int First(int pair) => _words[2 * (pair - 1)];
        public int Second(int pair) => _words[2 * (pair - 1) + 1];

        public int DoublePrecision(int pair)
        {
            var high = First(pair);
            var low = Second(pair);
            var negative = (high & AgcWord.SignBit) != 0;
            if (negative)
            {
                high = AgcWord.Negate(high);
                low = AgcWord.Negate(low);
            }

            // Each half carries a sign bit. The low half contributes 14 magnitude bits.
            var magnitude = (high << 14) | (low & (AgcWord.SignBit - 1));
            return negative ? -magnitude : magnitude;
        }
    }
}
