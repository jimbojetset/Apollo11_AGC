#!/bin/sh
set -eu

revision=83c6e53624d667bc315f5ad9644df4bde7b2908d
project_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
checkout=${VIRTUALAGC_CHECKOUT:-"$project_root/artifacts/virtualagc"}

if [ ! -d "$checkout/.git" ]; then
    git clone https://github.com/virtualagc/virtualagc.git "$checkout"
fi

if ! git -C "$checkout" cat-file -e "$revision^{commit}" 2>/dev/null; then
    git -C "$checkout" fetch origin "$revision"
fi
git -C "$checkout" checkout --detach "$revision"
make -C "$checkout/yaYUL" clean cc="${CC:-cc}"
make -C "$checkout/yaYUL" cc="${CC:-cc}" NATIVE=yes 'NVER=\"Apollo11.Agc\"'

(
    cd "$checkout/Luminary099"
    ../yaYUL/yaYUL --parity --unpound-page MAIN.agc >Luminary099.lst
)

actual=$(shasum -a 256 "$checkout/Luminary099/MAIN.agc.bin" | awk '{print $1}')
expected=b7a2abb432df5d4ce242c238e25fd2cf6711f72fb88b888a4b972afb3dea41c1
if [ "$actual" != "$expected" ]; then
    echo "Luminary 099 rope checksum mismatch: $actual" >&2
    exit 1
fi

cp "$checkout/Luminary099/MAIN.agc.bin" "$project_root/roms/Luminary099.bin"
echo "Built roms/Luminary099.bin ($actual)"
