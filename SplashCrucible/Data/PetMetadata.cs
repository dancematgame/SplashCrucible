using System;
using System.Collections.Generic;

namespace SplashCrucible.Data;

public readonly record struct PetVisualMetadata(
    string Colour,
    string Aspect,
    string BorrowType,
    string TemperedReleaseType);

public static class PetMetadata
{
    private static readonly Dictionary<string, PetVisualMetadata> Pets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Cu Sith"] = new("Red", "Piercing", "Shield", "Physical Attack"),
        ["Squirrel"] = new("Red", "Fire", "Shield", "Haste"),
        ["Lamb"] = new("Red", "Blunt", "Shield", "Sleep"),
        ["Pugil"] = new("Blue", "Water", "Purge", "Paralyze Resist"),
        ["Opo-opo"] = new("Red", "Blunt", "Shield", "Paralyze"),
        ["Dodo"] = new("Yellow", "Earth", "Dash", "Blind Resistance"),
        ["Coblyn"] = new("Yellow", "Lightning", "Interupt", "Poison Resistance"),
        ["Diremite"] = new("Red", "Piercing", "Block", "Knockback"),
        ["Megalocrab"] = new("Blue", "Water", "Purge", "Water Vuln"),
        ["Wespe"] = new("Green", "Piercing", "Block", "Explode"),
        ["Vulture"] = new("Green", "Wind", "Dash", "Purge"),
        ["Mandragora"] = new("Red", "Blunt", "Toxin", "Heavy"),
        ["Geshunpest"] = new("Yellow", "Lightning", "Cleanse", "Heal"),
        ["Puk"] = new("Red", "Piercing", "Barrier", "Knockback"),
        ["Crab"] = new("Blue", "Water", "Purge", "Haste"),
        ["Mantis"] = new("Blue", "Slashing", "Block", "Phys Vuln"),
        ["Slime"] = new("Yellow", "Unaspected", "Cleanse", "Bind"),
        ["Dullahan"] = new("Blue", "Slashing", "Interupt", "Phys Vuln"),
        ["Bat"] = new("Green", "Unaspected", "Dash", "Cleanse"),
        ["Flying Trap"] = new("Green", "Ice", "Toxin", "Magic Vuln"),
        ["Ziz"] = new("Blue", "Ice", "Barrier", "Petrify"),
        ["Sabotender"] = new("Red", "Piercing", "Toxin", ""),
        ["Golem"] = new("Yellow", "Earth", "Interupt", "Earth Vuln"),
        ["Apkallu"] = new("Blue", "Piercing", "Dash", "Slow"),
        ["Adamantoise"] = new("Yellow", "Lightning", "Barrier", "Phys Vuln"),
        ["Buffalo"] = new("Red", "Blunt", "Shield", "Stun"),
        ["Uragnite"] = new("Blue", "Ice", "Purge", "Poison"),
        ["Worm"] = new("Yellow", "Earth", "Block", "Heavy"),
        ["Spriggan"] = new("Red", "Fire", "Interupt", ""),
        ["Goobbue"] = new("Red", "Blunt", "Shield", ""),
        ["Gigantoad"] = new("Yellow", "Blunt", "Purge", ""),
        ["Colibri"] = new("Green", "Piercing", "Dash", ""),
        ["Coeurl"] = new("Yellow", "Lightning", "Shield", ""),
        ["Raptor"] = new("Blue", "Ice", "Barrier", ""),
        ["Drake"] = new("Red", "Fire", "Barrier", ""),
        ["Treant"] = new("Yellow", "Earth", "Toxin", "Knockback"),
        ["Antling"] = new("Red", "Slashing", "Block", ""),
        ["Chimera"] = new("Red", "Slashing", "Shield", ""),
        ["Morbol"] = new("Red", "Blunt", "Toxin", ""),
        ["Ghost"] = new("Green", "Wind", "Cleanse", "Doom"),
        ["Salamander"] = new("Blue", "Water", "Purge", "Magic Vuln"),
        ["Cobra"] = new("Blue", "Piercing", "Barrier", "Petrify"),
        ["Hydra"] = new("Blue", "Slashing", "Barrier", ""),
        ["Damselfly"] = new("Green", "Wind", "Block", ""),
        ["Rotting Goobbue"] = new("Yellow", "Earth", "Cleanse", ""),
        ["Zu"] = new("Green", "Slashing", "Dash", ""),
        ["Ice Golem"] = new("Blue", "Ice", "Interupt", ""),
        ["Karlabos"] = new("Blue", "Water", "Purge", ""),
        ["Rafflesia"] = new("Yellow", "Piercing", "Toxin", ""),
        ["Behemoth"] = new("Yellow", "Lightning", "Shield", ""),
    };

    public static bool TryGet(string name, out PetVisualMetadata metadata)
        => Pets.TryGetValue(name, out metadata);
}
