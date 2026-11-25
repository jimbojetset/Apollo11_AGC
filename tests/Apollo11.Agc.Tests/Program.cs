using System.Security.Cryptography;
using Apollo11.Agc.Dsky;
using Apollo11.Agc.Hardware;

var tests = new (string Name, Action Run)[]
{
    ("one's-complement end-around carry", OnesComplementEndAroundCarry),
    ("erasable and fixed bank selection", BankSelection),
    ("editing registers", EditingRegisters),
    ("basic instruction execution", BasicInstructions),
    ("DDOUBL central-register path", DoubleDouble),
    ("extracode I/O", ExtracodeIo),
    ("DOWNRUPT timing", DownruptTiming),
    ("priority interrupt and RESUME", InterruptAndResume),
    ("timer scaler", TimerScaler),
    ("Luminary 099 rope", LuminaryRope),
    ("DSKY display relays and flashing", DskyRelays),
    ("DSKY keypad and standby wiring", DskyKeypad),
    ("Luminary 099 DSKY lamp test", LuminaryLampTest)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

return failed == 0 ? 0 : 1;

static void OnesComplementEndAroundCarry()
{
    Equal(1, AgcWord.Add(AgcWord.SignExtend(AgcWord.NegativeZero), 1));
    Equal(1, AgcWord.Overflow(AgcWord.Add(AgcWord.SignExtend(0x3fff), 1)));
    Equal(-123, AgcWord.ToInt32(AgcWord.FromInt32(-123)));
    Equal(AgcWord.NegativeZero, AgcWord.Negate(0));
}

static void BankSelection()
{
    var memory = new AgcMemory();
    memory.WriteErasable(5, 0x23, 0x1234);
    memory[AgcMemory.Eb] = 5 << 8;
    Equal(0x1234, memory[0x323]);

    memory.LoadFixedWord(0x1b, 7, 0x2222, false);
    memory[AgcMemory.Fb] = 0x1b << 10;
    Equal(0x2222, memory[0x407]);

    memory.LoadFixedWord(0x23, 7, 0x3333, false);
    memory.Superbank = 0x40;
    Equal(0x3333, memory[0x407]);
}

static void EditingRegisters()
{
    var memory = new AgcMemory();
    memory[AgcMemory.Cyr] = 1;
    Equal(0x4000, memory[AgcMemory.Cyr]);
    memory[AgcMemory.Sr] = 0x4001;
    Equal(0x6000, memory[AgcMemory.Sr]);
    memory[AgcMemory.Cyl] = 0x4000;
    Equal(1, memory[AgcMemory.Cyl]);
    memory[AgcMemory.Edop] = 0x3f80;
    Equal(0x7f, memory[AgcMemory.Edop]);
}

static void BasicInstructions()
{
    var cpu = new AgcCpu();
    Write(cpu, 0x080, (0x18 << 9) | 0x090); // CA 0220
    Write(cpu, 0x081, (0x30 << 9) | 0x091); // AD 0221
    Write(cpu, 0x082, (0x2c << 9) | 0x092); // TS 0222
    Write(cpu, 0x090, 10);
    Write(cpu, 0x091, 20);
    cpu.Memory[AgcMemory.Z] = 0x080;

    _ = cpu.Step();
    _ = cpu.Step();
    _ = cpu.Step();

    Equal(30, cpu.Memory[0x092]);
    Equal(0x083, cpu.Z);
}

static void ExtracodeIo()
{
    var cpu = new AgcCpu();
    Write(cpu, 0x080, 6); // EXTEND
    Write(cpu, 0x081, (1 << 9) | 0x08); // WRITE 10 octal
    cpu.Memory[AgcMemory.A] = AgcWord.SignExtend(0x1234);
    cpu.Memory[AgcMemory.Z] = 0x080;

    _ = cpu.Step();
    _ = cpu.Step();

    Equal(0x1234, cpu.Channels.Get(0x08));
    Equal(0x1234, cpu.Channels.GetLatchedRelayRow(2));
}

static void DoubleDouble()
{
    var cpu = new AgcCpu();
    Write(cpu, 0x080, (0x10 << 9) | AgcMemory.L); // DAS L (DDOUBL)
    cpu.Memory[AgcMemory.A] = AgcWord.SignExtend(2);
    cpu.Memory[AgcMemory.L] = AgcWord.SignExtend(3);
    cpu.Memory[AgcMemory.Z] = 0x080;

    _ = cpu.Step();

    Equal(4, cpu.A);
    Equal(6, cpu.L);
}

static void DownruptTiming()
{
    var cpu = new AgcCpu();
    Write(cpu, 0x080, 6); // EXTEND
    Write(cpu, 0x081, (1 << 9) | 0x1c); // WRITE 34 octal
    Write(cpu, 0x082, 6);
    Write(cpu, 0x083, (1 << 9) | 0x1d); // WRITE 35 octal
    Write(cpu, 0x084, (0x0a << 9) | 0x084);
    cpu.Memory[AgcMemory.Z] = 0x080;

    cpu.Run(2_000);

    Equal(8, cpu.LastInterrupt);
    True(cpu.InInterrupt, "DOWNRUPT did not enter its interrupt sequence.");
}

static void InterruptAndResume()
{
    var cpu = new AgcCpu();
    Write(cpu, 0x080, (0x18 << 9) | 0x090); // Interrupted CA
    Write(cpu, 0x090, 0x321);
    cpu.Memory[AgcMemory.Z] = 0x080;
    cpu.RequestInterrupt(3);

    _ = cpu.Step();
    Equal(0x80c, cpu.Z);
    Equal(0x081, cpu.Memory[AgcMemory.Zrupt]);

    Write(cpu, 0x80c, (0x28 << 9) | AgcMemory.Brupt); // RESUME
    _ = cpu.Step();
    _ = cpu.Step();
    Equal(0x321, AgcWord.OverflowCorrect(cpu.A));
    Equal(0x081, cpu.Z);
}

static void TimerScaler()
{
    var cpu = new AgcCpu();
    Write(cpu, 0x080, (0x0a << 9) | 0x080); // TCF to itself, one MCT.
    cpu.Memory[AgcMemory.Z] = 0x080;
    cpu.Run(900);
    Equal(1, cpu.Memory.ReadErasable(0, AgcMemory.Time1));
    Equal(1, cpu.Memory.ReadErasable(0, AgcMemory.Time3));
}

static void LuminaryRope()
{
    var path = Path.Combine(AppContext.BaseDirectory, "roms", "Luminary099.bin");
    var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    Equal("b7a2abb432df5d4ce242c238e25fd2cf6711f72fb88b888a4b972afb3dea41c1", hash);

    var cpu = new AgcCpu();
    RopeImage.Load(path, cpu.Memory);
    True(cpu.Memory.CheckFixedParity, "Luminary rope was not assembled with parity.");
    Equal(4, cpu.Memory[0x800]);
    True(!cpu.Memory.FixedParityFailure, "Luminary's first rope word failed odd parity.");

    for (var instruction = 0; instruction < 8; instruction++)
        _ = cpu.Step();
    // This state is also produced by yaAGC at the DXCH RSBBQ boundary.
    Equal(0x5bb, cpu.Z);
    Equal(0, cpu.A);
    Equal(0, cpu.L);
    Equal(0, cpu.Q);
    Equal(0x300, cpu.Memory[AgcMemory.Eb]);
    Equal(0x1400, cpu.Memory[AgcMemory.Fb]);
    Equal(0x1403, cpu.Memory[AgcMemory.Bb]);

    cpu.Run(20_000);
    True(cpu.InstructionCount > 5_000, "Luminary did not execute enough instructions.");
    True(cpu.Z != 0x800, "Luminary remained at its reset vector.");
}

static void DskyRelays()
{
    var cpu = new AgcCpu();
    var panel = new DskyPanel(cpu);
    cpu.Channels.Write(0x08, (11 << 11) | (3 << 5) | 25, cpu.Memory);
    cpu.Channels.Write(0x08, (10 << 11) | (27 << 5) | 15, cpu.Memory);
    cpu.Channels.Write(0x08, (3 << 11) | (30 << 5) | 28, cpu.Memory);
    cpu.Channels.Write(0x08, (7 << 11) | 0x400, cpu.Memory);
    cpu.Channels.Write(0x08, (12 << 11) | 0x104, cpu.Memory);
    cpu.Channels.Write(0x08, 0, cpu.Memory); // Clearing the drive lines leaves the relays latched.
    cpu.Channels.Write(0x09, 0x62, cpu.Memory);

    Equal('1', panel.Digit(0));
    Equal('2', panel.Digit(1));
    Equal(' ', panel.Digit(2));
    Equal('5', panel.Digit(15));
    Equal('6', panel.Digit(16));
    Equal('+', panel.Sign(0));
    Equal(DskyLamps.ComputerActivity | DskyLamps.Program | DskyLamps.Velocity | DskyLamps.Restart, panel.Lamps);

    Write(cpu, 0x800, (0x0a << 9) | 0x800);
    cpu.Run(28_000);
    Equal('3', panel.Digit(2));
    True(panel.Lamps.HasFlag(DskyLamps.OperatorError), "OPR ERR did not turn on in the visible flash phase.");
}

static void DskyKeypad()
{
    var cpu = new AgcCpu();
    var panel = new DskyPanel(cpu);
    cpu.Channels.SetInput(0x0d, 0x200);
    panel.Press(DskyKey.Reset);
    Equal(0x212, cpu.Channels.Get(0x0d));
    True(!panel.Lamps.HasFlag(DskyLamps.Restart), "RSET did not clear the hardware RESTART latch.");
    _ = cpu.Step();
    Equal(5, cpu.LastInterrupt);

    cpu.Reset();
    Write(cpu, 0x800, (0x0a << 9) | 0x800);
    cpu.Channels.Write(0x0b, 0x400, cpu.Memory); // Enable standby on channel 13.
    panel.Press(DskyKey.Proceed);
    Equal(0x5fff, cpu.Channels.Get(0x1a));
    cpu.Run(165_000);
    True(cpu.Standby && panel.Lamps.HasFlag(DskyLamps.Standby), "Long PRO did not enter standby.");
    var instructionCount = cpu.InstructionCount;
    cpu.Run(112_000);
    Equal(instructionCount, cpu.InstructionCount);
    panel.Release(DskyKey.Proceed);
    Equal(0x7fff, cpu.Channels.Get(0x1a));
    panel.Press(DskyKey.Proceed);
    cpu.Run(110_000);
    True(!cpu.Standby && cpu.InstructionCount > instructionCount, "A second long PRO did not wake the AGC.");
}

static void LuminaryLampTest()
{
    var cpu = new AgcCpu();
    RopeImage.Load(Path.Combine(AppContext.BaseDirectory, "roms", "Luminary099.bin"), cpu.Memory);
    var panel = new DskyPanel(cpu);
    var relayWords = new List<int>();
    cpu.Channels.Output += (channel, value) =>
    {
        if (channel == 0x08 && value != 0)
            relayWords.Add(value);
    };

    foreach (var key in new[] { DskyKey.Verb, DskyKey.Three, DskyKey.Five, DskyKey.Enter })
    {
        cpu.Run(15_000);
        panel.Press(key);
    }
    Equal(0x6100, relayWords[0]); // Virtual AGC's first nonzero display word is octal 60400.
    cpu.Run(46_000);

    Equal("60674 51675 45675 41675 37675 31675 27675 21675 15675 13675 05675 55675",
        string.Join(' ', relayWords.TakeLast(12).Select(word => Convert.ToString(word, 8).PadLeft(5, '0'))));
    Equal("888888888888888888888", string.Concat(Enumerable.Range(0, 21).Select(panel.Digit)));
    Equal('+', panel.Sign(0));
    Equal('+', panel.Sign(1));
    Equal('+', panel.Sign(2));
    True(panel.Lamps.HasFlag(DskyLamps.Standby | DskyLamps.Restart | DskyLamps.Altitude | DskyLamps.Velocity),
        "Luminary's V35 lamp test did not light STBY, RESTART, ALT and VEL.");
}

static void Write(AgcCpu cpu, int address, int value)
{
    if (address < 0x400)
        cpu.Memory.WriteErasable(address >> 8, address & 0xff, value);
    else
        cpu.Memory.LoadFixedWord(address < 0x800 ? 0 : address < 0xc00 ? 2 : 3, address & 0x3ff, value, false);
}

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"expected {expected}, got {actual}");
}

static void True(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
