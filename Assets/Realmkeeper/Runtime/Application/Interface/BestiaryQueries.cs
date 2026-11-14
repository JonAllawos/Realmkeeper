using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Phase 25 (ADR-0116): one bestiary entry, as much as the player knows.
    /// <list type="bullet">
    /// <item><b>Met.</b> Name, kind, size, habitats and how it behaves, as seen in a fight.</item>
    /// <item><b>Defeated.</b> Also its armour, hit points, speed, challenge, attacks, resistances and traits.</item>
    /// </list>
    /// Fields the player does not know are null; the screen says so in words.
    /// </summary>
    public sealed class BestiaryEntryView
    {
        public string CreatureId { get; internal set; }
        public string Name { get; internal set; }
        public CreatureCategory Category { get; internal set; }
        public string CategoryName { get; internal set; }
        public string Type { get; internal set; }
        public string Size { get; internal set; }
        public string Habitats { get; internal set; }
        public string Behaviour { get; internal set; }
        public string Description { get; internal set; }
        public int Met { get; internal set; }
        public int Defeated { get; internal set; }
        public string FirstMet { get; internal set; }
        public bool Studied => Defeated > 0;

        // Known once defeated (null before).
        public string ArmourClass { get; internal set; }
        public string HitPoints { get; internal set; }
        public string Speed { get; internal set; }
        public string Challenge { get; internal set; }
        public List<string> Attacks { get; } = new List<string>();
        public string Defences { get; internal set; }
        public List<string> Traits { get; } = new List<string>();
        public string Senses { get; internal set; }
    }

    public sealed class BestiaryView
    {
        public List<BestiaryEntryView> Entries { get; } = new List<BestiaryEntryView>();

        /// <summary>How many kinds the player has met, and how many they have defeated at least once.</summary>
        public int Known => Entries.Count;

        public int Studied { get; internal set; }
    }

    public sealed partial class WorldSession
    {
        /// <summary>The bestiary: only the kinds the player has met, sorted by name. Unknown creatures are not listed at all.</summary>
        public BestiaryView GetBestiary()
        {
            var v = new BestiaryView();
            var p = _world?.Player;
            if (p?.KnownCreatures == null) return v;
            foreach (var k in p.KnownCreatures)
            {
                var d = CreatureCatalog.Find(k.CreatureId);
                if (d == null) continue;
                var e = new BestiaryEntryView
                {
                    CreatureId = d.Id, Name = d.Name, Category = d.Category, CategoryName = CategoryName(d.Category), Type = d.Type, Size = d.Size.ToString(),
                    Habitats = d.Habitats.Count == 0 ? null : string.Join(", ", HabitatNames(d.Habitats)), Behaviour = d.Behavior?.Summary,
                    Description = d.Description, Met = k.Met, Defeated = k.Defeated, FirstMet = When(k.FirstMetAt),
                };
                if (k.Defeated > 0)
                {
                    v.Studied++;
                    e.ArmourClass = d.ArmorClass.ToString(CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(d.ArmorNote) ? string.Empty : " (" + d.ArmorNote + ")");
                    e.HitPoints = d.MaxHp.ToString(CultureInfo.InvariantCulture) + " (" + d.HitDice + ")";
                    e.Speed = d.Speed + " ft" + (d.ClimbSpeed > 0 ? ", climb " + d.ClimbSpeed + " ft" : string.Empty) + (d.SwimSpeed > 0 ? ", swim " + d.SwimSpeed + " ft" : string.Empty);
                    e.Challenge = d.ChallengeRating + " (" + d.Xp.ToString(CultureInfo.InvariantCulture) + " XP)";
                    foreach (var a in d.Attacks)
                        e.Attacks.Add(a.Name + ": " + (a.AttackBonus >= 0 ? "+" : string.Empty) + a.AttackBonus + " to hit, " + a.Damage + " " + a.DamageType.ToString().ToLowerInvariant() + (a.Range > 0 ? ", range " + a.Range + " ft" : string.Empty));
                    var defences = new List<string>();
                    if (d.Resistances.Count > 0) defences.Add("resists " + string.Join(", ", Lower(d.Resistances)));
                    if (d.Immunities.Count > 0) defences.Add("immune to " + string.Join(", ", Lower(d.Immunities)));
                    if (d.Vulnerabilities.Count > 0) defences.Add("vulnerable to " + string.Join(", ", Lower(d.Vulnerabilities)));
                    e.Defences = defences.Count == 0 ? "None noted" : string.Join("; ", defences);
                    foreach (var t in d.Traits) e.Traits.Add(t.Name);
                    e.Senses = string.IsNullOrEmpty(d.Senses) ? (d.Darkvision > 0 ? "Darkvision " + d.Darkvision + " ft" : null) : d.Senses;
                }

                v.Entries.Add(e);
            }

            v.Entries.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return v;
        }

        public static string CategoryName(CreatureCategory c) => c switch
        {
            CreatureCategory.Humanoid => "People",
            CreatureCategory.Wildlife => "Wildlife",
            CreatureCategory.Livestock => "Livestock",
            CreatureCategory.CommonThreat => "Common threats",
            CreatureCategory.Dangerous => "Dangerous beasts",
            CreatureCategory.Undead => "Undead",
            _ => c.ToString(),
        };

        private static IEnumerable<string> HabitatNames(IReadOnlyList<Habitat> hs)
        {
            foreach (var h in hs) yield return h.ToString().ToLowerInvariant();
        }

        private static IEnumerable<string> Lower<T>(IReadOnlyList<T> list)
        {
            foreach (var x in list) yield return x.ToString().ToLowerInvariant();
        }
    }
}
