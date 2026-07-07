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

# The Mooneye suite is distributed as source; prebuilt ROMs live on gekkio.fi,
# which some CI networks block, so we build from source with wla-dx instead.
if [ ! -d "$data_dir/mooneye" ]; then
    echo "Building Mooneye test suite from source (needs cmake + make + cc)..."
    git clone --depth 1 https://github.com/vhelin/wla-dx.git "$tmp_dir/wla-dx"
    cmake -S "$tmp_dir/wla-dx" -B "$tmp_dir/wla-dx/build" -DCMAKE_BUILD_TYPE=Release >/dev/null
    cmake --build "$tmp_dir/wla-dx/build" --target wla-gb wlalink -j"$(nproc)" >/dev/null

    git clone --depth 1 https://github.com/Gekkio/mooneye-test-suite.git "$tmp_dir/mts"
    make -C "$tmp_dir/mts" -j"$(nproc)" \
        WLA="$tmp_dir/wla-dx/build/binaries/wla-gb" \
        WLALINK="$tmp_dir/wla-dx/build/binaries/wlalink" >/dev/null
    mkdir -p "$data_dir/mooneye"
    cp -r "$tmp_dir/mts/build/acceptance" "$data_dir/mooneye/acceptance"
    cp -r "$tmp_dir/mts/build/emulator-only" "$data_dir/mooneye/emulator-only"
else
    echo "Mooneye ROMs already present."
fi

# dmg-acid2 is built with RGBDS v0.5.2 (the source predates current syntax).
if [ ! -d "$data_dir/acid2" ]; then
    echo "Building dmg-acid2 from source (needs bison + libpng-dev + cc)..."
    git clone --branch v0.5.2 --depth 1 https://github.com/gbdev/rgbds.git "$tmp_dir/rgbds"
    make -C "$tmp_dir/rgbds" -j"$(nproc)" >/dev/null
    git clone --depth 1 --recurse-submodules https://github.com/mattcurrie/dmg-acid2.git "$tmp_dir/dmg-acid2"
    PATH="$tmp_dir/rgbds:$PATH" make -C "$tmp_dir/dmg-acid2" >/dev/null
    mkdir -p "$data_dir/acid2"
    cp "$tmp_dir/dmg-acid2/build/dmg-acid2.gb" "$data_dir/acid2/"
else
    echo "dmg-acid2 already present."
fi

echo "Done. Test data in $data_dir"
