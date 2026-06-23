namespace Replay.Models.Descriptors;

[Flags]
public enum ExportCategory
{
    None = 0,
    Movement = 1 << 0,
    Ability = 1 << 1,
    Gunplay = 1 << 2,
    Combat = 1 << 3,
    Round = 1 << 4,
    Bomb = 1 << 5,
    Economy = 1 << 6,
    Effects = 1 << 7,
    Visibility = 1 << 8,
    Debug = 1 << 9,
    All = (1 << 10) - 1,
}
