# GBEmu — Game Boy Emulator in C#: Build Plan

A phased plan for building an original Game Boy (DMG) emulator in C# on .NET 8+.
The guiding principle: **get a feedback loop early** (CPU test ROMs, then visible
graphics), and keep the emulation core free of any UI/framework dependencies so
it can be tested headlessly.

---

## 1. Goals & Scope

**Target hardware:** the original DMG Game Boy first. Game Boy Color (CGB)
support is a stretch goal — the architecture should not preclude it, but no CGB
work happens until DMG is solid.

**Definition of done (v1):**
- Passes Blargg's `cpu_instrs` and `instr_timing` test ROMs.
- Passes the acid2 PPU test (`dmg-acid2`).
- Boots and plays real games correctly: Tetris, Dr. Mario, Super Mario Land,
  Link's Awakening, Pokémon Red/Blue.
- Audio output for all 4 channels.
- Save files (battery-backed cartridge RAM) persist to disk.

**Out of scope for v1:** Game Boy Color, Super Game Boy features, link cable,
save states (nice-to-have), debugger UI (a simple trace log is enough).

---

## 2. Tech Stack

| Concern        | Choice                                    | Rationale |
|----------------|-------------------------------------------|-----------|
| Runtime        | .NET 8 (LTS)                              | Spans, performance, cross-platform |
| Core library   | `GBEmu.Core` — pure C#, zero dependencies | Headless testing, UI-agnostic |
| Frontend       | MonoGame **or** Silk.NET + OpenGL         | Simple texture blit + input + audio callback is all we need |
| Audio          | Frontend's audio API (ring buffer)        | Core produces raw samples |
| Tests          | xUnit + test ROMs as fixtures             | Blargg/Mooneye ROMs report pass/fail in serial output or memory |

Frontend recommendation: **MonoGame** — one dependency gives us window, keyboard,
gamepad, texture rendering, and `DynamicSoundEffectInstance` for streaming audio.

---

## 3. Project Structure

```
GBEmu.sln
├── src/
│   ├── GBEmu.Core/          # Emulation core (no dependencies)
│   │   ├── Cpu/             # SM83 CPU: registers, decoder, instructions, interrupts
│   │   ├── Memory/          # Bus, memory map, DMA
│   │   ├── Cartridge/       # ROM loading, MBC1/2/3/5 mappers, battery saves
│   │   ├── Graphics/        # PPU: modes, fetcher, sprites, LCD registers
│   │   ├── Audio/           # APU: 4 channels, frame sequencer, mixer
│   │   ├── Timer.cs         # DIV/TIMA/TMA/TAC
│   │   ├── Joypad.cs
│   │   └── GameBoy.cs       # Top-level: wires components, RunFrame()
│   ├── GBEmu.Desktop/       # MonoGame frontend
│   └── GBEmu.Cli/           # Headless runner (test ROMs, benchmarks, trace logs)
└── tests/
    └── GBEmu.Tests/         # Unit tests + test-ROM harness
```

**Key architectural decisions:**

- **The bus is the hub.** Every component (CPU, PPU, APU, timer, joypad,
  cartridge) attaches to an `IBus` / `MemoryBus`. The CPU never touches other
  components directly — it reads/writes addresses.
- **Cycle stepping:** the CPU is the clock master. Each instruction reports its
  M-cycles; after each instruction the machine "ticks" the PPU, APU, timer, and
  DMA by that many T-cycles (4 T-cycles = 1 M-cycle). Start with
  instruction-level granularity; refine to per-memory-access ticking later if
  test ROMs demand it.
- **Core produces, frontend consumes.** The core exposes a 160×144 framebuffer
  (`uint[]` ARGB) and an audio sample callback. The frontend polls input into a
  joypad state struct. No core code knows about MonoGame.

---

## 4. Build Phases

### Phase 0 — Skeleton (half a day) — ✅ done
- Solution + projects + CI (GitHub Actions: build + test on push).
- Cartridge header parsing: title, MBC type, ROM/RAM size, checksum.
- CLI that loads a ROM and dumps its header. *Milestone: `dotnet run -- rom.gb` prints header info.*

### Phase 1 — CPU (the big one, ~1–2 weeks) — ✅ done
The SM83 core: 8 registers (AF/BC/DE/HL as pairs), SP, PC, flags Z/N/H/C.

- Implement all 245 base opcodes + 256 CB-prefixed opcodes. Generate or
  table-drive the decoder rather than a 500-case switch where possible —
  opcodes decode cleanly into octal-ish patterns (see "decoding tables" in the
  Pan Docs community).
- Half-carry flag logic deserves dedicated helper functions and unit tests —
  it's the #1 source of subtle bugs (especially `DAA`).
- Interrupt handling: IME, `EI` delay (one instruction), `HALT` (including the
  HALT bug), IF/IE, the 5 interrupt vectors with correct priority.
- **Testing strategy (do this in parallel with implementation):**
  1. Single-instruction JSON tests (sm83 test suite: initial state → expected
     state) wired into xUnit — catches flag bugs instantly.
  2. Blargg `cpu_instrs` sub-ROMs — results written to serial port (0xFF01/02),
     so we only need CPU + RAM + serial stub to run them headless.
  3. Optional: trace-log diff against a known-good emulator for one frame of
     Tetris when something is mysteriously wrong.

*Milestone: all 11 `cpu_instrs` sub-tests pass headlessly in CI.*

### Phase 2 — Memory, Timer, Interrupt plumbing (2–3 days) — ✅ done
- Full memory map: ROM banks, VRAM, WRAM, echo RAM, OAM, I/O registers, HRAM.
- MBC1 first (Tetris is ROM-only, Mario Land is MBC1). MBC3 (+RTC stub) and
  MBC5 later; MBC2 last.
- Timer: DIV (16384 Hz), TIMA/TMA/TAC with correct overflow-and-reload timing.
- Boot behavior: skip the boot ROM; initialize registers/IO to post-boot DMG
  values (documented in Pan Docs). Optional boot ROM support later.

*Milestone: Blargg `instr_timing` passes; Mooneye timer tests mostly pass.*

### Phase 3 — PPU + first pixels (~1 week) — ✅ done (dmg-acid2 pixel-perfect)
This is where it becomes fun.

- Mode state machine: OAM scan (2) → drawing (3) → HBlank (0) → VBlank (1),
  456 dots/line, 154 lines, correct STAT interrupts and LY/LYC.
- Start with a **scanline renderer** (render the whole line at HBlank):
  background, window, then sprites with priority rules and the 10-sprite limit.
  A pixel-FIFO fetcher is a later refinement only if we chase edge-case tests.
- OAM DMA transfer (0xFF46).
- Wire up the MonoGame frontend now: blit framebuffer to a scaled texture,
  map keyboard → joypad.

*Milestone: Tetris title screen renders. Then: Tetris is playable. Then: `dmg-acid2` passes.*

### Phase 4 — Joypad & real games (1–2 days)
- Joypad register (0xFF00) matrix selection + joypad interrupt.
- Battery saves: flush cartridge RAM to `<rom>.sav` on exit and periodically.

*Milestone: Super Mario Land and Link's Awakening playable; Pokémon saves persist.*

### Phase 5 — APU (~1 week) — ✅ done (dmg_sound 12/12; done before Phase 4)
Most emulators leave this last; sound bugs don't block gameplay.

- Frame sequencer (512 Hz) driving length counters, envelopes, sweep.
- Channels: 2 square (one with sweep), wave, noise (LFSR).
- Mixer → ring buffer → frontend streams at 44.1/48 kHz with simple
  downsampling. Sync strategy: audio-driven pacing (block on buffer) is the
  simplest way to get correct emulation speed without frame stutter.

*Milestone: Tetris music sounds right; Blargg `dmg_sound` sub-tests mostly pass.*

### Phase 6 — Accuracy & polish
- Mooneye-gb acceptance tests (timing edge cases, DMA, PPU quirks) — fix what
  real games actually depend on first.
- Performance pass: profile; target comfortably >60 fps on a laptop
  (realistically we'll be 10–50× faster than needed).
- Nice-to-haves in priority order: save states, fast-forward, MBC3 RTC,
  configurable palette, then CGB groundwork.

---

## 5. Testing & CI

- **CI on every push:** build + xUnit suite. Test ROMs that report status via
  serial output or magic memory values run headlessly with a cycle budget.
- **Test ROM matrix** (checked into `tests/roms/` where licenses permit,
  otherwise fetched by script): Blargg (`cpu_instrs`, `instr_timing`,
  `mem_timing`, `dmg_sound`), Mooneye acceptance suite, `dmg-acid2`.
- Commercial game ROMs are **never** committed — local testing only.

## 6. Key References

- **Pan Docs** (gbdev.io/pandocs) — the canonical hardware reference.
- **SM83 opcode table** (gbdev.io opcode table + json).
- **The Ultimate Game Boy Talk** (33c3) — best conceptual overview of the PPU.
- Mooneye-gb source/docs for timing corner cases.

## 7. Risk Notes

| Risk | Mitigation |
|------|------------|
| CPU flag bugs surface late as weird game glitches | Single-instruction JSON tests from day one |
| PPU timing rabbit hole | Scanline renderer first; FIFO only if a target test demands it |
| Audio sync stutter | Audio-driven pacing from the start, not vsync-driven |
| Scope creep toward CGB | CGB explicitly out of v1; revisit after acid2 + game matrix passes |
