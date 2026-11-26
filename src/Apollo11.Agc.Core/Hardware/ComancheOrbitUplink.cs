using Apollo11.Agc.Dsky;

namespace Apollo11.Agc.Hardware;

public sealed class ComancheOrbitUplink(AgcCpu cpu, DskyPanel dsky)
{
    public const int TargetApogeeKilometres = 400;
    public const int TargetPerigeeKilometres = 200;

    private const int FlagWord7 = 0x43; // 0103 octal, Comanche PINBALL flags.
    private const int VerifyFlag = 0x04;
    private const int UplinkLock = 0x08;
    private const int LampChannel = 0x09; // 11 octal
    private const int UplinkActivity = 0x04;
    private const int OperatorError = 0x40;
    private const int CharacterInterval = AgcCpu.MachineCyclesPerSecond / 5;
    private const int ResponseTimeout = 3 * AgcCpu.MachineCyclesPerSecond;

    // V71 loads the documented 400 by 200 km test vector in octal. V33 verifies it.
    private const string KeySequence = "V71E"
        + "00021E01501E00001E00310E23215E00000E00000E00000E00000E00000E00000E"
        + "23471E35606E00000E00000E00000E00000E"
        + "V33E";

    private long _nextCharacter;
    private long _deadline;
    private int _verifyFlagAtStart;

    public int SentWords { get; private set; }
    public static int TotalWords => KeySequence.Length;
    public bool IsTransmitting { get; private set; }
    public bool IsComplete { get; private set; }
    public string? Error { get; private set; }
    public bool CanStart => !IsTransmitting && !IsComplete && IsP00
        && (cpu.Channels.Get(LampChannel) & (UplinkActivity | OperatorError)) == 0;

    private bool IsP00 => dsky.Spacecraft == DskySpacecraft.CommandModule
        && dsky.Digit(0) == '0' && dsky.Digit(1) == '0';

    public bool TryStart()
    {
        if (!CanStart)
            return false;

        SentWords = 0;
        Error = null;
        _verifyFlagAtStart = cpu.Memory[FlagWord7] & VerifyFlag;
        _nextCharacter = cpu.CycleCount;
        _deadline = cpu.CycleCount + ResponseTimeout;
        IsTransmitting = true;
        return true;
    }

    public void RunUntil(long targetCycle)
    {
        while (IsTransmitting && cpu.CycleCount < targetCycle)
        {
            if (cpu.CycleCount < _nextCharacter)
            {
                cpu.Run(Math.Min(targetCycle, _nextCharacter) - cpu.CycleCount);
                continue;
            }

            var flags = cpu.Memory[FlagWord7];
            var lamps = cpu.Channels.Get(LampChannel);
            if ((flags & UplinkLock) != 0 || (lamps & OperatorError) != 0)
            {
                Error = "Comanche rejected the uplink.";
                IsTransmitting = false;
            }
            else if (SentWords == TotalWords && (flags & VerifyFlag) != _verifyFlagAtStart &&
                     (lamps & UplinkActivity) == 0 && IsP00)
            {
                IsComplete = true;
                IsTransmitting = false;
            }
            else if (cpu.CycleCount >= _deadline)
            {
                Error = "Comanche did not acknowledge the uplink.";
                IsTransmitting = false;
            }
            else if (SentWords < TotalWords && cpu.TryReceiveUplinkWord(Encode(KeySequence[SentWords])))
            {
                SentWords++;
                _nextCharacter = cpu.CycleCount + CharacterInterval;
                _deadline = cpu.CycleCount + ResponseTimeout;
            }
            else
            {
                _nextCharacter = cpu.CycleCount + AgcCpu.MachineCyclesPerSecond / 100;
            }
        }

        if (cpu.CycleCount < targetCycle)
            cpu.Run(targetCycle - cpu.CycleCount);
    }

    private static int Encode(char key)
    {
        var code = key switch
        {
            '0' => (int)DskyKey.Zero,
            'V' => (int)DskyKey.Verb,
            'E' => (int)DskyKey.Enter,
            _ => key - '0'
        };

        // The receiver expects the key, its five-bit complement, and the key again.
        return (code << 10) | ((code ^ 0x1f) << 5) | code;
    }
}
