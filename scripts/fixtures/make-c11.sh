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

awk -v size="$size" -v seed=1464287828 '
BEGIN {
    srand(seed);
    sgr_vals[0] = 0; sgr_vals[1] = 1; sgr_vals[2] = 22;
    for (i = 31; i <= 37; i++) sgr_vals[i - 28] = i;
    sgr_vals[10] = 39;
    sgr_count = 11;
    ESC = sprintf("%c", 27);
    produced = 0;
    while (produced < size) {
        r = rand();
        if      (r < 0.80) { ch = sprintf("%c", 32 + int(rand() * 95));          bytes = 1 }
        else if (r < 0.90) { ch = sprintf("%c", 10);                             bytes = 1 }
        else if (r < 0.95) { ch = sprintf("%c", 9);                              bytes = 1 }
        else if (r < 0.98) {
            n = sgr_vals[int(rand() * sgr_count)];
            ch = ESC "[" n "m";
            bytes = length(ch);
        }
        else {
            row = 1 + int(rand() * 24);
            col = 1 + int(rand() * 24);
            ch = ESC "[" row ";" col "H";
            bytes = length(ch);
        }
        if (produced + bytes > size) bytes = size - produced;
        printf("%s", substr(ch, 1, bytes));
        produced += bytes;
    }
}' > "$tmp"

truncate -s "$size" "$tmp"
mv "$tmp" "$out"
