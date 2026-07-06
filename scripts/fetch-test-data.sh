#!/usr/bin/env bash
# Fetches third-party test suites into tests/data/ (gitignored):
#   - Blargg's test ROMs (retrio/gb-test-roms mirror)
#   - SingleStepTests SM83 per-instruction JSON tests (~160 MB)
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
data_dir="$repo_root/tests/data"
mkdir -p "$data_dir"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

if [ ! -d "$data_dir/cpu_instrs" ]; then
    echo "Fetching Blargg test ROMs..."
    git clone --depth 1 https://github.com/retrio/gb-test-roms.git "$tmp_dir/gb-test-roms"
    cp -r "$tmp_dir/gb-test-roms/cpu_instrs" "$data_dir/cpu_instrs"
    cp -r "$tmp_dir/gb-test-roms/instr_timing" "$data_dir/instr_timing"
    cp -r "$tmp_dir/gb-test-roms/mem_timing" "$data_dir/mem_timing"
else
    echo "Blargg ROMs already present."
fi

if [ ! -d "$data_dir/sm83" ]; then
    echo "Fetching SingleStepTests sm83 suite (~160 MB)..."
    git clone --depth 1 https://github.com/SingleStepTests/sm83.git "$tmp_dir/sm83"
    mv "$tmp_dir/sm83/v1" "$data_dir/sm83"
else
    echo "sm83 tests already present."
fi

echo "Done. Test data in $data_dir"
