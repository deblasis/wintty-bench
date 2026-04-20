#!/usr/bin/env bash
# Regenerates fixtures/vtebench/{dense_cells,scrolling,unicode}.txt.
# Run from the repo root:
#   bash scripts/fixtures/make-vtebench-fixtures.sh
#
# dense_cells.txt: emulates alacritty/vtebench benchmarks/dense_cells/benchmark
#                  at hardcoded 120 columns x 32 rows. No tty needed.
# scrolling.txt:   'y\n' repeated 100001 times (100000 from setup + 1 from benchmark).
# unicode.txt:     benchmarks/unicode/symbols fetched verbatim at the pinned SHA,
#                  sha256 verified before writing.
#
# Output is byte-identical for the same VTEBENCH_SHA and geometry.
set -euo pipefail

VTEBENCH_SHA="ead80032e57dee2e75f0b51f2ea67528647d9944"
UNICODE_SYMBOLS_SHA256="<<<FILL IN TASK 3 STEP 2>>>"

COLS=120
LINES=32

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
out_dir="${repo_root}/fixtures/vtebench"
mkdir -p "$out_dir"

# --- dense_cells.txt ---
# Per cell: ESC [ 38 ; 5 ; FG ; 48 ; 5 ; BG ; 1 ; 3 ; 4 m CHAR
# FG = (line + column + offset) % 156 + 100
# BG = 255 - ((line + column + offset) % 156) + 100
python3 - "$out_dir/dense_cells.txt" "$COLS" "$LINES" <<'PY'
import sys
out_path, cols, lines = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
ESC = b"\x1b"
letters = b"ABCDEFGHIJKLMNOPQRSTUVWXYZ"
with open(out_path, "wb") as f:
    for offset, ch in enumerate(letters):
        f.write(ESC + b"[H")
        for line in range(1, lines + 1):
            for column in range(1, cols + 1):
                index = line + column + offset
                fg = (index % 156) + 100
                bg = 255 - (index % 156) + 100
                f.write(ESC + b"[38;5;" + str(fg).encode("ascii")
                        + b";48;5;" + str(bg).encode("ascii")
                        + b";1;3;4m" + bytes([ch]))
PY

# --- scrolling.txt ---
python3 - "$out_dir/scrolling.txt" <<'PY'
import sys
out_path = sys.argv[1]
with open(out_path, "wb") as f:
    f.write(b"y\n" * 100001)
PY

# --- unicode.txt ---
url="https://raw.githubusercontent.com/alacritty/vtebench/${VTEBENCH_SHA}/benchmarks/unicode/symbols"
tmp="${out_dir}/unicode.txt.tmp"
curl -fsSL "$url" -o "$tmp"
actual=$(sha256sum "$tmp" | awk '{print $1}')
if [ "$UNICODE_SYMBOLS_SHA256" != "<<<FILL IN TASK 3 STEP 2>>>" ] && [ "$actual" != "$UNICODE_SYMBOLS_SHA256" ]; then
    echo "unicode/symbols sha256 mismatch: expected $UNICODE_SYMBOLS_SHA256, got $actual" >&2
    rm -f "$tmp"
    exit 1
fi
mv "$tmp" "$out_dir/unicode.txt"

echo
echo "Wrote:"
for f in dense_cells.txt scrolling.txt unicode.txt; do
    size=$(wc -c < "$out_dir/$f")
    hash=$(sha256sum "$out_dir/$f" | awk '{print $1}')
    printf "  %-18s %10d bytes  sha256=%s\n" "$f" "$size" "$hash"
done
