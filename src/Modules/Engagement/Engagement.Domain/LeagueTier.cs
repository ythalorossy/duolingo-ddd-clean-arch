namespace Engagement.Domain;

// The 10-tier ladder. Slice 1 only ever uses Bronze; promotion/demotion (and any
// ordering/neighbour logic) arrives in Slice 2. Declared in ascending order.
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
