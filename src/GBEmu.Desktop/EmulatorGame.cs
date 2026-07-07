using GBEmu.Core;
using GBEmu.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GBEmu.Desktop;

/// <summary>
/// MonoGame frontend: runs one emulated frame per Update and blits the
/// framebuffer to a point-scaled texture. Runs at MonoGame's fixed 60 Hz —
/// within 0.5% of the DMG's 59.73 Hz; audio-driven pacing replaces this in
/// Phase 5.
/// </summary>
public sealed class EmulatorGame : Game
{
    private const int Scale = 4;

    // Classic DMG greens, packed as ABGR for Texture2D.SetData<uint>.
    private static readonly uint[] Palette =
    {
        Pack(0xE0, 0xF8, 0xD0),
        Pack(0x88, 0xC0, 0x70),
        Pack(0x34, 0x68, 0x56),
        Pack(0x08, 0x18, 0x20),
    };

    private static readonly (Keys Key, GbButton Button)[] KeyMap =
    {
        (Keys.Right, GbButton.Right),
        (Keys.Left, GbButton.Left),
        (Keys.Up, GbButton.Up),
        (Keys.Down, GbButton.Down),
        (Keys.Z, GbButton.A),
        (Keys.X, GbButton.B),
        (Keys.RightShift, GbButton.Select),
        (Keys.Enter, GbButton.Start),
    };

    private readonly GameBoy _gameBoy;
    private readonly uint[] _pixels = new uint[Ppu.ScreenWidth * Ppu.ScreenHeight];
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _screen = null!;

    public EmulatorGame(byte[] rom, string title)
    {
        _gameBoy = new GameBoy(rom);
        Window.Title = $"GBEmu — {title}";
        _ = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = Ppu.ScreenWidth * Scale,
            PreferredBackBufferHeight = Ppu.ScreenHeight * Scale,
        };
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _screen = new Texture2D(GraphicsDevice, Ppu.ScreenWidth, Ppu.ScreenHeight);
    }

    protected override void Update(GameTime gameTime)
    {
        KeyboardState keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape))
            Exit();

        foreach ((Keys key, GbButton button) in KeyMap)
            _gameBoy.Joypad.SetButton(button, keyboard.IsKeyDown(key));

        _gameBoy.RunFrame();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _gameBoy.Ppu.CopyFrame(_pixels, Palette);
        _screen.SetData(_pixels);

        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_screen, new Rectangle(
            0, 0, Ppu.ScreenWidth * Scale, Ppu.ScreenHeight * Scale), Color.White);
        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private static uint Pack(byte r, byte g, byte b) =>
        0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;
}
