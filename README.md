# Apollo 11 Block II AGC

A .NET 10 emulator for the Apollo 11 Lunar Module's Block II Apollo Guidance
Computer. The current implementation covers the hardware core and loads the
Luminary 099 flight rope. SDL DSKY rendering and Virtual AGC differential
tracing are the next stages.

The core models 15-bit one's-complement memory, the 16-bit A/L/Q central
registers, erasable and fixed bank selection, the editing registers, the
Block II basic and extracode instruction set, machine-cycle timing, scaler
driven TIME counters, priority interrupts, and 9-bit I/O channels.

```sh
dotnet build Apollo11.Agc.slnx
dotnet run --project src/Apollo11.Agc -- --cycles 85333
dotnet run --project src/Apollo11.Agc -- --instructions 1000
```

The cycle rate is 85,333⅓ machine cycles per second. Channel 10 and 11 output
is printed in octal; channel 10 is the relay interface that will drive the
DSKY display in stage 3.

Rebuild the Luminary rope from the pinned Virtual AGC source with:

```sh
scripts/build-luminary099.sh
```
