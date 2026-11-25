using Apollo11.Agc.Dsky;
using Apollo11.Agc.Hardware;

var ropePath = Path.Combine(AppContext.BaseDirectory, "roms", "Luminary099.bin");
var cycles = 0L;
var instructions = 0L;
var noDsky = false;

for (var index = 0; index < args.Length; index++)
{
    switch (args[index])
    {
        case "--rope" when index + 1 < args.Length:
            ropePath = args[++index];
            break;
        case "--cycles" when index + 1 < args.Length && long.TryParse(args[++index], out var requestedCycles):
            cycles = requestedCycles;
            break;
        case "--instructions" when index + 1 < args.Length && long.TryParse(args[++index], out var requestedInstructions):
            instructions = requestedInstructions;
            break;
        case "--nodsky":
            noDsky = true;
            break;
        case "--help":
            Console.WriteLine("Usage: Apollo11.Agc [--rope PATH] [--instructions COUNT] [--cycles COUNT] [--dsky]");
            return;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument: {args[index]}");
            Environment.ExitCode = 2;
            return;
    }
}

var cpu = new AgcCpu();
RopeImage.Load(ropePath, cpu.Memory);
if (noDsky)
{
    cpu.Channels.Output += (channel, value) =>
    {
        if (channel is 0x08 or 0x09)
            Console.WriteLine($"{cpu.CycleCount,10}  CH {Octal(channel, 3)} <- {Octal(value, 5)}");
    };
}

if (cycles > 0)
    cpu.Run(cycles);
for (var instruction = 0L; instruction < instructions; instruction++)
    _ = cpu.Step();

if (!noDsky)
{
    try
    {
        DskyWindow.Run(cpu);
    }
    catch (DllNotFoundException)
    {
        Console.Error.WriteLine("SDL 2.0.18 or newer is required for --dsky. Install the native SDL2 library for this platform.");
        Environment.ExitCode = 1;
    }
    catch (EntryPointNotFoundException)
    {
        Console.Error.WriteLine("The loaded SDL2 library is too old. Update to SDL 2.0.18 or newer.");
        Environment.ExitCode = 1;
    }
    catch (InvalidOperationException error)
    {
        Console.Error.WriteLine($"Cannot run the SDL DSKY: {error.Message}");
        Environment.ExitCode = 1;
    }
    return;
}

Console.WriteLine(
    $"Luminary 099 loaded; cycles={cpu.CycleCount}, instructions={cpu.InstructionCount}, " +
    $"Z={Octal(cpu.Z, 4)}, A={Octal(cpu.A, 6)}, L={Octal(cpu.L, 6)}, Q={Octal(cpu.Q, 6)}, " +
    $"EB={Octal(cpu.Memory[AgcMemory.Eb], 6)}, FB={Octal(cpu.Memory[AgcMemory.Fb], 6)}, " +
    $"BB={Octal(cpu.Memory[AgcMemory.Bb], 6)}");

static string Octal(int value, int width) => Convert.ToString(value, 8).PadLeft(width, '0');
