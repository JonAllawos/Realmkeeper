using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.Quests
{
    // Phase 12 (ADR-0058): dialogue as data. A graph of nodes; each node has the speaker's lines (the first whose
    // conditions hold is said) and the player's responses (shown when their conditions hold). A response may carry a
    // skill check and consequences; choosing it goes through one command, which applies them atomically.

    public enum ResponseKind
    {
        Ask = 0,
        RequestInformation = 1,
        OfferHelp = 2,
        AcceptQuest = 3,
        DeclineQuest = 4,
        Negotiate = 5,
        Threaten = 6,
        Persuade = 7,
        Deceive = 8,
        Intimidate = 9,
        Bribe = 10,
        Trade = 11,
        Challenge = 12,
        End = 13,
        Continue = 14,
        Give = 15,
    }

    public sealed class DialogueLine
    {
        /// <summary>Tokens: {npc}, {player}, {settlement}, {role}… and {answer} (a topic answer from the person's own knowledge).</summary>
        public string Text { get; set; }

        /// <summary>A gentler variant for softened presentation of tagged content (null: the line is left out instead).</summary>
        public string Softened { get; set; }

        public ContentTags Tags { get; set; }
        public IReadOnlyList<QuestCondition> Conditions { get; set; } = Array.Empty<QuestCondition>();
    }

    /// <summary>A skill check made on a response, rolled with the world's dice: d20 + ability modifier (+ proficiency) against a DC.</summary>
    public sealed class DialogueCheck
    {
        /// <summary>A skill name (Persuasion, Intimidation, Deception, Insight, Investigation…).</summary>
        public string Skill { get; set; }

        public int Dc { get; set; }

        /// <summary>The person's trait that raises the DC (Key) by (trait − 50) / 10, e.g. a suspicious person is harder to deceive.</summary>
        public string ResistTrait { get; set; }
    }

    public enum DialogueConsequenceKind
    {
        /// <summary>The person answers a Phase 4 topic from their own knowledge (Key: topic).</summary>
        AskTopic = 0,
        OfferQuest = 1,
        AcceptQuest = 2,
        DeclineQuest = 3,
        AbandonQuest = 4,

        /// <summary>A decision recorded on the quest (Key) with its text (Value).</summary>
        Decision = 5,
        SetVariable = 6,

        /// <summary>Regard (Role, default the speaker; Key: dimension; Amount).</summary>
        Regard = 7,

        /// <summary>The player's standing with a role's faction (Role; Amount).</summary>
        FactionStanding = 8,

        SettlementReputation = 9,

        /// <summary>The player learns a fact (Key, Value: the text).</summary>
        Reveal = 10,

        /// <summary>The player hears a rumour (Key, Value).</summary>
        Rumor = 11,

        /// <summary>The speaker remembers it (Key: subject; Amount: salience).</summary>
        NpcMemory = 12,

        /// <summary>The speaker learns of a deed by the player (Key: deed), which then travels as a rumour.</summary>
        PlayerDeed = 13,

        /// <summary>The player gives an item to a person (Key: item ID; Amount: quantity; Role: recipient, default the speaker).</summary>
        GiveItem = 14,

        /// <summary>The player receives an item (Key: item ID; Amount).</summary>
        ReceiveItem = 15,

        PayCoin = 16,
        ReceiveCoin = 17,

        /// <summary>The player learns where a role is (Role).</summary>
        RevealLocation = 18,

        /// <summary>A fight starts (Key: outing purpose).</summary>
        StartEncounter = 19,

        /// <summary>The quest's ending (Key).</summary>
        SetOutcome = 20,

        /// <summary>An objective is done (Key: objective ID).</summary>
        CompleteObjective = 21,

        /// <summary>The person's shop opens to the player.</summary>
        OpenTrade = 22,

        EndConversation = 23,

        /// <summary>The speaker's morale (Amount).</summary>
        NpcMorale = 24,

        Notoriety = 25,

        /// <summary>A hidden objective becomes visible (Key: objective ID).</summary>
        RevealObjective = 26,
    }

    public sealed class DialogueConsequence
    {
        public DialogueConsequenceKind Kind { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Role { get; set; }
        public int Amount { get; set; }

        /// <summary>Applied only when the response's check succeeded (true), failed (false), or always (null).</summary>
        public bool? OnSuccess { get; set; }

        public static DialogueConsequence Of(DialogueConsequenceKind kind, string key = null, int amount = 0, string value = null, string role = null, bool? onSuccess = null) =>
            new DialogueConsequence { Kind = kind, Key = key, Amount = amount, Value = value, Role = role, OnSuccess = onSuccess };
    }

    public sealed class DialogueResponse
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public ResponseKind Kind { get; set; }
        public IReadOnlyList<QuestCondition> Conditions { get; set; } = Array.Empty<QuestCondition>();
        public DialogueCheck Check { get; set; }
        public IReadOnlyList<DialogueConsequence> Consequences { get; set; } = Array.Empty<DialogueConsequence>();

        /// <summary>The next node (null: back to the start; "end": the conversation ends).</summary>
        public string Next { get; set; }

        /// <summary>The next node when the check fails (null: the same as <see cref="Next"/>).</summary>
        public string NextOnFailure { get; set; }

        /// <summary>Offered only once per person (the key is remembered).</summary>
        public bool Once { get; set; }

        public ContentTags Tags { get; set; }
    }

    public sealed class DialogueNode
    {
        public string Id { get; set; }
        public IReadOnlyList<DialogueLine> Lines { get; set; } = Array.Empty<DialogueLine>();
        public IReadOnlyList<DialogueResponse> Responses { get; set; } = Array.Empty<DialogueResponse>();
    }

    public sealed class DialogueDefinition
    {
        public string Id { get; set; }

        /// <summary>The quest it belongs to (its roles fill the tokens), or null for a person's general talk.</summary>
        public string QuestId { get; set; }

        public string StartNode { get; set; } = "start";
        public IReadOnlyList<DialogueNode> Nodes { get; set; } = Array.Empty<DialogueNode>();
        public string Source { get; set; } = "Realmkeeper original";
        public int Version { get; set; } = 1;

        public DialogueNode Find(string id)
        {
            foreach (var n in Nodes) if (n.Id == id) return n;
            return null;
        }
    }

    public sealed class TranscriptLine
    {
        public string Speaker { get; set; }
        public string Text { get; set; }

        public TranscriptLine Clone() => (TranscriptLine)MemberwiseClone();
    }

    /// <summary>The conversation in progress (one at a time; saved, so a reload resumes it).</summary>
    public sealed class ConversationData
    {
        public const int MaxTranscript = 24;

        public string Key { get; set; }
        public NpcId NpcId { get; set; }
        public string DialogueId { get; set; }
        public string NodeId { get; set; }

        /// <summary>The quest whose dialogue it is, or null.</summary>
        public string QuestInstanceId { get; set; }

        public long StartedAt { get; set; }

        /// <summary>How many responses were chosen (seeds the checks: the same choice in the same place rolls the same).</summary>
        public int Turn { get; set; }

        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public List<TranscriptLine> Transcript { get; set; } = new List<TranscriptLine>();

        public void Say(string speaker, string text)
        {
            Transcript.Add(new TranscriptLine { Speaker = speaker, Text = text });
            while (Transcript.Count > MaxTranscript) Transcript.RemoveAt(0);
        }

        public ConversationData Clone()
        {
            var c = (ConversationData)MemberwiseClone();
            c.Variables = new Dictionary<string, string>(Variables, StringComparer.Ordinal);
            c.Transcript = Transcript.ConvertAll(t => t.Clone());
            return c;
        }
    }
}
