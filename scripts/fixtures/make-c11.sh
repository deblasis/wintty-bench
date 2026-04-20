#!/usr/bin/env bash
# Generates a C11 fixture: seeded PRNG bytes restricted to a safe VT grammar.
# Usage: make-c11.sh <size-in-bytes>
# Output: $HOME/.cache/wintty-bench/c11-<size>.bin (truncated to exact size)
#
# Grammar (weights approximate; generator accepts realized byte count after truncation):
#   ~80% printable ASCII 0x20..0x7E
#   ~10% newline 0x0A
#   ~5%  tab 0x09
#   ~3%  SGR   ESC [ N m   with N in {0,1,22,31,32,33,34,35,36,37,39}
#   ~2%  CUP   ESC [ R ; C H   with R,C in {1..24}
#
# Seed = 0x57494E54 (ASCII "WINT") = 1464287828 decimal.
#
# Python3 is used for determinism: random.Random(seed) is documented stable
# across CPython versions and platforms (Mersenne Twister, part of the
# stable API). Earlier versions of this script used awk, but awk PRNG
# behavior differs across implementations (mawk vs gawk vs bwk) so the
# "byte-identical across machines" guarantee was only true by accident.
set -euo pipefail

if [ $# -ne 1 ]; then
    echo "Usage: $0 <size-in-bytes>" >&2
    exit 2
fi

size="$1"
out_dir="$HOME/.cache/wintty-bench"
mkdir -p "$out_dir"
out="$out_dir/c11-${size}.bin"
tmp="${out}.tmp"

python3 - "$size" "$tmp" <<'PY'
import random
import sys

size = int(sys.argv[1])
out_path = sys.argv[2]

SEED = 0x57494E54  # ASCII "WINT"
rng = random.Random(SEED)

SGR_VALS = [0, 1, 22, 31, 32, 33, 34, 35, 36, 37, 39]
ESC = b"\x1b"

def next_token():
    r = rng.random()
    if r < 0.80:
        return bytes([0x20 + rng.randrange(95)])
    if r < 0.90:
        return b"\n"
    if r < 0.95:
        return b"\t"
    if r < 0.98:
        n = SGR_VALS[rng.randrange(len(SGR_VALS))]
        return ESC + b"[" + str(n).encode("ascii") + b"m"
    row = 1 + rng.randrange(24)
    col = 1 + rng.randrange(24)
    return ESC + b"[" + str(row).encode("ascii") + b";" + str(col).encode("ascii") + b"H"

produced = 0
with open(out_path, "wb") as f:
    while produced < size:
        tok = next_token()
        remaining = size - produced
        if len(tok) > remaining:
            tok = tok[:remaining]
        f.write(tok)
        produced += len(tok)
PY

# Safety: ensure exact target size (truncate is a no-op at the right size).
truncate -s "$size" "$tmp"
mv "$tmp" "$out"
