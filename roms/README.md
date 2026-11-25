# Luminary 099 rope

`Luminary099.bin` is assembled from the public-domain Apollo 11 Lunar Module
flight-software transcription in the
[Virtual AGC repository](https://github.com/virtualagc/virtualagc/tree/master/Luminary099).
It contains all 36 fixed-memory banks in yaYUL's simulator bank order
(`02`, `03`, `00`, `01`, `04` ... `43`). Each big-endian 16-bit value holds
one 15-bit AGC word shifted left by one; the low bit is the optional rope
parity bit.

The checked-in image was assembled with yaYUL from Virtual AGC revision
`83c6e53624d667bc315f5ad9644df4bde7b2908d` and has SHA-256:

```
b7a2abb432df5d4ce242c238e25fd2cf6711f72fb88b888a4b972afb3dea41c1
```

Run `scripts/build-luminary099.sh` to reproduce it from source.
The build enables yaYUL's odd-parity output so each stored rope word includes
the parity bit that the Block II fixed-memory interface checked in hardware.
