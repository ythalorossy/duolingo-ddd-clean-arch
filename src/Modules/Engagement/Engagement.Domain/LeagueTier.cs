namespace Engagement.Domain;

// The 10-tier ladder, ascending. Bronze is the floor, Diamond the summit.
public enum LeagueTier
{
    Bronze,
    Silver,
    Gold,
    Sapphire,
    Ruby,
    Emerald,
    Amethyst,
    Pearl,
    Obsidian,
    Diamond
}

public static class LeagueTierExtensions
{
    // Up one tier; Diamond (summit) has nowhere higher, so it stays.
    public static LeagueTier Next(this LeagueTier tier) =>
        tier == LeagueTier.Diamond ? tier : tier + 1;

    // Down one tier; Bronze (floor) has nowhere lower, so it stays.
    public static LeagueTier Previous(this LeagueTier tier) =>
        tier == LeagueTier.Bronze ? tier : tier - 1;
}
