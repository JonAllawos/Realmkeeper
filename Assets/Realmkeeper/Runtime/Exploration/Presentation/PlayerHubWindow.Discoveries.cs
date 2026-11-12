using Realmkeeper.Application;
using Realmkeeper.Domain.Quests;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 18 (ADR-0088): the Discoveries tab, grouped by kind (dangers first: they are what the player acts on), each
    /// with where it was found, whether it is only a guess, whether it matters, and how many settlements have heard it.
    /// Rumours stay on their own tab; places' explored and cleared states stay on Places.
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        private static readonly DiscoveryCategory[] DiscoveryOrder =
        {
            DiscoveryCategory.Dangerous, DiscoveryCategory.Faction, DiscoveryCategory.Economic, DiscoveryCategory.Geographic, DiscoveryCategory.Historical,
            DiscoveryCategory.Cultural, DiscoveryCategory.Religious, DiscoveryCategory.Arcane, DiscoveryCategory.Natural, DiscoveryCategory.Personal, DiscoveryCategory.None,
        };

        public static string DiscoveryHeading(DiscoveryCategory c)
        {
            switch (c)
            {
                case DiscoveryCategory.Dangerous: return "Dangers";
                case DiscoveryCategory.Faction: return "Factions";
                case DiscoveryCategory.Economic: return "Trade and wealth";
                case DiscoveryCategory.Geographic: return "Ways and places";
                case DiscoveryCategory.Historical: return "History";
                case DiscoveryCategory.Cultural: return "Customs";
                case DiscoveryCategory.Religious: return "Faith";
                case DiscoveryCategory.Arcane: return "Mysteries";
                case DiscoveryCategory.Natural: return "The wild";
                case DiscoveryCategory.Personal: return "People";
                default: return "Other findings";
            }
        }

        private void DiscoveryPanel(JournalView j)
        {
            if (j.DiscoveryEntries.Count == 0)
            {
                _content.Add(Text("Nothing found out yet. Examine what you find in old places; read what was left behind.", "xk-w-empty"));
                return;
            }

            _content.Add(Text("◆ matters · [unverified] seen but not yet confirmed. Tell what you find to those it concerns: an elder, a guard, a merchant.", "xk-w-desc"));
            foreach (var c in DiscoveryOrder)
            {
                var any = false;
                foreach (var e in j.DiscoveryEntries)
                {
                    if (e.Category != c) continue;
                    if (!any)
                    {
                        var head = Text(DiscoveryHeading(c), "xk-w-heading");
                        head.name = "xk-discoveries-" + c.ToString().ToLowerInvariant();
                        _content.Add(head);
                        any = true;
                    }

                    var line = Text((e.Major ? "◆ " : string.Empty) + (e.Certainty == DiscoveryCertainty.Unverified ? "[unverified] " : string.Empty) + e.When
                        + (e.Place != null ? " · " + e.Place : string.Empty) + " — " + e.Text
                        + (e.SharedCount > 0 ? $" (told in {e.SharedCount} {(e.SharedCount == 1 ? "settlement" : "settlements")})" : string.Empty), "xk-w-ledger-line");
                    line.name = "xk-discovery-" + e.Key;
                    line.style.whiteSpace = UnityEngine.UIElements.WhiteSpace.Normal; // long findings wrap rather than run off the panel
                    _content.Add(line);
                }
            }
        }
    }
}
