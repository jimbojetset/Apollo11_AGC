namespace Apollo11.Agc.Hardware;

public sealed class AgcCpu
{
    public const int MachineCyclesPerSecond = 1024000 / 12;

    private static readonly int[] BasicCycles =
    [
        1, 1, 1, 1, 2, 1, 1, 1, 3, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 3, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2
    ];

    private static readonly int[] ExtendedCycles =
    [
        2, 2, 2, 2, 6, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3,
        3, 3, 3, 3, 2, 2, 2, 2, 2, 1, 1, 1, 3, 3, 3, 3
    ];

    private readonly bool[] _interruptRequests = new bool[11];
    private int _index;
    private int _scaler;
    private int _counterDelay;
    private int _downlinkWords;
    private int _nextZ;
    private long _downruptCycle;
    private bool _substituteInstruction;
    private bool _trap31A;
    private bool _trap31B;
    private bool _trap32;

    public AgcCpu(AgcMemory? memory = null, AgcChannels? channels = null)
    {
        Memory = memory ?? new AgcMemory();
        Channels = channels ?? new AgcChannels();
        Reset();
    }

    public AgcMemory Memory { get; }
    public AgcChannels Channels { get; }
    public long CycleCount { get; private set; }
    public long InstructionCount { get; private set; }
    public bool ExtraCode { get; private set; }
    public bool InterruptsEnabled { get; private set; }
    public bool InInterrupt { get; private set; }
    public int LastInterrupt { get; private set; }
    public int LastProgramCounter { get; private set; }
    public int LastInstruction { get; private set; }

    public int A => Memory[AgcMemory.A];
    public int L => Memory[AgcMemory.L];
    public int Q => Memory[AgcMemory.Q];
    public int Z => Memory[AgcMemory.Z] & 0xfff;

    public void Reset()
    {
        Memory[AgcMemory.A] = 0;
        Memory[AgcMemory.L] = 0;
        Memory[AgcMemory.Q] = 0;
        Memory[AgcMemory.Eb] = 0;
        Memory[AgcMemory.Fb] = 0;
        Memory[AgcMemory.Z] = 0x800; // GOJAM transfers control to fixed-fixed 4000 octal.
        Array.Clear(_interruptRequests);
        CycleCount = 0;
        InstructionCount = 0;
        _index = 0;
        _scaler = 0;
        _counterDelay = 0;
        _downlinkWords = 0;
        _downruptCycle = -1;
        ExtraCode = false;
        InterruptsEnabled = true;
        InInterrupt = false;
        LastInterrupt = 0;
        LastProgramCounter = 0;
        LastInstruction = 0;
        _substituteInstruction = false;
        _trap31A = false;
        _trap31B = false;
        _trap32 = false;
    }

    public void RequestInterrupt(int priority)
    {
        if (priority is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(priority));
        _interruptRequests[priority] = true;
    }

    public int Step()
    {
        var startingCycle = CycleCount;
        WaitForCpuCycle();

        var programCounter = Z;
        var fetched = Memory.Read(programCounter);
        var instruction = _substituteInstruction
            ? Memory[AgcMemory.Brupt]
            : AgcWord.OverflowCorrect(AgcWord.Add(AgcWord.SignExtend(_index), AgcWord.SignExtend(fetched)));
        instruction &= AgcWord.WordMask;
        LastProgramCounter = programCounter;
        LastInstruction = instruction;

        var wasExtended = ExtraCode;
        var extendedOpcode = (instruction >> 9) | (wasExtended ? 0x40 : 0);
        var overflow = AgcWord.Overflow(Memory[AgcMemory.A]) != 0;
        if ((CanTakeInterrupt(instruction, overflow) || extendedOpcode == 0x47) &&
            TakeInterrupt(programCounter, instruction, extendedOpcode))
        {
            WaitForCpuCycle(); // ZRUPT and BRUPT each require an MCT.
            return checked((int)(CycleCount - startingCycle));
        }

        var instructionCycles = (wasExtended ? ExtendedCycles : BasicCycles)[instruction >> 10];
        for (var cycle = 1; cycle < instructionCycles; cycle++)
            WaitForCpuCycle();

        _nextZ = (programCounter + 1) & 0xffff;
        _index = 0;
        _substituteInstruction = false;
        var keepExtraCode = false;

        Execute(extendedOpcode, instruction, ref keepExtraCode);

        Memory[AgcMemory.Zero] = 0;
        Memory[AgcMemory.L] = AgcWord.SignExtend(AgcWord.OverflowCorrect(Memory[AgcMemory.L]));
        Memory[AgcMemory.Z] = _substituteInstruction ? _nextZ : _nextZ & 0xfff;
        if (!keepExtraCode)
            ExtraCode = false;

        InstructionCount++;
        return checked((int)(CycleCount - startingCycle));
    }

    public void Run(long machineCycles)
    {
        var target = CycleCount + machineCycles;
        while (CycleCount < target)
            Step();
    }

    private void Execute(int opcode, int instruction, ref bool keepExtraCode)
    {
        var address12 = instruction & 0xfff;
        var address10 = instruction & 0x3ff;
        var address9 = instruction & 0x1ff;
        var accumulator = Memory[AgcMemory.A];

        switch (opcode)
        {
            case <= 0x07: // TC and the RELINT, INHINT, EXTEND special cases.
                if (address12 == 3)
                    InterruptsEnabled = true;
                else if (address12 == 4)
                    InterruptsEnabled = false;
                else if (address12 == 6)
                {
                    ExtraCode = true;
                    keepExtraCode = true;
                }
                else
                {
                    if (address12 != AgcMemory.Q)
                        Memory[AgcMemory.Q] = _nextZ;
                    _nextZ = address12;
                }
                break;

            case 0x08 or 0x09: // CCS
            {
                var value = address10 < 3 ? ReadOperand(address10) : ReadOperandAndEdit(address10);
                var corrected = address10 < 3 ? AgcWord.OverflowCorrect(value) : value;
                Memory[AgcMemory.A] = DiminishedAbsolute(address10 < 3 ? value : corrected, address10 < 3);
                if (address10 < 3 && AgcWord.Overflow(value) == 1)
                    break;
                if (address10 < 3 && AgcWord.Overflow(value) != 0)
                    _nextZ += 2;
                else if (corrected == 0)
                    _nextZ++;
                else if (corrected == AgcWord.NegativeZero)
                    _nextZ += 3;
                else if ((corrected & AgcWord.SignBit) != 0)
                    _nextZ += 2;
                break;
            }

            case >= 0x0a and <= 0x0f: // TCF
                _nextZ = address12;
                break;

            case 0x10 or 0x11: // DAS
                ExecuteDas(address10);
                break;

            case 0x12 or 0x13: // LXCH
                ExchangeRegister(AgcMemory.L, address10);
                break;

            case 0x14 or 0x15: // INCR
            {
                var sum = AgcWord.Add(AgcWord.SignExtend(1), SignExtendedOperand(address10));
                WriteOperand(address10, address10 < 3 ? sum : AgcWord.OverflowCorrect(sum));
                RequestTimerInterruptOnOverflow(address10, sum);
                break;
            }

            case 0x16 or 0x17: // ADS
            {
                var sum = AgcWord.Add(accumulator, SignExtendedOperand(address10));
                Memory[AgcMemory.A] = sum;
                WriteOperand(address10, address10 < 3 ? sum : AgcWord.OverflowCorrect(sum));
                break;
            }

            case >= 0x18 and <= 0x1f: // CA
                if (address12 != AgcMemory.A)
                    Memory[AgcMemory.A] = SignExtendedOperand(address12);
                break;

            case >= 0x20 and <= 0x27: // CS
                Memory[AgcMemory.A] = address12 < 3
                    ? ~ReadOperand(address12) & AgcWord.RegisterMask
                    : AgcWord.SignExtend(AgcWord.Negate(ReadOperandAndEdit(address12)));
                break;

            case 0x28 or 0x29: // INDEX
                if (address10 == AgcMemory.Brupt)
                    Resume();
                else
                    _index = address10 < 3 ? AgcWord.OverflowCorrect(ReadOperand(address10)) : ReadOperand(address10);
                break;

            case 0x2a or 0x2b: // DXCH
                ExecuteDxch(address10);
                break;

            case 0x2c or 0x2d: // TS
                ExecuteTs(address10, accumulator);
                break;

            case 0x2e or 0x2f: // XCH
                ExchangeRegister(AgcMemory.A, address10);
                break;

            case >= 0x30 and <= 0x37: // AD
                Memory[AgcMemory.A] = AgcWord.Add(accumulator, SignExtendedOperand(address12));
                break;

            case >= 0x38 and <= 0x3f: // MASK
                Memory[AgcMemory.A] = address12 < 3
                    ? accumulator & ReadOperand(address12)
                    : AgcWord.SignExtend(AgcWord.OverflowCorrect(accumulator) & ReadOperand(address12));
                break;

            case 0x40: // READ
                Memory[AgcMemory.A] = address9 is AgcMemory.L or AgcMemory.Q
                    ? Memory[address9]
                    : AgcWord.SignExtend(Channels.Read(address9, Memory));
                break;
            case 0x41: // WRITE
                if (address9 is AgcMemory.L or AgcMemory.Q)
                    Memory[address9] = accumulator;
                else
                    WriteChannel(address9, AgcWord.OverflowCorrect(accumulator));
                break;
            case 0x42: // RAND
                Memory[AgcMemory.A] = IoLogical(address9, accumulator, static (a, b) => a & b, false);
                break;
            case 0x43: // WAND
                Memory[AgcMemory.A] = IoLogical(address9, accumulator, static (a, b) => a & b, true);
                break;
            case 0x44: // ROR
                Memory[AgcMemory.A] = IoLogical(address9, accumulator, static (a, b) => a | b, false);
                break;
            case 0x45: // WOR
                Memory[AgcMemory.A] = IoLogical(address9, accumulator, static (a, b) => a | b, true);
                break;
            case 0x46: // RXOR
                Memory[AgcMemory.A] = IoLogical(address9, accumulator, static (a, b) => a ^ b, false);
                break;
            case 0x47: // EDRUPT is consumed by interrupt selection.
                break;

            case 0x48 or 0x49: // DV
                ExecuteDivide(address10);
                break;

            case >= 0x4a and <= 0x4f: // BZF
                if (accumulator is 0 or AgcWord.RegisterMask)
                {
                    _nextZ = address12;
                    WaitForCpuCycle();
                }
                break;

            case 0x50 or 0x51: // MSU
                ExecuteMsu(address10);
                break;

            case 0x52 or 0x53: // QXCH
                ExchangeRegister(AgcMemory.Q, address10);
                break;

            case 0x54 or 0x55: // AUG
                ExecuteAugment(address10);
                break;

            case 0x56 or 0x57: // DIM
                ExecuteDiminish(address10);
                break;

            case >= 0x58 and <= 0x5f: // DCA
                LoadDouble(address12, complement: false);
                break;

            case >= 0x60 and <= 0x67: // DCS
                LoadDouble(address12, complement: true);
                break;

            case >= 0x68 and <= 0x6f: // Extended INDEX or RESUME.
                if (address12 == (AgcMemory.Brupt << 1))
                    Resume();
                else
                {
                    _index = address12 < 3 ? AgcWord.OverflowCorrect(ReadOperand(address12)) : ReadOperand(address12);
                    keepExtraCode = true;
                }
                break;

            case 0x70 or 0x71: // SU
                Memory[AgcMemory.A] = address10 == AgcMemory.A
                    ? AgcWord.SignExtend(AgcWord.NegativeZero)
                    : AgcWord.Add(accumulator, SignExtendedComplement(address10));
                break;

            case >= 0x72 and <= 0x77: // BZMF
                if (accumulator == 0 || (accumulator & AgcWord.RegisterSignBit) != 0)
                {
                    _nextZ = address12;
                    WaitForCpuCycle();
                }
                break;

            case >= 0x78 and <= 0x7f: // MP
                ExecuteMultiply(address12);
                break;

            default:
                throw new InvalidOperationException($"Invalid AGC opcode {opcode:X}.");
        }
    }

    private bool CanTakeInterrupt(int instruction, bool overflow) =>
        !InInterrupt && InterruptsEnabled && !ExtraCode && !overflow && instruction is not 3 and not 4 and not 6;

    private bool TakeInterrupt(int programCounter, int instruction, int opcode)
    {
        var priority = 0;
        for (var i = 1; i <= 10; i++)
        {
            if (!_interruptRequests[i])
                continue;
            priority = i;
            _interruptRequests[i] = false;
            break;
        }

        if (priority == 0 && opcode != 0x47)
            return false;

        Memory[AgcMemory.Zrupt] = programCounter + 1;
        Memory[AgcMemory.Brupt] = instruction;
        Memory[AgcMemory.Z] = priority == 0 ? 0 : 0x800 + (4 * priority);
        _index = 0;
        ExtraCode = false;
        _substituteInstruction = false;
        InInterrupt = true;
        LastInterrupt = priority;
        return true;
    }

    private int ReadOperand(int address) => Memory.Read(address);

    private int ReadOperandAndEdit(int address)
    {
        var value = Memory.Read(address);
        if (address is >= AgcMemory.Cyr and <= AgcMemory.Edop)
            Memory.Write(address, value);
        return value;
    }

    private int SignExtendedOperand(int address) =>
        address < 3 ? ReadOperand(address) : AgcWord.SignExtend(ReadOperandAndEdit(address));

    private int SignExtendedComplement(int address) =>
        address < 3
            ? ~ReadOperand(address) & AgcWord.RegisterMask
            : AgcWord.SignExtend(AgcWord.Negate(ReadOperandAndEdit(address)));

    private void WriteOperand(int address, int value)
    {
        Memory.Write(address, value);
        if (address == AgcMemory.Z)
            _nextZ = value;
    }

    private void ExchangeRegister(int register, int address)
    {
        if (address == register)
            return;
        if (address == AgcMemory.Zero)
        {
            Memory[register] = 0;
            return;
        }

        var registerValue = Memory[register];
        var operand = ReadOperand(address);
        WriteOperand(address, address < 3 ? registerValue : AgcWord.OverflowCorrect(registerValue));
        Memory[register] = address < 3 ? operand : AgcWord.SignExtend(operand);
    }

    private void ExecuteTs(int address, int accumulator)
    {
        var overflow = AgcWord.Overflow(accumulator);
        if (address == AgcMemory.A)
        {
            if (overflow != 0)
                _nextZ++;
            return;
        }

        if (address == AgcMemory.Z)
            _nextZ = accumulator & AgcWord.WordMask;
        else
            WriteOperand(address, address < 3 ? accumulator : AgcWord.OverflowCorrect(accumulator));

        if (overflow != 0)
        {
            Memory[AgcMemory.A] = AgcWord.SignExtend(overflow);
            if (address != AgcMemory.Z)
                _nextZ++;
        }
    }

    private void ExecuteDxch(int address)
    {
        if (address == AgcMemory.L)
        {
            Memory[AgcMemory.L] = AgcWord.SignExtend(AgcWord.OverflowCorrect(Memory[AgcMemory.L]));
            return;
        }

        var high = ReadOperand(address);
        WriteOperand(address, address < 3 ? Memory[AgcMemory.L] : AgcWord.OverflowCorrect(Memory[AgcMemory.L]));
        Memory[AgcMemory.L] = address < 3 ? high : AgcWord.SignExtend(high);

        var lowAddress = (address - 1) & 0xfff;
        var low = ReadOperand(lowAddress);
        WriteOperand(lowAddress, lowAddress < 3 ? Memory[AgcMemory.A] : AgcWord.OverflowCorrect(Memory[AgcMemory.A]));
        Memory[AgcMemory.A] = lowAddress < 3 ? low : AgcWord.SignExtend(low);
    }

    private void ExecuteDas(int address)
    {
        if (address == AgcMemory.L)
        {
            var doubledLow = AgcWord.Add(Memory[AgcMemory.L], Memory[AgcMemory.L]);
            var doubledHigh = AgcWord.Add(Memory[AgcMemory.A], Memory[AgcMemory.A]);
            if ((doubledLow & 0xc000) == 0x4000)
                doubledHigh = AgcWord.Add(doubledHigh, 1);
            else if ((doubledLow & 0xc000) == 0x8000)
                doubledHigh = AgcWord.Add(doubledHigh, AgcWord.SignExtend(AgcWord.WordMask - 1));
            Memory[AgcMemory.A] = doubledHigh;
            Memory[AgcMemory.L] = AgcWord.SignExtend(AgcWord.OverflowCorrect(doubledLow));
            return;
        }

        var lowSum = AgcWord.Add(Memory[AgcMemory.L], SignExtendedOperand(address));
        var highSum = AgcWord.Add(Memory[AgcMemory.A], SignExtendedOperand((address - 1) & 0x3ff));
        if ((lowSum & 0xc000) == 0x4000)
            highSum = AgcWord.Add(highSum, 1);
        else if ((lowSum & 0xc000) == 0x8000)
            highSum = AgcWord.Add(highSum, AgcWord.SignExtend(AgcWord.WordMask - 1));

        WriteOperand(address, address < 3 ? lowSum : AgcWord.OverflowCorrect(lowSum));
        WriteOperand((address - 1) & 0x3ff, address <= 3 ? highSum : AgcWord.OverflowCorrect(highSum));
        Memory[AgcMemory.A] = AgcWord.SignExtend(AgcWord.Overflow(highSum));
        Memory[AgcMemory.L] = 0;
    }

    private int IoLogical(int channel, int accumulator, Func<int, int, int> operation, bool write)
    {
        var central = channel is AgcMemory.L or AgcMemory.Q;
        var left = central ? accumulator : AgcWord.OverflowCorrect(accumulator);
        var value = operation(left, central ? Memory[channel] : Channels.Read(channel, Memory));
        if (write)
        {
            if (central)
                Memory[channel] = value;
            else
                WriteChannel(channel, value);
        }
        return central ? value & AgcWord.RegisterMask : AgcWord.SignExtend(value);
    }

    private void ExecuteMsu(int address)
    {
        var left = address < 3 ? Memory[AgcMemory.A] : AgcWord.OverflowCorrect(Memory[AgcMemory.A]);
        var right = address is >= AgcMemory.Cyr and <= AgcMemory.Edop
            ? ReadOperandAndEdit(address)
            : ReadOperand(address);
        var difference = (left + (~right & (address < 3 ? AgcWord.RegisterMask : AgcWord.WordMask)) + 1) & AgcWord.RegisterMask;
        if ((difference & AgcWord.SignBit) != 0)
            difference = ((difference | AgcWord.RegisterSignBit) - 1) & AgcWord.RegisterMask;
        Memory[AgcMemory.A] = address == AgcMemory.Q ? difference : AgcWord.SignExtend(difference & AgcWord.WordMask);
    }

    private void ExecuteAugment(int address)
    {
        var value = SignExtendedOperand(address);
        var delta = (value & AgcWord.RegisterSignBit) == 0 ? 1 : AgcWord.SignExtend(AgcWord.WordMask - 1);
        var sum = AgcWord.Add(value, delta);
        WriteOperand(address, address < 3 ? sum : AgcWord.OverflowCorrect(sum));
        RequestTimerInterruptOnOverflow(address, sum);
    }

    private void ExecuteDiminish(int address)
    {
        var value = SignExtendedOperand(address);
        if (value is 0 or AgcWord.RegisterMask)
            return;
        var delta = (value & AgcWord.RegisterSignBit) == 0 ? AgcWord.SignExtend(AgcWord.WordMask - 1) : 1;
        var sum = AgcWord.Add(value, delta);
        WriteOperand(address, address < 3 ? sum : AgcWord.OverflowCorrect(sum));
    }

    private void LoadDouble(int address, bool complement)
    {
        if (address == AgcMemory.L)
        {
            if (complement)
            {
                Memory[AgcMemory.A] = ~Memory[AgcMemory.A] & AgcWord.RegisterMask;
                Memory[AgcMemory.L] = ~Memory[AgcMemory.L] & AgcWord.RegisterMask;
            }
            Memory[AgcMemory.L] = AgcWord.SignExtend(AgcWord.OverflowCorrect(Memory[AgcMemory.L]));
            return;
        }

        var low = SignExtendedOperand(address);
        var high = SignExtendedOperand((address - 1) & 0xfff);
        Memory[AgcMemory.L] = complement ? ~low & AgcWord.RegisterMask : low;
        Memory[AgcMemory.L] = AgcWord.SignExtend(AgcWord.OverflowCorrect(Memory[AgcMemory.L]));
        Memory[AgcMemory.A] = complement ? ~high & AgcWord.RegisterMask : high;
    }

    private void ExecuteMultiply(int address)
    {
        var leftWord = AgcWord.OverflowCorrect(Memory[AgcMemory.A]);
        var rightWord = address < 3 ? AgcWord.OverflowCorrect(ReadOperand(address)) : ReadOperandAndEdit(address);
        var left = AgcWord.ToInt32(leftWord);
        var right = AgcWord.ToInt32(rightWord);

        if (left == 0 || right == 0)
        {
            var negative = ((leftWord ^ rightWord) & AgcWord.SignBit) != 0;
            var zero = negative ? AgcWord.NegativeZero : 0;
            Memory[AgcMemory.A] = AgcWord.SignExtend(zero);
            Memory[AgcMemory.L] = AgcWord.SignExtend(zero);
            return;
        }

        FromDoubleLength((long)left * right, out var high, out var low);
        Memory[AgcMemory.A] = AgcWord.SignExtend(high);
        Memory[AgcMemory.L] = AgcWord.SignExtend(low);
    }

    private void ExecuteDivide(int address)
    {
        var divisorWord = address < 3 ? AgcWord.OverflowCorrect(ReadOperand(address)) : ReadOperandAndEdit(address);
        var divisor = AgcWord.ToInt32(divisorWord);
        var dividend = ToDoubleLength(AgcWord.OverflowCorrect(Memory[AgcMemory.A]), AgcWord.OverflowCorrect(Memory[AgcMemory.L]));
        if (divisor == 0 || Math.Abs(dividend) >= (long)Math.Abs(divisor) << 14)
        {
            // The hardware's illegal-division result is deliberately deterministic.
            Memory[AgcMemory.A] = AgcWord.SignExtend((dividend < 0) == (divisor < 0) ? 0x3fff : 0x4000);
            return;
        }

        var quotient = dividend / divisor;
        var remainder = dividend % divisor;
        Memory[AgcMemory.A] = AgcWord.SignExtend(AgcWord.FromInt32((int)quotient));
        Memory[AgcMemory.L] = AgcWord.SignExtend(AgcWord.FromInt32((int)remainder));
    }

    private void Resume()
    {
        _nextZ = (Memory[AgcMemory.Zrupt] - 1) & 0xffff;
        InInterrupt = false;
        _substituteInstruction = true;
    }

    private void RequestTimerInterruptOnOverflow(int address, int sum)
    {
        if (AgcWord.Overflow(sum) == 0)
            return;
        if (address == AgcMemory.Time1)
            IncrementCounter(AgcMemory.Time2, 0);
        else if (address == AgcMemory.Time5)
            RequestInterrupt(2);
        else if (address == AgcMemory.Time3)
            RequestInterrupt(3);
        else if (address == AgcMemory.Time4)
            RequestInterrupt(4);
    }

    private void WaitForCpuCycle()
    {
        while (TickMachineCycle())
        {
        }
    }

    private bool TickMachineCycle()
    {
        var counterOperations = 0;
        var alreadyDelayed = _counterDelay > 0;
        if (alreadyDelayed)
            _counterDelay--;

        CycleCount++;
        if (_downruptCycle >= 0 && CycleCount >= _downruptCycle)
        {
            RequestInterrupt(8);
            _downruptCycle = -1;
        }
        _scaler += 3;
        while (_scaler >= 80)
        {
            _scaler -= 80;
            var scaler1 = (Channels.Get(4) + 1) & 0x7fff;
            Channels.SetInput(4, scaler1);
            if (scaler1 == 0x4000)
            {
                Channels.SetInput(4, 0);
                Channels.SetInput(3, (Channels.Get(3) + 1) & 0x3fff);
                scaler1 = 0;
            }

            if ((scaler1 & 0x1f) == 0x10)
            {
                counterOperations += 2;
                if (IncrementCounter(AgcMemory.Time1, 0))
                {
                    counterOperations++;
                    IncrementCounter(AgcMemory.Time2, 0);
                }
                if (IncrementCounter(AgcMemory.Time3, 0))
                    RequestInterrupt(3);
            }
            if ((scaler1 & 0x1f) == 0)
            {
                counterOperations++;
                if (IncrementCounter(AgcMemory.Time5, 0))
                    RequestInterrupt(2);
            }
            if ((scaler1 & 0x1f) == 0x08)
            {
                counterOperations++;
                if (IncrementCounter(AgcMemory.Time4, 0))
                    RequestInterrupt(4);
            }
            if ((Channels.Get(0x0b) & 0x4000) != 0 && (scaler1 & 1) != 0)
            {
                counterOperations++;
                var time6 = (ushort)Memory.ReadErasable(0, AgcMemory.Time6);
                if (AgcWord.Diminish(ref time6))
                {
                    RequestInterrupt(1);
                    Channels.SetInput(0x0b, Channels.Get(0x0b) & 0x3fff);
                }
                Memory.WriteErasable(0, AgcMemory.Time6, time6);
            }

            if (_trap31A && (Channels.Get(0x19) & 0x3f) != 0x3f)
            {
                _trap31A = false;
                RequestInterrupt(10);
            }
            if (_trap31B && (Channels.Get(0x19) & 0x7c0) != 0x7c0)
            {
                _trap31B = false;
                RequestInterrupt(10);
            }
            if (_trap32 && (Channels.Get(0x1a) & 0x3ff) != 0x3ff)
            {
                _trap32 = false;
                RequestInterrupt(10);
            }
        }

        if (counterOperations > 0)
        {
            // Unprogrammed counter sequences pre-empt instruction sequencing,
            // one MCT per counter, while the scaler itself keeps running.
            _counterDelay += counterOperations - (alreadyDelayed ? 0 : 1);
            return true;
        }

        return alreadyDelayed;
    }

    private void WriteChannel(int channel, int value)
    {
        if (channel == 0x0b)
        {
            _trap31A |= (value & 0x0800) != 0;
            _trap31B |= (value & 0x1000) != 0;
            _trap32 |= (value & 0x2000) != 0;
            value &= 0x47ff;
        }
        else if (channel == 0x1b)
        {
            value = Channels.Get(channel) | 0x7c00;
        }
        else if (channel == 0x3f)
        {
            value = 0;
        }

        Channels.Write(channel, value, Memory);

        if (channel == 0x1c)
            _downlinkWords |= 1;
        else if (channel == 0x1d)
            _downlinkWords |= 2;
        if (_downlinkWords == 3)
        {
            _downruptCycle = CycleCount + (MachineCyclesPerSecond / 50);
            _downlinkWords = 0;
        }
    }

    private bool IncrementCounter(int address, int interrupt)
    {
        var counter = (ushort)Memory.ReadErasable(0, address);
        var overflow = AgcWord.Increment(ref counter);
        Memory.WriteErasable(0, address, counter);
        if (overflow && interrupt != 0)
            RequestInterrupt(interrupt);
        return overflow;
    }

    private static int DiminishedAbsolute(int value, bool register)
    {
        var sign = register ? AgcWord.RegisterSignBit : AgcWord.SignBit;
        var mask = register ? AgcWord.RegisterMask : AgcWord.WordMask;
        value &= mask;
        if ((value & sign) != 0)
            value = ~value & (sign - 1);
        return value > 1 ? value - 1 : 0;
    }

    private static long ToDoubleLength(int high, int low)
    {
        var highValue = AgcWord.ToInt32(high);
        var lowMagnitude = low & 0x3fff;
        return highValue < 0 || (highValue == 0 && (low & AgcWord.SignBit) != 0)
            ? ((long)highValue << 14) - lowMagnitude
            : ((long)highValue << 14) + lowMagnitude;
    }

    private static void FromDoubleLength(long value, out int high, out int low)
    {
        var negative = value < 0;
        var magnitude = Math.Abs(value);
        high = AgcWord.FromInt32((int)(magnitude >> 14));
        low = (int)(magnitude & 0x3fff);
        if (negative)
        {
            high = AgcWord.Negate(high);
            low = AgcWord.Negate(low);
        }
    }
}
