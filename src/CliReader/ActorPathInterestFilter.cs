namespace CliReader;

internal static class ActorPathInterestFilter
{
    private static readonly string[] InterestingPathParts =
    [
        "Player",
        "Controller",
        "Character",
        "_PC",
        "Pawn",
        "Ability",
        "Gun",
        "Weapon",
        "Equippable",
        "Bomb",
        "Projectile",
        "Smoke",
        "Flash",
        "Trap",
        "Wall",
    ];

    public static bool IsInteresting(string path) =>
        InterestingPathParts.Any(part => path.Contains(part, StringComparison.OrdinalIgnoreCase));
}