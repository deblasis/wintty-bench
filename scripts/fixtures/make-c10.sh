#!/usr/bin/env bash
# Generates a C10 fixture by repeating vtebench dense_cells.txt.
# Usage: make-c10.sh <size-in-bytes>
# Output: $HOME/.cache/wintty-bench/c10-<size>.bin (truncated to exact size)
set -euo pipefail

if [ $# -ne 1 ]; then
    echo "Usage: $0 <size-in-bytes>" >&2
    exit 2
fi

size="$1"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source_fixture="${script_dir}/../../fixtures/vtebench/dense_cells.txt"

if [ ! -f "$source_fixture" ]; then
    echo "Source fixture not found: $source_fixture" >&2
    exit 1
fi

out_dir="$HOME/.cache/wintty-bench"
mkdir -p "$out_dir"
out="$out_dir/c10-${size}.bin"
tmp="${out}.tmp"

source_size=$(stat -c '%s' "$source_fixture")
if [ "$source_size" -eq 0 ]; then
    echo "Source fixture is empty" >&2
    exit 1
fi

# Repeat the source enough times, then truncate to exact size.
repeats=$(( (size + source_size - 1) / source_size ))
: > "$tmp"
for (( i=0; i<repeats; i++ )); do
    cat "$source_fixture" >> "$tmp"
done
truncate -s "$size" "$tmp"
mv "$tmp" "$out"
