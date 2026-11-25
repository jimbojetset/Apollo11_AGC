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
- Apollo 11 lamp legends and colors follow the LM configuration.
  [Apollo News Reference](https://www.nasa.gov/wp-content/uploads/static/history/alsj/LM08_Guidance-Navigation-Control_ppGN1-48.pdf)
  · [Virtual AGC's DSKY documentation](https://www.ibiblio.org/apollo/yaDSKY.html)
