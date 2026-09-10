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
        ["Mantis"] = new("Blue", "Block", "Phys Vuln"),
        ["Slime"] = new("Yellow", "Cleanse", "Bind"),
        ["Dullahan"] = new("Blue", "Interupt", "Phys Vuln"),
        ["Bat"] = new("Green", "Dash", "Cleanse"),
        ["Flying Trap"] = new("Green", "Toxin", "Magic Vuln"),
        ["Ziz"] = new("Blue", "Magic Barrier", "Petrify"),
        ["Sabotender"] = new("Red", "Toxin", ""),
        ["Golem"] = new("Yellow", "Interupt", "Earth Vuln"),
        ["Apkallu"] = new("Blue", "Dash", "Slow"),
        ["Adamantoise"] = new("Yellow", "Magic Barrier", "Phys Vuln"),
        ["Buffalo"] = new("Red", "Shield", "Stun"),
        ["Uragnite"] = new("Blue", "Purge", "Poison"),
        ["Worm"] = new("Yellow", "Block", "Heavy"),
        ["Spriggan"] = new("Red", "Interupt", ""),
        ["Goobbue"] = new("Red", "Shield", ""),
        ["Gigantoad"] = new("Yellow", "Purge", ""),
        ["Colibri"] = new("Green", "Dash", ""),
        ["Coeurl"] = new("Yellow", "Shield", ""),
        ["Raptor"] = new("Blue", "Magic Barrier", ""),
        ["Drake"] = new("Red", "Magic Barrier", ""),
        ["Treant"] = new("Yellow", "Toxin", "Knockback"),
        ["Antling"] = new("Red", "Block", ""),
        ["Chimera"] = new("Red", "Shield", ""),
        ["Morbol"] = new("Red", "Toxin", ""),
        ["Ghost"] = new("Green", "Cleanse", "Doom"),
        ["Salamander"] = new("Blue", "Purge", "Magic Vuln"),
        ["Cobra"] = new("Blue", "Magic Barrier", "Petrify"),
        ["Hydra"] = new("Blue", "Magic Barrier", ""),
        ["Damselfly"] = new("Green", "Block", ""),
        ["Rotting Goobbue"] = new("Yellow", "Cleanse", ""),
        ["Zu"] = new("Green", "Dash", ""),
        ["Ice Golem"] = new("Blue", "Interupt", ""),
        ["Karlabos"] = new("Blue", "Purge", ""),
        ["Rafflesia"] = new("Yellow", "Toxin", ""),
        ["Behemoth"] = new("Yellow", "Shield", ""),
    };

    public static bool TryGet(string name, out PetVisualMetadata metadata)
        => Pets.TryGetValue(name, out metadata);
}
