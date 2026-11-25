using Apollo11.Agc.Hardware;

var ropePath = Path.Combine(AppContext.BaseDirectory, "roms", "Luminary099.bin");
var cycles = 0L;
var instructions = 0L;

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
        case "--help":
            Console.WriteLine("Usage: Apollo11.Agc [--rope PATH] [--instructions COUNT] [--cycles COUNT]");
            return;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument: {args[index]}");
            Environment.ExitCode = 2;
            return;
    }
}

var cpu = new AgcCpu();
RopeImage.Load(ropePath, cpu.Memory);
cpu.Channels.Output += (channel, value) =>
{
    if (channel is 0x08 or 0x09)
        Console.WriteLine($"{cpu.CycleCount,10}  CH {Convert.ToString(channel, 8).PadLeft(3, '0')} <- {Convert.ToString(value, 8).PadLeft(5, '0')}");
};

if (cycles > 0)
    cpu.Run(cycles);
for (var instruction = 0L; instruction < instructions; instruction++)
    cpu.Step();

Console.WriteLine(
    $"Luminary 099 loaded; cycles={cpu.CycleCount}, instructions={cpu.InstructionCount}, " +
    $"Z={Octal(cpu.Z, 4)}, A={Octal(cpu.A, 6)}, L={Octal(cpu.L, 6)}, Q={Octal(cpu.Q, 6)}, " +
    $"EB={Octal(cpu.Memory[AgcMemory.Eb], 6)}, FB={Octal(cpu.Memory[AgcMemory.Fb], 6)}, " +
    $"BB={Octal(cpu.Memory[AgcMemory.Bb], 6)}");

static string Octal(int value, int width) => Convert.ToString(value, 8).PadLeft(width, '0');
