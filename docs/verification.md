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

Later stages should extend the comparison to instruction traces, channel 10
relay words, channel 11 lamp words, and keypad-generated KEYRUPT behaviour.
