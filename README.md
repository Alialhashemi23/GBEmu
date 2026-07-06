# GBEmu

A Game Boy (DMG) emulator written in C# / .NET 8.

**Status: Phase 1 complete** — the SM83 CPU core passes the full
[SingleStepTests sm83](https://github.com/SingleStepTests/sm83) suite
(500 opcodes × 1000 cases) and all 11 of Blargg's `cpu_instrs` test ROMs.
See [PLAN.md](PLAN.md) for the full build plan and roadmap.

## Layout

| Project | Purpose |
|---------|---------|
| `src/GBEmu.Core` | Emulation core — pure C#, no dependencies |
| `src/GBEmu.Cli` | Headless runner (currently: ROM header inspector) |
| `tests/GBEmu.Tests` | xUnit test suite |

A `GBEmu.Desktop` frontend (window, input, audio) arrives in Phase 3 when the PPU
produces its first pixels.

## Usage

```sh
dotnet run --project src/GBEmu.Cli -- path/to/rom.gb
```

Prints the cartridge header: title, mapper, ROM/RAM size, battery, checksums.

## Development

```sh
./scripts/fetch-test-data.sh   # one-time: Blargg ROMs + sm83 JSON suite (~160 MB)
dotnet build
dotnet test
```

Tests that depend on the fetched data are skipped (not failed) when it is
absent. CI fetches and caches the data, so the full suite always runs there.
Commercial ROMs are never committed to this repository (`*.gb`/`*.gbc` are
gitignored).
