using System;
using System.Collections.Generic;

namespace SplashCrucible.Data;

public readonly record struct PetVisualMetadata(string Colour, string BorrowType, string TemperedReleaseType);

public static class PetMetadata
{
    private static readonly Dictionary<string, PetVisualMetadata> Pets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Cu Sith"] = new("Red", "Shield", "Physical Attack"),
        ["Squirrel"] = new("Red", "Shield", "Haste"),
        ["Lamb"] = new("Red", "Shield", "Sleep"),
        ["Pugil"] = new("Blue", "Purge", "Paralyze Resist"),
        ["Opo-opo"] = new("Red", "Shield", "Paralyze"),
        ["Dodo"] = new("Yellow", "Dash", "Blind Resistance"),
        ["Coblyn"] = new("Yellow", "Interupt", "Poison Resistance"),
        ["Diremite"] = new("Red", "Block", "Knockback"),
        ["Megalocrab"] = new("Blue", "Purge", "Water Vuln"),
        ["Wespe"] = new("Green", "Block", "Explode"),
        ["Vulture"] = new("Green", "Dash", "Purge"),
        ["Mandragora"] = new("Red", "Toxin", "Heavy"),
        ["Geshunpest"] = new("Yellow", "Cleanse", "Heal"),
        ["Puk"] = new("Red", "Magic Barrier", "Knockback"),
        ["Crab"] = new("Blue", "Purge", "Haste"),
        ["Treant"] = new("Blue", "Block", "Phys Vuln"),
        ["Slime"] = new("Yellow", "Cleanse", "Bind"),
        ["Golem"] = new("Blue", "Interupt", "Phys Vuln"),
        ["Bat"] = new("Green", "Dash", "Cleanse"),
        ["Dullahan"] = new("Green", "Toxin", "Magic Vuln"),
        ["Bomb"] = new("Blue", "Magic Barrier", "Petrify"),
        ["Morbol"] = new("Red", "Toxin", ""),
        ["Antlion"] = new("Yellow", "Interupt", "Earth Vuln"),
        ["Goobbue"] = new("Blue", "Dash", "Slow"),
        ["Coeurl"] = new("Yellow", "Magic Barrier", "Phys Vuln"),
        ["Chimera"] = new("Red", "Shield", "Stun"),
        ["Adamantoise"] = new("Blue", "Purge", "Poison"),
        ["Worm"] = new("Yellow", "Block", "Heavy"),
        ["Ahriman"] = new("Red", "Interupt", ""),
        ["Cactuar"] = new("Red", "Shield", ""),
        ["Raptor"] = new("Yellow", "Purge", ""),
        ["Funguar"] = new("Green", "Dash", ""),
        ["Hecteyes"] = new("Yellow", "Shield", ""),
        ["Mousse"] = new("Blue", "Magic Barrier", ""),
        ["Skeleton"] = new("Red", "Magic Barrier", ""),
        ["Ghost"] = new("Yellow", "Toxin", "Knockback"),
        ["Spriggan"] = new("Red", "Block", ""),
        ["Ogre"] = new("Red", "Block", ""),
        ["Peiste"] = new("Red", "Toxin", ""),
        ["Bhoot"] = new("Green", "Cleanse", "Doom"),
        ["Wamoura"] = new("Blue", "Purge", "Magic Vuln"),
        ["Flan"] = new("Blue", "Purge", ""),
        ["Apkallu"] = new("Yellow", "Purge", ""),
        ["Uraganite"] = new("Green", "Block", ""),
        ["Adamantoise"] = new("Blue", "Purge", "Poison"),
        ["Zu"] = new("Green", "Dash", ""),
        ["Ice Golem"] = new("Blue", "Interupt", ""),
        ["Karlabos"] = new("Blue", "Purge", ""),
        ["Rafflesia"] = new("Yellow", "Toxin", ""),
        ["Behemoth"] = new("Yellow", "Shield", ""),
    };

    public static bool TryGet(string name, out PetVisualMetadata metadata)
        => Pets.TryGetValue(name, out metadata);
}
