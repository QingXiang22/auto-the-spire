using CoreTheSpire.Engine;
using System.Globalization;

namespace Differential;

internal static class NormalDslEventTrace
{
    internal static NormalEventProjection Capture(IReadOnlyList<SessionDebugRecord> records, long dropped, RuleSet rules,
        IReadOnlyDictionary<InstanceId, string> identities, IReadOnlyDictionary<InstanceId, int> relics)
    {
        if (dropped != 0) throw NormalEventTrace.Missing("dropped DSL records");
        var events = new List<NormalSemanticEvent>(); var sources = new List<NormalEventSource>(); var unmapped = new List<string>();
        var stack = new Stack<SessionDebugRecord>();
        var procedures = rules.Procedures.ToDictionary(p => p.QualifiedId);
        var payloads = rules.Events.ToDictionary(e => e.QualifiedId);
        var reactions = rules.Subscriptions.Select(s => s.Reaction.QualifiedId).ToHashSet(StringComparer.Ordinal);
        var reactionEvents = rules.Subscriptions.GroupBy(s => s.Reaction.QualifiedId).ToDictionary(g => g.Key, g => g.Select(s => s.EventType.QualifiedId).Distinct().ToArray());
        var dispositions = new NormalDslEventExclusions(rules); var excluded = new Dictionary<(string Subject, string Reason), int>();
        var positions = records.Select((r, i) => (r.Sequence, i)).ToDictionary(p => p.Sequence, p => p.i);
        var effects = new int[records.Count + 1];
        for (var i = 0; i < records.Count; i++) effects[i + 1] = effects[i] + (records[i].Kind is SessionDebugKind.EventEmit or SessionDebugKind.RandomCall or SessionDebugKind.DecisionRequested ? 1 : 0);
        var actionPrograms = rules.Actions.Select(a => a.Program.QualifiedId).ToHashSet(StringComparer.Ordinal);
        var powers = records.Where(r => r.Kind == SessionDebugKind.EventEmit && r.Subject == "base-spire:PowerCreated")
            .Select((r, index) => (r.Snapshot!.Entities.Single(e => e.Type == "base-spire:Power").Instance, index)).ToDictionary(p => p.Instance, p => p.index);
        var plays = new Dictionary<InstanceId, int>(); var attacks = new Dictionary<InstanceId, int>(); var activeAttacks = new Stack<InstanceId>();
        var activePlay = -1;
        var livingEnemies = new List<string>();
        var prepared = new Dictionary<long, string>();
        void Exclude(string subject, string reason)
        { var key = (subject, reason); excluded[key] = excluded.GetValueOrDefault(key) + 1; }
        SessionDebugSnapshot Snap(SessionDebugRecord r) => !r.TextTruncated && r.Snapshot is { Complete: true } s ? s : throw NormalEventTrace.Missing("DSL snapshot " + r.Subject);
        SessionDebugValueSnapshot Param(SessionDebugRecord r, string name)
        {
            var parameters = r.Kind == SessionDebugKind.EventEmit ? payloads[r.Subject!].Payload : procedures[r.Subject!].Parameters;
            var index = parameters.Select((p, i) => (p.Name, i)).Single(p => p.Name == name).i;
            return Snap(r).Values[index];
        }
        string S(SessionDebugValueSnapshot v) => v.Scalar ?? throw NormalEventTrace.Missing("DSL scalar");
        InstanceId Ref(SessionDebugValueSnapshot v) => new(int.Parse(S(v)));
        SessionDebugEntitySnapshot Entity(SessionDebugSnapshot s, InstanceId id) => s.Entities.Single(e => e.Instance == id && !e.Missing);
        SessionDebugValueSnapshot F(SessionDebugEntitySnapshot e, string name) => e.Fields.Single(f => f.Name == name).Value;
        string Id(SessionDebugValueSnapshot v, SessionDebugSnapshot s)
        {
            if (v.Kind == ValueKind.None || v.Kind == ValueKind.Ref && v.Scalar == "0") return "none";
            var id = Ref(v);
            if (powers.TryGetValue(id, out var power)) return "power:" + power;
            if (relics.TryGetValue(id, out var relic)) return "relic:" + relic;
            if (!identities.TryGetValue(id, out var origin)) throw NormalEventTrace.Missing("DSL instance origin " + id);
            if (!origin.Contains(":base-spire:", StringComparison.Ordinal)) return origin;
            var card = Entity(s, id);
            return origin[..origin.IndexOf(":base-spire:", StringComparison.Ordinal)] + ":" + S(F(card, "DefinitionId"))
                + (card.Type == "base-spire:Card" && S(F(card, "Upgraded")) == "true" ? "+" : "");
        }
        string Actor(SessionDebugValueSnapshot value) => value.Kind == ValueKind.None || value.Kind == ValueKind.Ref && value.Scalar == "0" ? "none"
            : identities.TryGetValue(Ref(value), out var actor) ? actor : throw NormalEventTrace.Missing("DSL actor origin");
        string Pair(SessionDebugEntitySnapshot e, string a, string b) => F(e, a).Kind == ValueKind.Ref ? Actor(F(e, a)) : Actor(F(e, b));
        void Add(SessionDebugRecord r, string kind, params string[] data)
        {
            if (kind == "enemy-added") livingEnemies.Add(data[0]);
            if (kind == "death" && !livingEnemies.Remove(data[0])) throw NormalEventTrace.Missing("unknown DSL death participant");
            // These participants come from the preceding historical add/death
            // occurrences, never from the enemies remaining at the final boundary.
            if (kind is "side-before" or "side-after" or "side-end-very-early" or "side-end-early" or "side-end" or "side-ended")
                data = [.. data, .. (data[0] == "player" ? new[] { "player" } : livingEnemies.ToArray())];
            events.Add(new(kind, data)); sources.Add(new(events.Count - 1, checked((int)r.Sequence), r.Subject!));
        }
        foreach (var r in records)
        {
            if (r.Kind == SessionDebugKind.ProcedureEnter)
            {
                if (reactions.Contains(r.Subject!)) _ = Snap(r);
                stack.Push(r);
                if (r.Subject == "base-spire:ResolveCardEffects")
                { var s = Snap(r); Add(r, "play-request", Id(Param(r, "card"), s), Actor(Param(r, "target"))); }
                else if (r.Subject == "base-spire:ExecuteCardBody")
                { var s = Snap(r); Add(r, "card-body-start", Id(Param(r, "card"), s), activePlay.ToString()); }
                else if (r.Subject == "base-spire:RunEnemyTurn") Add(r, "enemy-turn-starting");
                else if (r.Subject == "base-spire:SelectNextEnemyMove") Add(r, "move-prepare-start", Actor(Param(r, "target")));
                else if (r.Subject == "base-spire:BurningBlood.WhenCombatEnded") Add(r, "victory-reaction-start", Id(Param(r, "listener"), Snap(r)), "base-spire:burning-blood");
                continue;
            }
            if (r.Kind == SessionDebugKind.ProcedureExit)
            {
                // Submit installs the action activation directly; its entry is the
                // public command, not a ProcedureEnter. Real called procedures and
                // reactions still require their actual paired entry below.
                if (stack.Count == 0 && actionPrograms.Contains(r.Subject!)) continue;
                if (!stack.TryPop(out var entry) || entry.Subject != r.Subject || r.TextTruncated)
                    throw NormalEventTrace.Missing($"DSL call nesting/completion at {r.Sequence} {r.Subject}; expected={entry?.Subject}; truncated={r.TextTruncated}");
                if (r.Subject == "base-spire:FinishCardPlay")
                { Add(r, "play-finished", Id(Param(entry, "card"), Snap(entry))); activePlay = -1; }
                else if (r.Subject == "base-spire:BeginDevelopmentCombat") Add(r, "combat-started");
                else if (r.Subject == "base-spire:DrawDevelopmentTurnHand") Add(r, "player-turn-prepared");
                else if (r.Subject == "base-spire:BeginDevelopmentTurn") Add(r, "player-turn-started");
                else if (r.Subject == "base-spire:PublishEnemyMove")
                {
                    var selection = stack.First(p => p.Subject == "base-spire:SelectNextEnemyMove");
                    prepared.Add(selection.Sequence, S(F(Entity(Snap(entry), Ref(Param(entry, "node"))), "Id")));
                }
                else if (r.Subject == "base-spire:SelectNextEnemyMove")
                {
                    if (!prepared.Remove(entry.Sequence, out var move)) throw NormalEventTrace.Missing("move selection did not publish its actual node");
                    Add(r, "move-prepare-end", Actor(Param(entry, "target")), move);
                }
                else if (r.Subject == "base-spire:RemoveObtainedOpeningRelic")
                {
                    var relic = Entity(Snap(entry), Ref(Param(entry, "relic")));
                    if (relics[relic.Instance] != 0) Add(r, "relic-obtained", "relic:" + relics[relic.Instance], S(F(relic, "DefinitionId")));
                }
                else if (r.Subject == "base-spire:RunEnemyTurn") Add(r, "player-turn-ended");
                else if (r.Subject == "base-spire:PerformEnemyMove")
                {
                    var enemy = Snap(entry).Entities.Single(e => e.Type == "base-spire:Enemy");
                    Add(r, "enemy-move-finished", identities[enemy.Instance], S(F(enemy, "MoveId")));
                }
                else if (r.Subject == "base-spire:BoomingConch.Draw") Add(r, "draw-modifier", Id(Param(entry, "listener"), Snap(entry)), "base-spire:booming-conch", S(Param(entry, "value")), r.Detail!);
                else if (r.Subject == "base-spire:BoomingConch.Energy")
                {
                    if (entry.StateVersion != r.StateVersion) throw NormalEventTrace.Missing("BoomingConch energy result requires a historical post-effect snapshot");
                    var owner = Entity(Snap(entry), Ref(Param(entry, "owner"))); var energy = S(F(owner, "Energy"));
                    Add(r, "energy-modifier", Id(Param(entry, "listener"), Snap(entry)), "base-spire:booming-conch", energy, energy,
                        "player", Actor(Param(entry, "owner")), "player");
                }
                else if (r.Subject == "base-spire:VulnerablePower.Vulnerable")
                {
                    var amount = decimal.Parse(S(Param(entry, "value")), CultureInfo.InvariantCulture) / 10000m;
                    var result = decimal.Parse(r.Detail!, CultureInfo.InvariantCulture) / 10000m;
                    Add(r, "damage-modifier", Id(Param(entry, "listener"), Snap(entry)), "base-spire:vulnerable-power", Actor(Param(entry, "target")),
                        amount.ToString("G29", CultureInfo.InvariantCulture), result.ToString("G29", CultureInfo.InvariantCulture), Id(Param(entry, "card"), Snap(entry)), activePlay.ToString());
                }
                else if (r.Subject == "base-spire:BurningBlood.WhenCombatEnded") Add(r, "victory-reaction-end", Id(Param(entry, "listener"), Snap(entry)), "base-spire:burning-blood");
                else if (reactions.Contains(r.Subject!))
                {
                    var noEffects = entry.StateVersion == r.StateVersion && effects[positions[r.Sequence]] == effects[positions[entry.Sequence] + 1];
                    var reason = dispositions.Reaction(procedures[r.Subject!], reactionEvents[r.Subject!], noEffects);
                    if (reason is null) unmapped.Add(entry.Sequence + ":reaction:" + r.Subject);
                    else Exclude(r.Subject!, reason);
                }
                continue;
            }
            if (r.Kind != SessionDebugKind.EventEmit) continue;
            var snapshot = Snap(r);
            switch (r.Subject)
            {
                case "base-spire:RelicObtained":
                    // The native AfterObtained callback starts after bag invalidation;
                    // the DSL's priority -100 acquisition adapter completes that stage.
                    Exclude(r.Subject, "acquisition-adapter: bag invalidation and obtained callback are represented by RemoveObtainedOpeningRelic completion"); break;
                case "base-spire:CardObtained":
                    var acquired = snapshot.Entities.Single(e => e.Type == "base-spire:Card");
                    if (identities[acquired.Instance].Contains(":card-reward:", StringComparison.Ordinal))
                        Add(r, "card-obtained", Id(Param(r, "card"), snapshot));
                    else Exclude(r.Subject, "starting-deck-materialization: native initial permanent cards are constructed directly");
                    break;
                case "base-spire:NormalRoomEntered": Add(r, "room-entered", "battle"); break;
                case "base-spire:EnemyAddedToCombat":
                    var added = Entity(snapshot, Ref(Param(r, "target"))); Add(r, "enemy-added", identities[added.Instance], S(F(added, "Hp"))); break;
                case "base-spire:CardEnteredCombat":
                    if (Id(Param(r, "card"), snapshot).StartsWith("generated:", StringComparison.Ordinal)) Add(r, "card-entered", Id(Param(r, "card"), snapshot));
                    else Exclude(r.Subject, "starting-combat-materialization: native initial deck clones are synchronous and publish no entry callback");
                    break;
                case "base-spire:AfterCardGenerated": Add(r, "card-generated", Id(Param(r, "card"), snapshot)); break;
                case "base-spire:AfterCardGeneratedForCombat": Add(r, "card-generated-for-combat", Id(Param(r, "card"), snapshot)); break;
                case "base-spire:AfterBlockCleared": Add(r, "block-cleared", "player", S(F(Entity(snapshot, Ref(Param(r, "owner"))), "Block"))); break;
                case "base-spire:AfterEnemyBlockCleared":
                    var cleared = Entity(snapshot, Ref(Param(r, "target"))); Add(r, "block-cleared", identities[cleared.Instance], S(F(cleared, "Block"))); break;
                case "base-spire:AfterEnergyReset": Add(r, "energy-reset", "player", S(F(Entity(snapshot, Ref(Param(r, "owner"))), "Energy"))); break;
                case "base-spire:BeforeHandDraw": Add(r, "before-hand-draw", "player"); break;
                case "base-spire:AfterPlayerTurnStartEarly": Add(r, "player-start-early"); break;
                case "base-spire:AfterPlayerTurnStart": Add(r, "player-start"); break;
                case "base-spire:AfterAutoPostPlayPhaseEntered": Add(r, "auto-post-play"); break;
                case "base-spire:BeforePlayerSideTurnEndVeryEarly": Add(r, "side-end-very-early", "player"); break;
                case "base-spire:BeforeEnemySideTurnEndVeryEarly": Add(r, "side-end-very-early", "enemy"); break;
                case "base-spire:BeforePlayerSideTurnEndEarly": Add(r, "side-end-early", "player"); break;
                case "base-spire:BeforeEnemySideTurnEndEarly": Add(r, "side-end-early", "enemy"); break;
                case "base-spire:BeforePlayerSideTurnEnd": Add(r, "side-end", "player"); break;
                case "base-spire:BeforeEnemySideTurnEnd": Add(r, "side-end", "enemy"); break;
                case "base-spire:AfterPlayerSideTurnEnd": Add(r, "side-ended", "player"); break;
                case "base-spire:AfterEnemySideTurnEnd": Add(r, "side-ended", "enemy"); break;
                case "base-spire:AfterShuffle": Add(r, "shuffle-finished"); break;
                case "base-spire:BeforeHandFlush": Add(r, "before-flush", "player"); break;
                case "base-spire:AfterHandFlush": Add(r, "after-flush", "player"); break;
                case "base-spire:CardPlayResultModifying":
                case "base-spire:CardPlayCountModifying":
                case "base-spire:AfterModifyingCardPlayCount":
                    var preparation = snapshot.Entities.Single(e => e.Type == "base-spire:CardPlayPreparation");
                    var cardContext = Snap(stack.First(p => p.Subject == "base-spire:ResolveCardEffects"));
                    Add(r, r.Subject == "base-spire:CardPlayResultModifying" ? "result-modifying"
                        : r.Subject == "base-spire:CardPlayCountModifying" ? "count-modifying" : "count-modified",
                        Id(F(preparation, "Card"), cardContext), S(F(preparation, r.Subject == "base-spire:CardPlayResultModifying" ? "ResultPile" : "Count")));
                    break;
                case "base-spire:BlockClearChecking":
                    var check = snapshot.Entities.Single(e => e.Type == "base-spire:BlockClearCheck");
                    Add(r, "block-clear-check", Pair(check, "Player", "Enemy")); break;
                case "base-spire:BeforeDamageReceived":
                    var pendingDamage = snapshot.Entities.Single(e => e.Type == "base-spire:DamageResult");
                    Add(r, "before-damage", Pair(pendingDamage, "DealerPlayer", "DealerEnemy"), Pair(pendingDamage, "Player", "Enemy"),
                        (decimal.Parse(S(F(pendingDamage, "ModifiedScaled")), CultureInfo.InvariantCulture) / 10000m).ToString("G29", CultureInfo.InvariantCulture)); break;
                case "base-spire:AfterDamageHpLost":
                    var hpLoss = snapshot.Entities.Single(e => e.Type == "base-spire:DamageResult");
                    Add(r, "damage-hp-lost", Pair(hpLoss, "Player", "Enemy"), "-" + S(F(hpLoss, "Unblocked"))); break;
                case "base-spire:AfterDamageGiven":
                case "base-spire:AfterDamageReceived":
                    var notifiedDamage = snapshot.Entities.Single(e => e.Type == "base-spire:DamageResult");
                    Add(r, r.Subject == "base-spire:AfterDamageGiven" ? "damage-given" : "damage-received",
                        Pair(notifiedDamage, "DealerPlayer", "DealerEnemy"), Pair(notifiedDamage, "Player", "Enemy"),
                        S(F(notifiedDamage, "Blocked")), S(F(notifiedDamage, "Unblocked")), S(F(notifiedDamage, "Overkill")), S(F(notifiedDamage, "Killed"))); break;
                case "base-spire:PotionObtained": Add(r, "potion-obtained", Id(Param(r, "potion"), snapshot)); break;
                case "base-spire:PotionDiscarded": Add(r, "potion-discarded", Id(Param(r, "potion"), snapshot)); break;
                case "base-spire:AfterModifyingGoldGained": Add(r, "gold-modified", "player", S(Param(r, "amount"))); break;
                case "base-spire:GoldGained": Add(r, "gold-gained", "player", S(F(Entity(snapshot, Ref(Param(r, "owner"))), "Gold"))); break;
                case "base-spire:AfterCardDrawn": Add(r, "draw", Id(Param(r, "card"), snapshot)); break;
                case "base-spire:AfterEnergySpent": Add(r, "paid", Id(Param(r, "card"), snapshot), S(Param(r, "amount"))); break;
                case "base-spire:BeforeCardExecution":
                {
                    var play = Entity(snapshot, Ref(Param(r, "play"))); plays.Add(play.Instance, plays.Count); activePlay = plays[play.Instance];
                    Add(r, "before-play", Id(Param(r, "card"), snapshot), Actor(F(play, "Target")), S(F(play, "PlayIndex")), S(F(play, "PlayCount")),
                        S(F(play, "IsAutoPlay")), S(F(play, "PaidEnergy")), S(F(play, "ResultPile"))); break;
                }
                case "base-spire:AfterCardExecution": Add(r, "card-body-end", Id(Param(r, "card"), snapshot), activePlay.ToString()); break;
                case "base-spire:AfterCardPlayed": Add(r, "after-play", Id(Param(r, "card"), snapshot)); break;
                case "base-spire:BeforeAttack":
                {
                    var attack = snapshot.Entities.Single(e => e.Type == "base-spire:Attack"); attacks.Add(attack.Instance, attacks.Count); activeAttacks.Push(attack.Instance);
                    var card = F(attack, "Card");
                    // ExecuteAttack's entry contains the actual referenced card/play,
                    // copied before the notification rather than read from final state.
                    var context = stack.First(p => p.Subject == "base-spire:ExecuteAttack");
                    Add(r, "attack-start", attacks[attack.Instance].ToString(), Pair(attack, "Player", "Enemy"), Id(card, Snap(context)),
                        Pair(attack, "Target", "Defender"), S(F(attack, "Amount")), S(F(attack, "Hits")), activePlay.ToString()); break;
                }
                case "base-spire:AfterAttack":
                    var completed = snapshot.Entities.Single(e => e.Type == "base-spire:Attack");
                    if (!activeAttacks.TryPop(out var expected) || expected != completed.Instance) throw NormalEventTrace.Missing("DSL attack completion");
                    Add(r, "attack-end", attacks[completed.Instance].ToString()); break;
                case "base-spire:DamageResolved":
                {
                    var d = snapshot.Entities.Single(e => e.Type == "base-spire:DamageResult"); string D(string name) => S(F(d, name));
                    Add(r, "damage", Pair(d, "DealerPlayer", "DealerEnemy"), Pair(d, "Player", "Enemy"), D("Blocked"), D("Unblocked"), D("Overkill"),
                        D("Killed"), D("BlockBroken"), D("FullyBlocked"), D("Powered"), D("Move"), D("Unblockable"), activePlay.ToString(),
                        activeAttacks.Count == 0 ? "-1" : attacks[activeAttacks.Peek()].ToString(), D("Hit")); break;
                }
                case "base-spire:PowerAmountChanged":
                {
                    var power = snapshot.Entities.Single(e => e.Type == "base-spire:Power");
                    Add(r, "power", "power:" + powers[power.Instance], S(F(power, "DefinitionId")), Pair(power, "HolderPlayer", "HolderEnemy"), S(F(power, "Stacks")),
                        S(Param(r, "delta")), Param(r, "applierPlayer").Kind == ValueKind.Ref ? Actor(Param(r, "applierPlayer")) : Actor(Param(r, "applierEnemy")),
                        Id(Param(r, "card"), Snap(stack.First(p => p.Subject == "base-spire:ExecuteCardBody")))); break;
                }
                case "base-spire:BeforePowerCreated":
                    var nextPower = powers.Count(p => records.First(e => e.Kind == SessionDebugKind.EventEmit && e.Subject == "base-spire:PowerCreated"
                        && e.Snapshot!.Entities.Any(entity => entity.Instance == p.Key)).Sequence < r.Sequence);
                    Add(r, "power-before-create", "power:" + nextPower, S(Param(r, "definition")),
                        Param(r, "player").Kind == ValueKind.Ref ? Actor(Param(r, "player")) : Actor(Param(r, "enemy")), S(Param(r, "amount")),
                        Param(r, "applierPlayer").Kind == ValueKind.Ref ? Actor(Param(r, "applierPlayer")) : Actor(Param(r, "applierEnemy")),
                        Id(Param(r, "card"), Snap(stack.First(p => p.Subject == "base-spire:ExecuteCardBody"))), activePlay.ToString()); break;
                case "base-spire:AfterPowerApplied":
                    var applied = snapshot.Entities.Single(e => e.Type == "base-spire:Power");
                    Add(r, "power-created", "power:" + powers[applied.Instance], S(F(applied, "DefinitionId")), Pair(applied, "HolderPlayer", "HolderEnemy"), S(F(applied, "Stacks"))); break;
                case "base-spire:PowerRemoved":
                    var removed = snapshot.Entities.Single(e => e.Type == "base-spire:Power");
                    Add(r, "power-removed", "power:" + powers[removed.Instance], S(F(removed, "DefinitionId")), Pair(removed, "HolderPlayer", "HolderEnemy")); break;
                case "base-spire:EnemyDied":
                    var dead = snapshot.Entities.Single(e => e.Type == "base-spire:Enemy"); Add(r, "death", identities[dead.Instance]); break;
                case "base-spire:BeforeCombatStart": Add(r, "before-combat-start"); break;
                case "base-spire:BeforePlayerSideTurnStart": Add(r, "side-before", "player"); break;
                case "base-spire:BeforeEnemySideTurnStart": Add(r, "side-before", "enemy"); break;
                case "base-spire:AfterPlayerSideTurnStart": Add(r, "side-after", "player"); break;
                case "base-spire:AfterEnemySideTurnStart": Add(r, "side-after", "enemy"); break;
                case "base-spire:AfterPlayerTurnStartLate": Add(r, "player-start-late"); break;
                case "base-spire:EnemyTurnStarted":
                    var enemy = snapshot.Entities.Single(e => e.Type == "base-spire:Enemy");
                    Add(r, "enemy-move-started", identities[enemy.Instance], S(F(enemy, "MoveId"))); break;
                case "base-spire:CombatEnded": Add(r, "combat-ended", "victory"); break;
                case "base-spire:CombatVictory": Add(r, "combat-victory"); break;
                case "base-spire:PlayerHealed":
                    var owner = Entity(snapshot, Ref(Param(r, "owner")));
                    Add(r, "heal", "player", S(F(owner, "Hp")), S(Param(r, "actual"))); break;
                default:
                    var eventReason = NormalDslEventExclusions.Event(r.Subject!);
                    if (eventReason is null) unmapped.Add(r.Sequence + ":event:" + r.Subject);
                    else Exclude(r.Subject!, eventReason);
                    break;
            }
        }
        if (activeAttacks.Count != 0 || activePlay != -1) throw NormalEventTrace.Missing("unfinished DSL attack/play");
        return new(events.ToArray(), sources.ToArray(), unmapped.ToArray())
            { Exclusions = excluded.Select(p => new NormalEventExclusion(p.Key.Subject, p.Key.Reason, p.Value)).ToArray() };
    }
}
