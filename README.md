# GBEmu

A Game Boy (DMG) emulator written in C# / .NET 8.

**Status: Phases 3 + 5 complete** (Phase 4, battery saves, is next) — plays
real games with sound. The PPU renders
[dmg-acid2](https://github.com/mattcurrie/dmg-acid2) **pixel-perfectly**, the
APU passes **all 12** Blargg `dmg_sound` tests, and the MonoGame frontend
streams audio with audio-driven pacing. Also passing: the
[SingleStepTests sm83](https://github.com/SingleStepTests/sm83) suite
(500 opcodes × 1000 cases), Blargg `cpu_instrs`/`instr_timing`/`mem_timing`,
and the Mooneye timer, OAM DMA, and MBC1/2/5 suites (12/13 timer tests;
`rapid_toggle` needs fetch/execute overlap — deferred to the Phase 6 accuracy
pass). See [PLAN.md](PLAN.md) for the roadmap.

## Layout

| Project | Purpose |
|---------|---------|
| `src/GBEmu.Core` | Emulation core — pure C#, no dependencies |
| `src/GBEmu.Cli` | Headless runner: header dump, serial runner, screenshots |
| `src/GBEmu.Desktop` | MonoGame frontend — window, keyboard input |
| `tests/GBEmu.Tests` | xUnit test suite |

## Usage

```sh
# Play a ROM (arrows, Z=A, X=B, Enter=Start, RShift=Select, Esc=quit)
dotnet run --project src/GBEmu.Desktop -- path/to/rom.gb

# Inspect a cartridge header
dotnet run --project src/GBEmu.Cli -- path/to/rom.gb

# Run headless (Blargg-style serial output) / capture a screenshot
dotnet run --project src/GBEmu.Cli -- path/to/rom.gb --run
dotnet run --project src/GBEmu.Cli -- path/to/rom.gb --screenshot out.png --frames 300
```

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
