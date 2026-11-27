# Apollo 11 Block II AGC

A .NET 10 emulator for the Apollo 11 Block II Apollo Guidance Computer and
DSKY. It runs Eagle's Luminary 099 and Columbia's Comanche flight software
with a real-time SDL2 panel for the displays, annunciators, and keypad.

The core models 15-bit one's-complement memory, the 16-bit A/L/Q central
registers, erasable and fixed bank selection, the editing registers, the
Block II basic and extracode instruction set, machine-cycle timing, scaler
driven TIME counters, priority interrupts, and 9-bit I/O channels.

```sh
dotnet build Apollo11.Agc.slnx
dotnet run --project src/Apollo11.Agc
```

The window uses SDL 2.0.18 or newer. On macOS, `brew install sdl2` provides
it. On Linux, install your distribution's SDL2 runtime. On Windows, place the
matching architecture's `SDL2.dll` next to the executable.

Click the keys or use the keyboard. The number row and numeric keypad both work.
Use `V` for VERB, `N` for NOUN, `C` or Backspace for CLR, `K` for KEY REL,
`P` for PRO, `R` for RSET, and `Enter` or `E` for ENTR. Use `+` and `-` for
signs. Holding PRO keeps its discrete input asserted. Escape closes the panel.

Enter `VERB 3 5 ENTR` to run Luminary's lamp test and see the register segments
and annunciators. The panel starts with Luminary's cold-start state and changes
as the flight software writes to the AGC channels.

![Apollo 11 DSKY during Luminary 099's lamp test, with the annunciators lit and eights in the displays](dsky-lamp-test.png)

## Run in a browser

[Open the Apollo 11 DSKY on GitHub Pages](https://jimbojetset.github.io/Apollo11_AGC/).

The `Web/` project runs Luminary 099 and Comanche 055 in Blazor WebAssembly.
It shares the AGC core and both spacecraft's panel layouts with the SDL app.
Click or tap the keys or use the same keyboard shortcuts. The emulated clock
pauses while the tab is hidden.

```sh
dotnet workload install wasm-tools --source https://api.nuget.org/v3/index.json
dotnet run --project Web/Apollo11.Agc.Web.csproj
```

Open the local URL printed by the development server. The browser starts
with Comanche 055. Use the selector above the panel to switch to Luminary
099. Changing ropes starts a fresh AGC session and clears the displays and
keypad state. The LM panel shows `ALT` and `VEL` in the lower-right lamp
positions. The CM panel has a smooth blank area below its five rows of
annunciators. `VERB 3 5 ENTR` runs the lamp test.

With **Comanche 055**, use **Systems → Ground Uplink** to send the Earth
orbit test vector. On the DSKY, press `RSET`, then enter `VERB 37 ENTR` and
`00 ENTR` to put Comanche in P00. Click **Send state vector** and wait for
**Complete**. Comanche runs Program 27 and drives `UPLINK ACTY` during the
transfer, which takes about 22 seconds. Then enter `VERB 82 ENTR` and press
and release `PRO` to display `V16 N44`. Ground Uplink then compares the DSKY
heights with the kilometre targets and explains the third register. Expect
about 216 nmi apogee and 108 nmi perigee. The last digit can vary with the
AGC clock. To send the preset again after a successful transfer, start a
fresh Comanche session by reloading the page or switching ropes.

The **I/O Monitor** shows the uplink and DSKY channel words. **Systems →
Mission Control Telemetry** reconstructs an Apollo digital-to-television
console with fixed labels and live numeric readouts. The display updates on
complete AGC downlists, about every two seconds at the emulated rate. It shows
the AGC clock, the onboard state vector and its epoch, and the failure register.
Comanche’s Coast and Align list adds the last R30 apogee and perigee above the
AGC reference radius. Luminary’s Descent and Ascent list adds landing-radar
slant range and the altitude residual when those fields are in use.

**Raw downlink** shows the latest octal channel words and the complete list
with pair numbers for reference. The console keeps the last complete list on
screen with **Hold** if the stream stops. Switching views or hiding Systems
leaves the current transfer running. Changing flight software starts a fresh
session. The browser pauses the emulator and transfer while its tab is hidden.

The display style follows NASA’s [Apollo real-time display system report](https://ntrs.nasa.gov/api/citations/19760024152/downloads/19760024152.pdf).
Field order and units come from the [CMC Data Links manual](https://ntrs.nasa.gov/api/citations/19720025987/downloads/19720025987.pdf#page=115),
the [Luminary 099 flight listing](https://www.ibiblio.org/apollo/listings/Luminary099/DOWNLINK_LISTS.agc.html),
and [Virtual AGC’s downlist reference](https://www.ibiblio.org/apollo/yaTelemetry2.html).

```sh
dotnet publish Web/Apollo11.Agc.Web.csproj -c Release
```

The published site is in `Web/bin/Release/net10.0/publish/wwwroot`. The GitHub
Pages workflow builds and uploads it when the repository's Pages source is
set to **GitHub Actions**. `Apollo11.Agc.slnx` includes the web project for
editor tooling and builds the desktop app and its tests by default. Use the
web project commands above to build or publish the browser app.

## Calculate an orbit on the DSKY

Start a fresh Command Module session with Comanche 051 or Comanche 055.
From the project folder, launch the SDL panel with the Comanche rope:

```sh
dotnet run --project src/Apollo11.Agc -- --rope artifacts/Comanche051.bin
```

If you need the rope, run `scripts/build-luminary099.sh` to prepare the pinned
Virtual AGC checkout and yaYUL, then assemble Comanche from that checkout:

```sh
(cd artifacts/virtualagc/Comanche051 && ../yaYUL/yaYUL --parity --unpound-page MAIN.agc)
cp artifacts/virtualagc/Comanche051/MAIN.agc.bin artifacts/Comanche051.bin
```

Replace `Comanche051` with `Comanche055` in these examples to try the version
flown on Apollo 11. SDL selects the CM panel for filenames starting with
`Comanche`. Use `--dsky-panel cm` or `--dsky-panel lm` to choose the panel
explicitly when loading a rope with a different filename.

### Load the test state vector

| DSKY keys | Expected response |
| --- | --- |
| `RSET` | Clears the startup RESTART and PROG lamps. |
| `VERB 37 ENTR`, then `00 ENTR` | The PROG display shows `00`. |
| `VERB 71 ENTR` | PROG changes to `27` and flashing `V21 N01` requests the first word. |

Enter the following octal words in order. Press **ENTR after each five-digit
word**. `V21 N01` requests each successive word. After the final word, wait
for flashing `V21 N02`.

| Words to enter | Meaning |
| --- | --- |
| `00021` | Octal component count |
| `01501` | Starting erasable address |
| `00001` | Earth-centred Command Module state vector |
| `00310` `23215` | X position |
| `00000` `00000` | Y position |
| `00000` `00000` | Z position |
| `00000` `00000` | X velocity |
| `23471` `35606` | Y velocity |
| `00000` `00000` | Z velocity |
| `00000` `00000` | State-vector epoch at AGC clock zero |

At `V21 N02`, key `VERB 33 ENTR` to store the vector. PROG returns to `00`.
[Colossus Program 27 listing](https://www.ibiblio.org/apollo/listings/Comanche055/UPDATE_PROGRAM.agc.html)

### Display apogee and perigee

| DSKY keys | Expected response |
| --- | --- |
| `VERB 82 ENTR` | Flashing `V04 N12`. R1 shows `00002` and R2 shows `00001`, which selects the Command Module. |
| Hold `PRO` for about a quarter-second, then release | After a few seconds, `V16 N44` displays the orbit results. |
| `VERB 34 ENTR` | Ends the monitor and returns to the idle P00 display. |

The screenshot shows R1 `+02159` (**215.9 nautical miles apogee**) and R2
`+01081` (**108.1 nautical miles perigee**). R3 `−59 59` indicates that this
orbit never crosses the 300,000-foot reference altitude. This capture predates
the Columbia panel layout.
[Colossus R30 listing](https://www.ibiblio.org/apollo/listings/Comanche055/R30.agc.html)

![Comanche DSKY showing V16 N44, an apogee of 215.9 nautical miles, a perigee of 108.1 nautical miles, and −59 59 in R3](docs/dsky-orbit-result.png)

The last digits can vary because Colossus propagates the vector from clock zero
to the time you press `PRO`. To bring the calculation back near the entered
epoch, key `VERB 25 NOUN 36 ENTR` after storing the vector, then enter
`+00000 ENTR` three times for hours, minutes, and seconds. Follow the flashing
`V21 N36`, `V22 N36`, and `V23 N36` prompts, then run `VERB 82` and `PRO`.
[Colossus noun table](https://www.ibiblio.org/apollo/listings/Comanche055/PINBALL_NOUN_TABLES.agc.html)

## Headless use

For headless use, the cycle rate is 85,333⅓ machine cycles per second. Channel 10
relay words and channel 11 lamp words are printed in octal.

```sh
dotnet run --project src/Apollo11.Agc -- --nodsky --cycles 85333
dotnet run --project src/Apollo11.Agc -- --nodsky --instructions 1000
```

Rebuild the Luminary rope from the pinned Virtual AGC source with:

```sh
scripts/build-luminary099.sh
```
