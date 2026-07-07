namespace GBEmu.Core;

public enum GbButton
{
    Right, Left, Up, Down, // direction pad (select bit 4)
    A, B, Select, Start,   // action buttons (select bit 5)
}

/// <summary>
/// The joypad matrix behind register FF00. Buttons read active-low in the
/// nibble selected by bits 4/5; pressing a selected button requests the
/// joypad interrupt (IF bit 4).
/// </summary>
public sealed class Joypad
{
    private byte _select = 0x30; // bits 4-5 as written (1 = deselected)
    private byte _directions;    // bit set = pressed
    private byte _actions;

    /// <summary>IF bit 4 request; the bus collects this.</summary>
    public bool InterruptRequested;

    public void SetButton(GbButton button, bool pressed)
    {
        int bit = (int)button & 3;
        ref byte group = ref ((int)button < 4 ? ref _directions : ref _actions);
        bool wasPressed = (group & (1 << bit)) != 0;
        if (pressed)
            group |= (byte)(1 << bit);
        else
            group &= (byte)~(1 << bit);

        bool selected = (int)button < 4
            ? (_select & 0x10) == 0
            : (_select & 0x20) == 0;
        if (pressed && !wasPressed && selected)
            InterruptRequested = true;
    }

    public byte Read()
    {
        int nibble = 0x0F;
        if ((_select & 0x10) == 0)
            nibble &= ~_directions & 0x0F;
        if ((_select & 0x20) == 0)
            nibble &= ~_actions & 0x0F;
        return (byte)(0xC0 | _select | nibble);
    }

    public void Write(byte value) => _select = (byte)(value & 0x30);
}
