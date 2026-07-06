# GBEmu

A Game Boy (DMG) emulator written in C# / .NET 8.

**Status: Phase 0** — project skeleton, cartridge header parsing, CLI ROM inspector.
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
dotnet build
dotnet test
```

CI runs build + tests on every push. Commercial ROMs are never committed to
this repository (`*.gb`/`*.gbc` are gitignored).
