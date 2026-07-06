namespace GBEmu.Core.Cartridge;

/// <summary>Cartridge type byte at 0x147. Values from Pan Docs.</summary>
public enum CartridgeType : byte
{
    RomOnly = 0x00,
    Mbc1 = 0x01,
    Mbc1Ram = 0x02,
    Mbc1RamBattery = 0x03,
    Mbc2 = 0x05,
    Mbc2Battery = 0x06,
    RomRam = 0x08,
    RomRamBattery = 0x09,
    Mmm01 = 0x0B,
    Mmm01Ram = 0x0C,
    Mmm01RamBattery = 0x0D,
    Mbc3TimerBattery = 0x0F,
    Mbc3TimerRamBattery = 0x10,
    Mbc3 = 0x11,
    Mbc3Ram = 0x12,
    Mbc3RamBattery = 0x13,
    Mbc5 = 0x19,
    Mbc5Ram = 0x1A,
    Mbc5RamBattery = 0x1B,
    Mbc5Rumble = 0x1C,
    Mbc5RumbleRam = 0x1D,
    Mbc5RumbleRamBattery = 0x1E,
    Mbc6 = 0x20,
    Mbc7SensorRumbleRamBattery = 0x22,
    PocketCamera = 0xFC,
    BandaiTama5 = 0xFD,
    HuC3 = 0xFE,
    HuC1RamBattery = 0xFF,
}

public static class CartridgeTypeExtensions
{
    public static bool HasBattery(this CartridgeType type) => type is
        CartridgeType.Mbc1RamBattery or
        CartridgeType.Mbc2Battery or
        CartridgeType.RomRamBattery or
        CartridgeType.Mmm01RamBattery or
        CartridgeType.Mbc3TimerBattery or
        CartridgeType.Mbc3TimerRamBattery or
        CartridgeType.Mbc3RamBattery or
        CartridgeType.Mbc5RamBattery or
        CartridgeType.Mbc5RumbleRamBattery or
        CartridgeType.Mbc7SensorRumbleRamBattery or
        CartridgeType.HuC1RamBattery;

    /// <summary>The mapper family this type belongs to, used to pick an MBC implementation.</summary>
    public static MapperKind Mapper(this CartridgeType type) => type switch
    {
        CartridgeType.RomOnly or CartridgeType.RomRam or CartridgeType.RomRamBattery
            => MapperKind.None,
        CartridgeType.Mbc1 or CartridgeType.Mbc1Ram or CartridgeType.Mbc1RamBattery
            => MapperKind.Mbc1,
        CartridgeType.Mbc2 or CartridgeType.Mbc2Battery
            => MapperKind.Mbc2,
        CartridgeType.Mbc3TimerBattery or CartridgeType.Mbc3TimerRamBattery or
        CartridgeType.Mbc3 or CartridgeType.Mbc3Ram or CartridgeType.Mbc3RamBattery
            => MapperKind.Mbc3,
        CartridgeType.Mbc5 or CartridgeType.Mbc5Ram or CartridgeType.Mbc5RamBattery or
        CartridgeType.Mbc5Rumble or CartridgeType.Mbc5RumbleRam or CartridgeType.Mbc5RumbleRamBattery
            => MapperKind.Mbc5,
        _ => MapperKind.Unsupported,
    };
}

public enum MapperKind
{
    None,
    Mbc1,
    Mbc2,
    Mbc3,
    Mbc5,
    Unsupported,
}
