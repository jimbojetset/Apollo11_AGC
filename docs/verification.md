# Verification

The first comparison point uses `yaAGC` built from the same pinned Virtual AGC
revision as yaYUL. Starting with clean erasable memory, both emulators reach
the following state immediately before `DXCH RSBBQ` in
`FRESH_START_AND_RESTART.agc`:

| Register | Octal value |
| --- | ---: |
| A | `000000` |
| L | `000000` |
| Q | `000000` |
| EB | `001400` |
| FB | `012000` |
| BB | `012003` |
| Z | `2673` |

This boundary is reached after eight completed instructions in this emulator.
The state is asserted by the `Luminary 099 rope` test, making it a permanent
early-boot differential check rather than a one-off observation.

Run the local checks with:

```sh
dotnet run --project tests/Apollo11.Agc.Tests
```

The DSKY regression sends `VERB 3 5 ENTR` through channel 15 and KEYRUPT.
It compares the channel 10 relay words from Luminary's lamp test with the
ordered sequence observed in yaAGC, then checks all 21 digits, the three
register signs, and the annunciators. The relay comparison covers the word
sequence. Full instruction and cycle-by-cycle comparison remains future work.

## Block II instruction validation

Virtual AGC's `Validation` rope reaches `PROG 77` with zero error prompts in
both this emulator and yaAGC. The local tests cover signed zero and the unit
quotient in `DV`, `MP` and `DV` reads of editing registers, and double-word
transfers through L and Q.
[Virtual AGC Validation source](https://github.com/virtualagc/virtualagc/tree/83c6e53624d667bc315f5ad9644df4bde7b2908d/Validation)

With yaYUL built from the revision pinned by `scripts/build-luminary099.sh`,
assemble `Validation.agc` from its source directory and run the replay from
the project root.

```sh
# In virtualagc/Validation
../yaYUL/yaYUL --parity --unpound-page Validation.agc

# In the C# project root, with the path to the assembled rope
dotnet run --project tests/Apollo11.Agc.Tests -- --block-ii-validation /path/to/virtualagc/Validation/Validation.agc.bin
```

The replay presses and releases PRO at each prompt. An error prompt or a
failure to reach completion causes a nonzero exit status.

## Comanche orbit check

This scenario accepts a yaYUL simulator rope for Comanche 051 (the initial
release) or 055 (flown on Apollo 11). The reference images were assembled from
the Virtual AGC revision pinned by `scripts/build-luminary099.sh`, using
`yaYUL --parity --unpound-page MAIN.agc` in the matching source directory.

```sh
dotnet run --project tests/Apollo11.Agc.Tests -- --comanche-orbit artifacts/Comanche051.bin
```

The check seeds the CSM position and velocity at perigee in erasable memory.
The input describes an Earth orbit with apogee and perigee 400 km and 200 km
above the stored launch-pad reference radius. `VERB 8 2 ENTR`, then `PRO`,
runs Colossus routine R30 on the CSM state. Both revisions give `V16 N44`
with `+02160` and `+01080` (216.0 and 108.0 nautical miles). The third
register shows `-59 59` because this orbit stays above the 300,000 ft
reference altitude.
[Comanche 051 R30 listing](https://www.ibiblio.org/apollo/listings/Comanche051/R30.agc.html)
· [Comanche 055 R30 listing](https://www.ibiblio.org/apollo/listings/Comanche055/R30.agc.html)

The browser Ground Uplink and the `Comanche 055 state vector via digital
uplink` regression exercise Program 27 before running R30. The ground preset
sends encoded characters at 0.2-second intervals. Each received word enters
`INLINK` and raises `UPRUPT`, following the Virtual AGC channel 173 convention.
Comanche verifies and writes the vector in P27. The regression checks the
stored words, the `UPLINK ACTY` transition, and the `V16 N44` readout. The
published web build and a yaAGC replay both displayed `+02161` and `+01080`
for the tested sequence. The readout varies with the time of `PRO`.
[Virtual AGC uplink](https://www.ibiblio.org/apollo/yaTelemetry.html)
· [Comanche Program 27](https://www.ibiblio.org/apollo/listings/Comanche055/UPDATE_PROGRAM.agc.html)

`DV BUF` at octal `00,3436` in `SQRTSUB` divides a double-precision number
whose high word is positive and whose low word is negative. The previous CPU
treated the low word as unsigned, adding 16,383 integer counts to this
dividend. The quotient and remainder were octal `014537` and `023606`.
Decoding both words as signed one's-complement gives `014536` and `015104`,
matching yaAGC. The interpreter uses the quotient and the remainder in its
square-root refinement, and the conic calculation uses that root to compute
the apsides. The core regression runs this divide and its sign-reversed case.
[Comanche 055 interpreter listing](https://www.ibiblio.org/apollo/listings/Comanche055/INTERPRETER.agc.html)
· [Time of Free Fall listing](https://www.ibiblio.org/apollo/listings/Comanche055/TIME_OF_FREE_FALL.agc.html)

| Altitude above pad | C# AGC before fix, 3.17 s | C# AGC and yaAGC, 3.17 s | C# AGC and yaAGC, 3.18 s | Two-body reference |
| --- | ---: | ---: | ---: | ---: |
| Apogee | 398,812 m | 400,120 m | 400,118 m | 400,000 m |
| Perigee | 199,994 m | 200,000 m | 200,006 m | 200,000 m |

In the original keypad runs, R30 began about 300 machine cycles earlier in
the C# emulator. That put its clock read just before a `TIME1` tick and
yaAGC's just after it. For the matched-epoch comparisons, `TIME1` was moved by
one centisecond before `PRO` in yaAGC for 3.17 s and in C# for 3.18 s. Both
emulators produced identical propagated position and velocity words and
identical apsides at each matched epoch. The replay checks the propagated
position, raw apsides and DSKY digits against those captured results. At
3.17 s, the propagated position is 33.5 m from the independent two-body
calculation.

## DSKY face

- MIT's configuration report identifies the same DSKY assembly (2003994-091)
  on Eagle and Intrepid. The faceplate proportions and keyboard spacing were
  checked against Intrepid's panel photograph and MIT's DSKY photographs.
  [MIT E-1142, table 2.4](https://www.ibiblio.org/apollo/Documents/E1142-61.pdf)
  · [LM-6 panel photograph](https://danielmarin.naukas.com/files/2019/07/LM6-co28-1024x1006.jpg)
  · [MIT R-700, fig. 2-5 and §3.10](https://www.ibiblio.org/apollo/Documents/R-700.pdf)
- The engraved letter and numeral outlines were redrawn from photographs of
  original hardware. The separate slanted digits and dark lettering over
  luminous captions were checked against an energized Sylvania flight spare.
  The display colors approximate the photographed hardware.
  [Smithsonian Block II DSKY](https://www.si.edu/object/nasm_A19720313000)
  · [Powered flight spare](https://www.youtube.com/watch?v=feRCZyLzAwA)
- Apollo 11 lamp legends and colors follow the LM and CM configurations.
  The annunciator regression drives `PROG`, `ALT`, and `VEL` through the same
  relay word on both panels. Eagle shows all three. Columbia shows `PROG`
  and has a continuous blank faceplate below its five rows of annunciators.
  [MIT alarm indicator drawing 1006387, sheet 3](https://www.ibiblio.org/apollo/SCDs/scd_1006387d.pdf#page=3)
  · [Smithsonian photo of Columbia’s instrument panel](https://commons.wikimedia.org/wiki/File:Apollo_11_Command_Module_Columbia_instrument_panel_(NASM-SI-98-16043).jpg)
  [Apollo News Reference](https://www.nasa.gov/wp-content/uploads/static/history/alsj/LM08_Guidance-Navigation-Control_ppGN1-48.pdf)
  · [Virtual AGC's DSKY documentation](https://www.ibiblio.org/apollo/yaDSKY.html)
