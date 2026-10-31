#if REFERENCE_SIM
using System.Globalization;
using Sts2Sim.Core.ValueProps;

namespace Differential;

internal static class NormalReferenceEventTrace
{
    internal static NormalEventProjection Capture(NormalNativeHookJournal journal, Func<NormalHookValue, string> identity)
    {
        var records = journal.Records;
        NormalReferenceHookAudit.Check(records, journal.Provenance.Targets);
        CheckSideNotifications(records);
        var events = new List<NormalSemanticEvent>(); var sources = new List<NormalEventSource>(); var unmapped = new List<string>();
        var entries = records.Where(r => r.Stage == "enter").ToDictionary(r => r.Call);
        var children = records.Where(r => r.Stage == "enter").GroupBy(r => r.Parent).ToDictionary(g => g.Key, g => g.ToArray());
        var plays = new Dictionary<int, int>(); var attacks = new Dictionary<int, int>(); var activeAttacks = new Stack<NormalHookValue>();
        var powers = records.Where(r => r.Stage == "enter" && r.Kind == "listener" && r.Name == "BeforeApplied"
                && r.Receiver?.Fields?.ContainsKey("amount") == true)
            .Select((r, index) => (r.Receiver!.Reference, index)).ToDictionary(p => p.Reference, p => p.index);
        var activePlay = -1;
        string Id(NormalHookValue v) => v.Kind == "null" ? "none" : powers.TryGetValue(v.Reference, out var power) ? "power:" + power : identity(v);
        string S(NormalHookValue v) => v.Scalar ?? throw NormalEventTrace.Missing("native scalar " + v.Kind);
        NormalHookValue F(NormalHookValue v, string name) => v.Fields?.GetValueOrDefault(name) ?? throw NormalEventTrace.Missing("native field " + name);
        NormalHookValue A(NormalNativeHookRecord r, string type) => r.Arguments.Single(v => v.Kind.EndsWith("." + type, StringComparison.Ordinal));
        void Add(NormalNativeHookRecord r, string kind, params string[] data)
        {
            if (kind is "side-before" or "side-after" or "side-end-very-early" or "side-end-early" or "side-end" or "side-ended")
                data = [.. data, .. (r.Arguments[^1].Items ?? throw NormalEventTrace.Missing("native side participants")).Select(Id)];
            events.Add(new(kind, data)); sources.Add(new(events.Count - 1, r.Sequence, r.Name));
        }
        bool Outer(NormalNativeHookRecord r) => !entries.TryGetValue(r.Parent, out var p) || p.Kind != r.Kind || p.Name != r.Name
            || p.Receiver?.Reference != r.Receiver?.Reference;
        bool Ancestor(NormalNativeHookRecord r, string type, string name)
        {
            while (entries.TryGetValue(r.Parent, out r!)) if (r.Method?.DeclaringType == type && r.Name == name) return true;
            return false;
        }
        foreach (var r in records)
        {
            if (r.Context == "query") continue;
            if (r.Kind == "dispatch" && r.Stage == "enter" && r.Name is "BeforeCardPlayed" or "BeforeSideTurnStart" or "BeforeHandDraw")
            {
                var context = r.Arguments.Single(v => v.Fields?.ContainsKey("ending") == true);
                if (S(F(context, "ending")) != "false") throw NormalEventTrace.Missing("command started while native combat was ending");
            }
            if (r.Kind == "observer")
            {
                switch (r.Name)
                {
                    case "CardDrawn": Add(r, "draw", Id(r.Arguments[0])); break;
                    case "CardPlayStarted": Add(r, "play-request", Id(r.Arguments[0]), Id(r.Arguments[1])); break;
                    case "CardPlayFinished": Add(r, "play-finished", Id(r.Arguments[0])); activePlay = -1; break;
                    case "DamageResolved":
                    {
                        var d = r.Arguments[1]; string D(string n) => S(F(d, "<" + n + ">k__BackingField"));
                        var props = Enum.Parse<ValueProp>(D("Props"));
                        Add(r, "damage", Id(r.Arguments[0]), Id(F(d, "<Receiver>k__BackingField")), D("BlockedDamage"), D("UnblockedDamage"), D("OverkillDamage"),
                            D("WasTargetKilled"), D("WasBlockBroken"), D("WasFullyBlocked"), props.IsPoweredAttack() ? "true" : "false",
                            props.HasFlag(ValueProp.Move) ? "true" : "false", props.HasFlag(ValueProp.Unblockable) ? "true" : "false",
                            activePlay.ToString(), activeAttacks.Count == 0 ? "-1" : attacks[activeAttacks.Peek().Reference].ToString(), "0"); break;
                    }
                    case "PowerAmountChanged": Add(r, "power", Id(r.Arguments[0]), S(F(r.Arguments[0], "definition")), Id(F(r.Arguments[0], "owner")),
                        S(F(r.Arguments[0], "amount")), S(r.Arguments[1]), Id(r.Arguments[2]), Id(r.Arguments[3])); break;
                    case "CombatStarted": Add(r, "combat-started"); break;
                    case "PlayerTurnPrepared": Add(r, "player-turn-prepared"); break;
                    case "PlayerTurnStarted": Add(r, "player-turn-started"); break;
                    case "EnemyTurnStarting": Add(r, "enemy-turn-starting"); break;
                    case "PlayerTurnEnded": Add(r, "player-turn-ended"); break;
                    case "EnemyMoveStarted": Add(r, "enemy-move-started", Id(r.Arguments[0]), S(r.Arguments[1])); break;
                    case "EnemyMoveFinished": Add(r, "enemy-move-finished", Id(r.Arguments[0]), S(r.Arguments[1])); break;
                    case "SideTurnEndListener": break;
                    default: unmapped.Add(r.Sequence + ":observer:" + r.Name); break;
                }
                continue;
            }
            if (r.Kind == "operation" && r.Name == "RollMove")
            {
                if (r.Stage == "enter") Add(r, "move-prepare-start", Id(F(r.Receiver!, "creature")));
                else Add(r, "move-prepare-end", Id(F(r.Receiver!, "creature")), S(F(r.Receiver!, "move")));
                continue;
            }
            if (r.Kind == "dispatch" && Outer(r))
            {
                if (r.Stage == "enter")
                    switch (r.Name)
                    {
                        case "AfterEnergySpent": Add(r, "paid", Id(r.Arguments.Single(v => v.Fields?.ContainsKey("deck_source") == true)), S(r.Arguments[^1])); break;
                        case "BeforeCardPlayed":
                        {
                            var play = A(r, "CardPlay"); plays.Add(play.Reference, plays.Count); activePlay = plays[play.Reference];
                            Add(r, "before-play", Id(F(play, "card")), Id(F(play, "target")), S(F(play, "index")), S(F(play, "count")), S(F(play, "auto")),
                                S(F(F(play, "resources"), "<EnergySpent>k__BackingField")), S(F(play, "pile")).ToLowerInvariant()); break;
                        }
                        case "AfterCardPlayed": Add(r, "after-play", Id(F(A(r, "CardPlay"), "card"))); break;
                        case "BeforeAttack":
                        {
                            var attack = A(r, "AttackCommand"); attacks.Add(attack.Reference, attacks.Count); activeAttacks.Push(attack);
                            var targets = F(attack, "targets");
                            if (S(F(attack, "random")) != "false" || targets.Items is not { Length: 1 } || S(F(attack, "hits")) != "1") throw NormalEventTrace.Missing("unmapped native attack target/hit policy");
                            Add(r, "attack-start", attacks[attack.Reference].ToString(), Id(F(attack, "attacker")), Id(F(attack, "source")), Id(targets.Items[0]),
                                S(F(attack, "amount")), S(F(attack, "hits")), activePlay.ToString()); break;
                        }
                        case "AfterAttack":
                            if (!activeAttacks.TryPop(out var finished)) throw NormalEventTrace.Missing("native attack end");
                            Add(r, "attack-end", attacks[finished.Reference].ToString()); break;
                        case "BeforeSideTurnStart": Add(r, "side-before", S(A(r, "CombatSide")).ToLowerInvariant()); break;
                        case "AfterSideTurnStart": Add(r, "side-after", S(A(r, "CombatSide")).ToLowerInvariant()); break;
                        case "BeforeCombatStart": Add(r, "before-combat-start"); break;
                        case "BeforeRoomEntered":
                            var room = r.Arguments.Single(v => v.Fields?.ContainsKey("room") == true);
                            if (S(F(room, "room")) == "Monster") Add(r, "room-entered", "battle");
                            break;
                        case "AfterBlockCleared": Add(r, "block-cleared", Id(A(r, "Creature")), S(F(A(r, "Creature"), "block"))); break;
                        case "AfterEnergyReset": Add(r, "energy-reset", "player", S(F(A(r, "Player"), "energy"))); break;
                        case "BeforeHandDraw": Add(r, "before-hand-draw", "player"); break;
                        case "AfterPlayerTurnStart": Add(r, "player-start-early"); Add(r, "player-start"); break;
                        case "AfterPlayerTurnStartLate": Add(r, "player-start-late"); break;
                        case "AfterAutoPostPlayPhaseEntered": Add(r, "auto-post-play"); break;
                        case "BeforeSideTurnEndVeryEarly": Add(r, "side-end-very-early", S(A(r, "CombatSide")).ToLowerInvariant()); break;
                        case "BeforeSideTurnEndEarly": Add(r, "side-end-early", S(A(r, "CombatSide")).ToLowerInvariant()); break;
                        case "BeforeSideTurnEnd": Add(r, "side-end", S(A(r, "CombatSide")).ToLowerInvariant()); break;
                        case "AfterSideTurnEnd": Add(r, "side-ended", S(A(r, "CombatSide")).ToLowerInvariant()); break;
                        case "AfterShuffle": Add(r, "shuffle-finished"); break;
                        case "BeforeFlush": Add(r, "before-flush", "player"); break;
                        case "AfterFlush": Add(r, "after-flush", "player"); break;
                        case "ModifyCardPlayResultLocation": Add(r, "result-modifying", Id(r.Arguments[1]), S(F(r.Arguments[4], "<PileType>k__BackingField")).ToLowerInvariant()); break;
                        case "ModifyCardPlayCount": Add(r, "count-modifying", Id(r.Arguments[1]), S(r.Arguments[2])); break;
                        case "AfterModifyingCardPlayCount":
                            var countCall = records.Last(c => c.Sequence < r.Sequence && c.Kind == "dispatch" && c.Name == "ModifyCardPlayCount" && c.Stage == "exit");
                            Add(r, "count-modified", Id(r.Arguments[1]), S(countCall.Result!)); break;
                        case "ShouldClearBlock": Add(r, "block-clear-check", Id(r.Arguments[1])); break;
                        case "BeforeDamageReceived": Add(r, "before-damage", Id(r.Arguments[^2]), Id(r.Arguments[^5]), decimal.Parse(S(r.Arguments[^4]), CultureInfo.InvariantCulture).ToString("G29", CultureInfo.InvariantCulture)); break;
                        case "AfterDamageGiven":
                        case "AfterDamageReceived":
                            var result = r.Arguments[2]; string Damage(string name) => S(F(result, "<" + name + ">k__BackingField"));
                            Add(r, r.Name == "AfterDamageGiven" ? "damage-given" : "damage-received",
                                Id(r.Arguments[r.Name == "AfterDamageGiven" ? 1 : 4]), Id(F(result, "<Receiver>k__BackingField")),
                                Damage("BlockedDamage"), Damage("UnblockedDamage"), Damage("OverkillDamage"), Damage("WasTargetKilled")); break;
                        case "AfterCardEnteredCombat": Add(r, "card-entered", Id(r.Arguments.Single(v => v.Fields?.ContainsKey("deck_source") == true))); break;
                        case "AfterCardGenerated":
                        case "AfterCardGeneratedForCombat":
                            var generated = r.Arguments.Single(v => v.Fields?.ContainsKey("deck_source") == true);
                            Add(r, r.Name == "AfterCardGenerated" ? "card-generated" : "card-generated-for-combat", Id(generated)); break;
                        case "AfterDeath":
                            var death = r.Arguments.Any(v => v.Kind == "bool") ? r
                                : children[r.Call].Single(c => c.Kind == "dispatch" && c.Name == "AfterDeath");
                            if (S(death.Arguments.Single(v => v.Kind == "bool")) != "false") throw NormalEventTrace.Missing("unmapped prevented removal");
                            Add(r, "death", Id(A(r, "Creature"))); break;
                        case "AfterCardChangedPiles":
                            var movedCard = Id(r.Arguments.Single(v => v.Fields?.ContainsKey("deck_source") == true));
                            if (movedCard.Contains(":card-reward:", StringComparison.Ordinal)) Add(r, "card-obtained", movedCard);
                            break;
                        case "AfterPotionProcured": Add(r, "potion-obtained", Id(r.Arguments[^1])); break;
                        case "AfterPotionDiscarded": Add(r, "potion-discarded", Id(r.Arguments[^1])); break;
                        case "AfterGoldGained": Add(r, "gold-gained", "player", S(F(r.Arguments[^1], "gold"))); break;
                        case "AfterCombatEnd": Add(r, "combat-ended", "victory"); break;
                        case "AfterCombatVictory": Add(r, "combat-victory"); break;
                        case "AfterCurrentHpChanged" when Ancestor(r, "Sts2Sim.Core.Models.Relics.BurningBlood", "AfterCombatVictory"):
                            Add(r, "heal", Id(r.Arguments[2]), S(F(r.Arguments[2], "hp")), S(r.Arguments[3])); break;
                        case "AfterCurrentHpChanged":
                            if (decimal.Parse(S(r.Arguments[3]), CultureInfo.InvariantCulture) >= 0) throw NormalEventTrace.Missing("unmapped positive HP change");
                            Add(r, "damage-hp-lost", Id(r.Arguments[2]), S(r.Arguments[3])); break;
                    }
                if (r.Stage == "exit" && r.Name == "ModifyGoldGained") Add(r, "gold-modified", "player", S(r.Result!));
                continue;
            }
            if (r.Kind != "listener") continue;
            if (r.Stage == "enter" && r.Name == "AfterAddedToRoom")
            { Add(r, "enemy-added", Id(F(r.Receiver!, "creature")), S(F(F(r.Receiver!, "creature"), "hp"))); continue; }
            if (r.Name == "OnPlay" && r.Receiver?.Fields?.ContainsKey("deck_source") == true && Outer(r))
            {
                Add(r, r.Stage == "enter" ? "card-body-start" : "card-body-end", Id(r.Receiver), activePlay.ToString()); continue;
            }
            if (r.Stage == "enter" && r.Name == "AfterObtained" && r.Receiver?.Fields?.ContainsKey("owner") == true)
            { Add(r, "relic-obtained", Id(r.Receiver), S(F(r.Receiver, "definition"))); continue; }
            if (r.Receiver?.Fields?.ContainsKey("amount") == true && r.Stage == "enter")
            {
                if (r.Name == "BeforeApplied")
                {
                    Add(r, "power-before-create", Id(r.Receiver), S(F(r.Receiver, "definition")), Id(r.Arguments[0]), S(r.Arguments[1]),
                        Id(r.Arguments[2]), Id(r.Arguments[3]), activePlay.ToString()); continue;
                }
                if (r.Name == "AfterApplied")
                { Add(r, "power-created", Id(r.Receiver), S(F(r.Receiver, "definition")), Id(F(r.Receiver, "owner")), S(F(r.Receiver, "amount"))); continue; }
                if (r.Name == "AfterRemoved")
                { Add(r, "power-removed", Id(r.Receiver), S(F(r.Receiver, "definition")), Id(r.Arguments[0])); continue; }
            }
            if (r.Method!.DefaultImplementation is not null) continue;
            var type = r.Method.DeclaringType;
            if (r.Method.DefaultDeclaration && type == "Sts2Sim.Core.Models.AbstractModel" && r.Name is "AfterDeath" or "AfterCardGenerated" or "TryModifyCardRewardOptionLate")
            {
                if (r.Stage == "enter" && (!children.TryGetValue(r.Call, out var forwarded) || forwarded.Length != 1
                    || forwarded[0].Name != r.Name || forwarded[0].Receiver?.Reference != r.Receiver?.Reference))
                    throw NormalEventTrace.Missing("unobserved forwarding child");
                continue;
            }
            if (type == "Sts2Sim.Core.Models.Relics.BoomingConch" && r.Name == "ModifyHandDraw")
            { if (r.Stage == "exit") Add(r, "draw-modifier", Id(r.Receiver!), "base-spire:booming-conch", S(r.Arguments[^1]), S(r.Result!)); }
            else if (type == "Sts2Sim.Core.Models.Relics.BoomingConch" && r.Name == "AfterSideTurnStart")
            {
                if (r.Stage != "exit") continue;
                var before = entries[r.Call]; var start = F(F(before.Receiver!, "owner"), "energy"); var end = F(F(r.Receiver!, "owner"), "energy");
                if (S(r.Arguments[0]) == "Player") Add(r, "energy-modifier", [Id(r.Receiver!), "base-spire:booming-conch", S(start), S(end),
                    "player", Id(F(before.Receiver!, "owner")), .. (r.Arguments[1].Items ?? throw NormalEventTrace.Missing("native energy participants")).Select(Id)]);
                else if (S(r.Arguments[0]) != "Enemy" || S(start) != S(end)
                    || r.Arguments[1].Items is not { } participants
                    || participants.Any(p => p.Reference == F(F(before.Receiver!, "owner"), "creature").Reference))
                    throw NormalEventTrace.Missing("inactive BoomingConch side/participant guard");
            }
            else if (type == "Sts2Sim.Core.Models.Powers.VulnerablePower" && r.Name == "ModifyDamageMultiplicative")
            {
                if (r.Stage == "exit")
                {
                    var amount = decimal.Parse(S(r.Arguments[1]), CultureInfo.InvariantCulture);
                    var result = amount * decimal.Parse(S(r.Result!), CultureInfo.InvariantCulture);
                    Add(r, "damage-modifier", Id(r.Receiver!), "base-spire:vulnerable-power", Id(r.Arguments[0]), amount.ToString("G29", CultureInfo.InvariantCulture),
                        result.ToString("G29", CultureInfo.InvariantCulture), Id(r.Arguments[^2]), activePlay.ToString());
                }
            }
            else if (type == "Sts2Sim.Core.Models.Relics.BurningBlood" && r.Name == "AfterCombatVictory")
                Add(r, r.Stage == "enter" ? "victory-reaction-start" : "victory-reaction-end", Id(r.Receiver!), "base-spire:burning-blood");
            else if (r.Stage == "enter") unmapped.Add(r.Sequence + ":listener:" + type + "." + r.Name);
        }
        if (activeAttacks.Count != 0 || activePlay != -1) throw NormalEventTrace.Missing("unfinished native play/attack");
        var exclusions = NormalReferenceEventExclusions.Audit(records, sources, unmapped);
        return new(events.ToArray(), sources.ToArray(), unmapped.Distinct().ToArray()) { Exclusions = exclusions };
    }

    private static void CheckSideNotifications(NormalNativeHookRecord[] records)
    {
        var pending = new Stack<(int Call, int Instance, string Hook, string Side)>();
        for (var i = 0; i < records.Length; i++)
        {
            var r = records[i];
            if (r.Kind != "observer" || r.Name != "SideTurnEndListener") continue;
            if (r.Arguments is not [var hook, var model, var side, var starting]) throw NormalEventTrace.Missing("side notification arguments");
            if (starting.Scalar == "true")
            {
                if (i + 1 == records.Length) throw NormalEventTrace.Missing("side listener was not entered");
                var call = records[i + 1];
                if (call.Stage != "enter" || call.Kind != "listener" || call.Name != hook.Scalar || call.Receiver?.Reference != model.Reference
                    || call.Arguments[0].Scalar != side.Scalar) throw NormalEventTrace.Missing("side listener entry was bypassed or reordered");
                pending.Push((call.Call, model.Reference, hook.Scalar!, side.Scalar!));
            }
            else
            {
                if (starting.Scalar != "false" || !pending.TryPop(out var before) || before.Instance != model.Reference || before.Hook != hook.Scalar
                    || before.Side != side.Scalar || i == 0 || records[i - 1].Stage != "exit" || records[i - 1].Call != before.Call)
                    throw NormalEventTrace.Missing("side listener completion was bypassed or reordered");
            }
        }
        if (pending.Count != 0) throw NormalEventTrace.Missing("unfinished side notification");
    }
}
#endif
