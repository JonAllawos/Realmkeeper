using System;
using System.Collections.Generic;
using System.Globalization;
using FeatureDefinition = Realmkeeper.Domain.Places.FeatureDefinition;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Places
{
    /// <summary>
    /// Phase 18 (ADR-0089): the place mechanisms — a small set of complete patterns, each understood from what can be
    /// seen and read in the place, each with lasting state in the place's own variables (saved with it, never reset on
    /// load), each with a cost for getting it wrong, and none solved by guessing:
    /// <list type="bullet">
    /// <item><b>Sequence</b> (the Ringers' Vault): bells rung in the order a verse gives. A wrong bell brings grit down on
    /// the ringer and starts the sequence over; a second wrong peal wakes what sleeps by the tomb. The door they open can
    /// also be picked or forced, very hard.</item>
    /// <item><b>Staged water control</b> (Greywater Drift): clear the intake, then work the pump through its stages until
    /// the flooded gallery drains. Working it choked cracks the barrel, which stays cracked until mended with the right
    /// tool. The flooded gallery can still be swum, at a risk.</item>
    /// <item><b>Raking light</b> (Hollin Tower): a sign that shows only by a flame held close, pointing to a hidden way —
    /// which a thorough search can also find.</item>
    /// </list>
    /// </summary>
    public static partial class PlaceActions
    {
        public const int IntakeDc = 11;

        private static void MechanismOptions(WorldData w, PlaceData place, PlaceFeature f, FeatureStateData s, FeatureDefinition def, List<ActionOption> list)
        {
            void Add(PlaceAction a, string label, string reason = null, string check = null) => list.Add(new ActionOption { Action = a, Label = label, Enabled = reason == null, Reason = reason, Check = check });
            var look = def?.Study != null ? "Examine" : "Look";
            switch (f.Get(Phase18Places.Mech))
            {
                case Phase18Places.MechSequence:
                    if (Var(place, "seq:" + f.Get("seq")) != "solved") Add(PlaceAction.Activate, "Ring it");
                    Add(PlaceAction.Inspect, look, null, def?.Study?.Skill.ToString());
                    break;
                case Phase18Places.MechIntake:
                    if (Var(place, "intake") != "clear") Add(PlaceAction.Clear, "Clear the grate", null, "Athletics");
                    Add(PlaceAction.Inspect, "Look");
                    break;
                case Phase18Places.MechPump:
                    if (Var(place, "pump") == "cracked")
                        Add(PlaceAction.Mend, "Brace and mend the barrel", HasAny(w.Player, "pump_wrench", "smiths_tools") ? null : "The split barrel needs a pump-wright's wrench or smith's tools.");
                    else if (Water(place) > 0) Add(PlaceAction.Activate, "Work the pump");
                    Add(PlaceAction.Inspect, look, null, def?.Study?.Skill.ToString());
                    break;
                case Phase18Places.MechLight:
                    if (!s.Has(FeatureFlags.Read))
                        Add(PlaceAction.Illuminate, "Hold a flame close", PlaceRules.CarriesLight(w.Player) ? null : "You need a flame to throw the scoring into relief: a lantern or a torch.");
                    Add(PlaceAction.Inspect, "Look");
                    break;
            }
        }

        private static CommandResult Mechanism(CommandContext context, WorldData w, PlaceData place, PlaceRoom room, PlaceFeature f, FeatureDefinition def, FeatureStateData s, PlaceAction action)
        {
            var now = QuestClock.Now(w);
            var report = QuestRuntime.Reporter(context);
            var name = PlaceRules.Name(f);
            switch (f.Get(Phase18Places.Mech))
            {
                case Phase18Places.MechSequence when action == PlaceAction.Activate:
                {
                    var key = "seq:" + f.Get("seq");
                    var progress = Var(place, key) ?? string.Empty;
                    if (progress == "solved") return CommandResult.Ok("The bells hang still. The ringers' door stands open.");
                    s.Flags |= FeatureFlags.Used;
                    s.ChangedAt = now;
                    var order = int.TryParse(f.Get("order"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var o) ? o : 0;
                    if (order == progress.Length + 1)
                    {
                        progress += order.ToString(CultureInfo.InvariantCulture);
                        context.Quest(new QuestEvent { Kind = QuestEventKind.FeatureUsed, Subject = place.PlaceId, Detail = f.Id });
                        if (progress.Length < Phase18Places.BellOrder.Length)
                        {
                            place.Variables[key] = progress;
                            return CommandResult.Ok($"The {name.ToLowerInvariant()} rings out, clear and true, and the note hangs in the stone.");
                        }

                        place.Variables[key] = "solved";
                        var door = place.Layout.Connection(f.Get("opens"));
                        if (door != null && place.DoorOf(door) == DoorState.Locked) place.Doors[door.Id] = DoorState.Open;
                        place.Note($"The ringers' door in {place.Name} was opened by its bells.");
                        Discover(context, w, place, "ringers_door_open", "The three bells rung in the verse's order open the ringers' door.", Domain.Quests.DiscoveryCategory.Cultural, true);
                        PlaceRules.UpdateDiscovery(context, w, place);
                        return CommandResult.Ok($"The {name.ToLowerInvariant()} answers the other two, and the three notes run together. Below, stone grinds on stone: the ringers' door swings open.");
                    }

                    // The wrong bell: grit from the vault roof, the sequence lost, and the second time, the dead stir.
                    place.Variables[key] = string.Empty;
                    var fails = Count(place, key + ":fails") + 1;
                    place.Variables[key + ":fails"] = fails.ToString(CultureInfo.InvariantCulture);
                    var harm = new HarmSpec
                    {
                        Source = "mechanism:bells", Name = "falling grit and stone", Damage = new Dice(1, 6), DamageType = DamageType.Bludgeoning, Save = Ability.Dexterity, SaveDc = 12,
                    };
                    var hurt = PlaceHarm.Apply(w, harm, PlaceHarm.Key(place, "bells:" + fails.ToString(CultureInfo.InvariantCulture)), out _);
                    var said = $"The {name.ToLowerInvariant()} jangles against the others, sour and wrong. Grit and stone sift down from the vault. {hurt}".TrimEnd() + " The sequence is lost; it must be begun again.";
                    if (fails >= 2)
                    {
                        var tomb = place.Layout.Feature("story:ringers_tomb");
                        var stirred = tomb == null ? null : Wake(context, w, place, room, tomb, said + " ");
                        if (stirred != null) return stirred;
                    }

                    return CommandResult.Ok(said);
                }

                case Phase18Places.MechIntake when action == PlaceAction.Clear:
                {
                    if (Var(place, "intake") == "clear") return CommandResult.Ok("The grate is clear.");
                    var limit = PlaceRules.AttemptsLeft(place, f.Id, "clear", out var key);
                    if (limit != null) return CommandResult.InvalidState(limit);
                    var roll = PlaceRules.Check(w, Skill.Athletics, IntakeDc, key, PlaceRules.Dark(w, room));
                    if (!roll.Success)
                    {
                        PlaceRules.Failed(place, f.Id, "clear");
                        var harm = new HarmSpec { Source = "mechanism:intake", Name = "splintered timber", Damage = new Dice(1, 4), DamageType = DamageType.Slashing };
                        var hurt = PlaceHarm.Apply(w, harm, PlaceHarm.Key(place, "intake:" + s.Attempts.ToString(CultureInfo.InvariantCulture)), out _);
                        s.Attempts++;
                        return CommandResult.Ok($"({roll.Text}.) The silt is packed hard, and a splinter of prop timber opens your hand. {hurt}".TrimEnd());
                    }

                    place.Variables["intake"] = "clear";
                    s.Flags |= FeatureFlags.Used;
                    s.ChangedAt = now;
                    context.Quest(new QuestEvent { Kind = QuestEventKind.FeatureUsed, Subject = place.PlaceId, Detail = f.Id });
                    return CommandResult.Ok($"({roll.Text}.) You drag out silt and splintered timber by the armful until water runs freely through the grate.");
                }

                case Phase18Places.MechPump when action == PlaceAction.Activate:
                {
                    var water = Water(place);
                    if (water <= 0) return CommandResult.Ok("The gauge reads DRY. The gallery below is drained.");
                    s.Flags |= FeatureFlags.Used;
                    s.ChangedAt = now;
                    if (Var(place, "intake") != "clear")
                    {
                        place.Variables["pump"] = "cracked";
                        place.Note($"The pump at {place.Name} was worked choked, and its barrel split.");
                        return CommandResult.Ok("You lean on the handle. It fights you — silt grinds in the barrel — and then, with a crack like a shot, the barrel splits. Water sprays from the seam. The pump is useless until it is braced and mended.");
                    }

                    water--;
                    place.Variables["water"] = water.ToString(CultureInfo.InvariantCulture);
                    context.Quest(new QuestEvent { Kind = QuestEventKind.FeatureUsed, Subject = place.PlaceId, Detail = f.Id });
                    if (water > 0) return CommandResult.Ok("You work the handle until your arms burn. Water gouts from the outflow, and the gauge needle drops to HALF.");
                    place.Variables["drained"] = "yes";
                    place.Note($"The flooded gallery of {place.Name} was pumped dry.");
                    Discover(context, w, place, "drift_drained", "The flooded gallery of the drift has been pumped dry; the silver seam can be reached on foot.", Domain.Quests.DiscoveryCategory.Geographic, true);
                    return CommandResult.Ok("A last long effort, and the gauge reads DRY. Below, the flooded gallery drains away to black mud and dripping timber.");
                }

                case Phase18Places.MechPump when action == PlaceAction.Mend:
                {
                    if (Var(place, "pump") != "cracked") return CommandResult.Ok("The pump is sound.");
                    if (!HasAny(w.Player, "pump_wrench", "smiths_tools")) return CommandResult.InvalidState("You need a pump-wright's wrench or smith's tools.");
                    place.Variables.Remove("pump");
                    s.ChangedAt = now;
                    return CommandResult.Ok("You brace the split barrel with iron strapping and draw the bolts tight. It will hold — if the grate is kept clear.");
                }

                case Phase18Places.MechLight when action == PlaceAction.Illuminate:
                {
                    if (!PlaceRules.CarriesLight(w.Player)) return CommandResult.InvalidState("You need a flame.");
                    s.Flags |= FeatureFlags.Read | FeatureFlags.Examined;
                    s.ChangedAt = now;
                    var way = place.Layout.Connection(f.Get("reveals"));
                    if (way != null) PlaceRules.Find(context, place, way);
                    Learn(context, w, place, f, def, report);
                    return CommandResult.Ok((def?.Look ?? name) + " " + (def?.Insight ?? string.Empty));
                }
            }

            return null;
        }

        private static string Var(PlaceData place, string key) => place.Variables.TryGetValue(key, out var v) ? v : null;

        private static int Count(PlaceData place, string key) => int.TryParse(Var(place, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

        /// <summary>The drift's water: 2 (full), 1 (half), 0 (dry).</summary>
        public static int Water(PlaceData place) => place.Variables.ContainsKey("drained") ? 0 : place.Variables.ContainsKey("water") ? Count(place, "water") : 2;

        private static bool HasAny(PlayerCharacterData p, params string[] items)
        {
            foreach (var i in items) if (PlaceCommandHandlers.HasItem(p, i)) return true;
            return false;
        }

        /// <summary>A discovery the mechanism itself makes (not a feature's insight).</summary>
        private static void Discover(CommandContext context, WorldData w, PlaceData place, string key, string text, Domain.Quests.DiscoveryCategory category, bool major)
        {
            var d = new Domain.Quests.DiscoveryData { Key = key, Text = text, Source = "object:" + place.PlaceId, Category = category, Major = major, PlaceId = place.PlaceId };
            if (QuestRuntime.Discover(w, d, QuestRuntime.Reporter(context)))
                context.Quest(new QuestEvent { Kind = QuestEventKind.Discovered, Subject = key });
        }
    }
}
