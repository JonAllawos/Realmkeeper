using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using UnityEngine;
using UnityEngine.TestTools;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// The strategic view follows the runtime's session through shutdown and restart, and
    /// saves go to the runtime's selected slot.
    /// </summary>
    public sealed class SessionRestartPlayModeTests : RealmkeeperPlayModeFixture
    {
        [UnityTest]
        public IEnumerator RestartingTheSession_RebindsMapSelectionAndHud_WithoutDuplicateSubscriptions()
        {
            var first = Bootstrap.Session;
            Assert.That(first, Is.Not.Null);
            var baseline = first.WorldChangedSubscriberCount;
            Bootstrap.Selection.Select(first.GetSettlementsInView().First().SettlementId);

            for (var cycle = 0; cycle < 3; cycle++)
            {
                var old = Runtime.Session;
                Assert.That(Runtime.Host.Shutdown().IsSuccess, Is.True);
                yield return null;

                Assert.That(Bootstrap.Session, Is.Null, $"Cycle {cycle}: the map let go of the closed session.");
                Assert.That(Bootstrap.Hud.BoundSession, Is.Null);
                Assert.That(Bootstrap.Hud.BoundPlayback, Is.Null);
                Assert.That(Bootstrap.Selection.BoundSession, Is.Null);
                Assert.That(Bootstrap.Selection.HasSelection, Is.False);
                Assert.That(Bootstrap.WorldView.Settlements.Count, Is.EqualTo(0), "No stale world on screen.");
                Assert.That(Bootstrap.Hud.WorldNameLabel.text, Is.EqualTo("No world"));
                Assert.That(old.WorldChangedSubscriberCount, Is.EqualTo(0));

                Click(Bootstrap.Hud.AdvanceDayButton); // must not throw with no session
                StringAssert.Contains("No world session", Bootstrap.Hud.MessageLabel.text);

                var name = "Restarted " + cycle;
                Assert.That(Runtime.Host.StartNew(new CreateWorldCommand(name, 1000 + cycle)).IsSuccess, Is.True);
                yield return null;

                var session = Runtime.Session;
                Assert.That(session, Is.Not.SameAs(old));
                Assert.That(Bootstrap.Session, Is.SameAs(session));
                Assert.That(Bootstrap.Hud.BoundSession, Is.SameAs(session));
                Assert.That(Bootstrap.Selection.BoundSession, Is.SameAs(session));
                Assert.That(Bootstrap.Hud.BoundPlayback, Is.SameAs(Runtime.Playback));
                Assert.That(session.WorldChangedSubscriberCount, Is.EqualTo(baseline), "Each listener subscribed exactly once.");
                Assert.That(Bootstrap.WorldView.Settlements.Count, Is.EqualTo(session.GetSettlementsInView().Count));
                Assert.That(Bootstrap.Hud.WorldNameLabel.text, Is.EqualTo(name));

                var date = session.GetOverview().Date;
                Click(Bootstrap.Hud.AdvanceDayButton);
                Assert.That(session.GetOverview().Date, Is.EqualTo(date.AddDays(1)), "HUD commands reach the new session.");
            }
        }

        [UnityTest]
        public IEnumerator Saves_GoToTheSelectedSlot_AndInvalidSlotsAreRefused()
        {
            var saves = Path.Combine(UnityEngine.Application.persistentDataPath, "Saves");
            var a = Path.Combine(saves, "playmode-slot-a.rkworld.json");
            var b = Path.Combine(saves, "playmode-slot-b.rkworld.json");
            try
            {
                Assert.That(Runtime.ActiveSlot, Is.EqualTo(SaveSlot.Default));
                Assert.That(Runtime.SelectSlot("../escape").Status, Is.EqualTo(CommandStatus.ValidationFailed));
                Assert.That(Runtime.ActiveSlot, Is.EqualTo(SaveSlot.Default), "An invalid name leaves the slot unchanged.");

                Assert.That(Runtime.SelectSlot("playmode-slot-a").IsSuccess, Is.True);
                Click(Bootstrap.Hud.SaveButton);
                var dayA = Bootstrap.Session.GetOverview().Date;
                Assert.That(File.Exists(a), Is.True, Bootstrap.Hud.MessageLabel.text);

                Bootstrap.Session.Execute(new AdvanceDaysCommand(6));
                Assert.That(Runtime.SelectSlot("playmode-slot-b").IsSuccess, Is.True);
                Click(Bootstrap.Hud.SaveButton);
                var dayB = Bootstrap.Session.GetOverview().Date;
                Assert.That(File.Exists(b), Is.True);

                Assert.That(Runtime.Load("playmode-slot-a").IsSuccess, Is.True);
                yield return null;
                Assert.That(Bootstrap.Session.GetOverview().Date, Is.EqualTo(dayA));
                Assert.That(Runtime.ActiveSlot.Name, Is.EqualTo("playmode-slot-a"), "Loading selects the slot.");

                Click(Bootstrap.Hud.LoadButton); // loads the active slot (a)
                Assert.That(Bootstrap.Session.GetOverview().Date, Is.EqualTo(dayA));
                Assert.That(Runtime.Load("playmode-slot-b").IsSuccess, Is.True);
                Assert.That(Bootstrap.Session.GetOverview().Date, Is.EqualTo(dayB));

                Assert.That(Runtime.Load("playmode-missing").Status, Is.EqualTo(CommandStatus.NotFound));
                Assert.That(Runtime.ActiveSlot.Name, Is.EqualTo("playmode-slot-b"), "A failed load keeps the active slot.");
            }
            finally
            {
                if (File.Exists(a)) File.Delete(a);
                if (File.Exists(b)) File.Delete(b);
            }
        }
    }
}
