using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace Differential;

/// <summary>
/// `run`：ENV-03 的**最小可测整局流程**。
///
/// 现状（实测）：切片已有整局实体与"战斗结束写回整局"的接线，因此 `run` 现在能做的是
/// **跑通一场战斗并读回整局字段**——这正是整局流程的第一段，也是后续地图/房间层的地基。
/// 完整整局（地图、房间选择、奖励结算）尚未实现，因此本命令 rc=1（**部分实现：已测量的部分通过、
/// 整局未完成**），与 `verify` 的 rc=1 语义一致；并把缺口**从 `inventory` 的实测数字**带出来，
/// 不手写清单。
/// </summary>
internal static class RunCommand
{
    private const string PlayerTypeId = "base-spire:Player";
    private const string MonsterTypeId = "base-spire:Monster";
    private const string RunTypeId = "base-spire:Run";
    private const string RunStatesCollection = "base-spire:RunStates";
    private const string MonstersCollection = "base-spire:Monsters";
    private const string HandPile = "base-spire:Hand";
    private const string DrawPile = "base-spire:DrawPile";
    private const string DiscardPile = "base-spire:DiscardPile";
    private const string ExhaustPile = "base-spire:ExhaustPile";
    private const string PlayCardAction = "base-spire:PlayCard";
    private const string EnterNextRoom = "base-spire:EnterNextRoom";
    private const string StrikeId = "base-spire:strike-ironclad";
    private const string PommelStrikeId = "base-spire:pommel-strike";

    public static int Run(string outputDirectory, string coveragePath)
    {
        var json = new JsonObject
        {
            ["run_version"] = "env-03-run-flow@1",
            ["scope"] = "最小可测整局流程：一场真实战斗 + 战斗结束写回整局；地图/房间/奖励尚未实现。",
        };
        try
        {
            var rules = FormalSpirePackage.BuildRuleSet(out var loaded);
            var session = new Session(rules, 20261001UL, [SliceManifest.PlayerRole]);
            session.Start();

            var player = session.State.AllInstances(rules.TypeByQualifiedId(PlayerTypeId).Id).Single();
            var run = session.State.CollectionMembers(Collection(rules, session, RunStatesCollection)).Single();
            var before = RunFacts(rules, session, run);

            // 判别性构造：整局初始 Hp 是满血，因此把玩家压到 30 后写回才会看得出来。
            SetNumber(rules, session, player, PlayerTypeId, "Hp", 30);
            var enemies = session.State.CollectionMembers(Collection(rules, session, MonstersCollection))
                .ToList();
            foreach (var enemy in enemies)
            {
                SetNumber(rules, session, enemy, MonsterTypeId, "Hp", 1);
            }

            var steps = new List<string>();
            var attackers = new[] { StrikeId, PommelStrikeId };
            for (var index = 0; index < enemies.Count && index < attackers.Length; index++)
            {
                var card = MoveToHand(rules, session, attackers[index]);
                PlayAt(rules, session, card, enemies[index]);
                steps.Add($"play {attackers[index]} -> monster[{index}]");
            }

            var alive = session.State.CollectionMembers(Collection(rules, session, MonstersCollection)).Count;
            var after = RunFacts(rules, session, run);
            var settlement = new JsonObject
            {
                ["enemies_remaining"] = alive,
                // JsonNode 不能同时挂在两个父节点下：取出来的值必须 DeepClone 再放进第二个对象。
                ["run_hp_before"] = before["hp"]!.DeepClone(),
                ["run_hp_after"] = after["hp"]!.DeepClone(),
                ["player_hp"] = Number(rules, session, player, PlayerTypeId, "Hp"),
                ["run_current_room"] = after["current_room"]!.DeepClone(),
                ["run_settled_room"] = after["settled_room"]!.DeepClone(),
                ["battle_settled"] = Bool(rules, session, player, PlayerTypeId, "BattleSettled"),
            };
            // 判定：敌人全灭 + 整局 Hp 写成玩家血量 + 幂等键钉在本场房间。
            var ok = alive == 0
                && after["hp"]!.GetValue<long>() == 30L
                && after["settled_room"]!.GetValue<long>() == after["current_room"]!.GetValue<long>()
                && Bool(rules, session, player, PlayerTypeId, "BattleSettled");

            json["status"] = ok ? "flow_verified" : "flow_failed";
            json["flow"] = new JsonArray(steps.Select(step => (JsonNode)JsonValue.Create(step)).ToArray());
            json["settlement"] = settlement;

            // 整局循环的另半边：清场后由**公开动作**推进到下一间房（房间号 +1、结算标记复位、
            // 幂等键仍留在上一间房 ⇒ 下一场还会写回）。这一步通了，说明循环的**机制**是完整的，
            // 缺的是地图/遭遇/奖励这些**内容**（见 remaining_gap）。
            if (ok)
            {
                var enter = session.LegalActions(SliceManifest.Player)
                    .FirstOrDefault(candidate => candidate.Definition.QualifiedId == EnterNextRoom);
                var advance = new JsonObject { ["action_available"] = enter is not null };
                if (enter is not null)
                {
                    steps.Add($"submit {EnterNextRoom}");
                    session.Submit(enter.Definition, SliceManifest.Player, []);
                    while (session.Status == SessionStatus.AwaitingDecision)
                    {
                        var pending = session.PendingDecision!;
                        session.Answer(new DecisionAnswer(pending.RequestId, [pending.Candidates[0]]));
                    }

                    var advanced = RunFacts(rules, session, run);
                    advance["room_before"] = after["current_room"]!.DeepClone();
                    advance["room_after"] = advanced["current_room"]!.DeepClone();
                    advance["settled_room"] = advanced["settled_room"]!.DeepClone();
                    advance["battle_settled"] = Bool(rules, session, player, PlayerTypeId, "BattleSettled");
                    advance["enemies_in_new_room"] =
                        session.State.CollectionMembers(Collection(rules, session, MonstersCollection)).Count;
                    json["flow"] = new JsonArray(steps.Select(step => (JsonNode)JsonValue.Create(step)).ToArray());
                }

                json["room_advance"] = advance;
            }

            json["formal_source"] = new JsonObject
            {
                ["cards"] = loaded.CardIds.Count,
                ["powers"] = loaded.PowerIds.Count,
            };
            json["remaining_gap"] = RemainingGap(coveragePath);
            json["note"] = "rc=1 表示**部分实现**：这一段已测量并通过，但地图/房间/奖励与完整整局尚未实现"
                + "（缺口见 remaining_gap，取自 inventory 的实测分类数字，不手写清单）。";
        }
        catch (Exception failure)
        {
            json["status"] = "fault";
            json["error"] = $"{failure.GetType().Name}: {failure.Message}";
        }

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "run.json");
        File.WriteAllText(path,
            json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Console.WriteLine($"run flow: {path}");
        Console.WriteLine($"status:   {json["status"]}");
        if (json["settlement"] is { } settlementNode)
        {
            Console.WriteLine($"settlement: 敌人剩余 {settlementNode["enemies_remaining"]}；"
                + $"整局 Hp {settlementNode["run_hp_before"]} → {settlementNode["run_hp_after"]}"
                + $"（玩家 {settlementNode["player_hp"]}）；SettledRoom {settlementNode["run_settled_room"]}"
                + $" / CurrentRoom {settlementNode["run_current_room"]}");
        }

        return json["status"]?.GetValue<string>() == "flow_verified" ? 1 : 2;
    }

    private static JsonObject RunFacts(RuleSet rules, Session session, InstanceId run) => new()
    {
        ["hp"] = Number(rules, session, run, RunTypeId, "Hp"),
        ["max_hp"] = Number(rules, session, run, RunTypeId, "MaxHp"),
        ["gold"] = Number(rules, session, run, RunTypeId, "Gold"),
        ["current_room"] = Number(rules, session, run, RunTypeId, "CurrentRoom"),
        ["settled_room"] = Number(rules, session, run, RunTypeId, "SettledRoom"),
    };

    /// <summary>整局侧缺口：直接从 `verify` 的 coverage 分类数字里取（不手写清单）。</summary>
    private static JsonArray RemainingGap(string coveragePath)
    {
        var names = new[]
        {
            "ACT", "EVENT", "ENCOUNTER", "MONSTER", "RELIC", "POTION", "MODIFIER", "CHARACTER",
            "ENCHANTMENT", "ORB", "CARD_POOL", "POTION_POOL", "RELIC_POOL", "AFFLICTION",
        };
        var gap = new JsonArray();
        if (!File.Exists(coveragePath))
        {
            gap.Add(new JsonObject { ["note"] = $"找不到 coverage 产物：{coveragePath}（先跑 verify）" });
            return gap;
        }

        var root = JsonNode.Parse(File.ReadAllText(coveragePath))!.AsObject();
        if (root["categories"] is not JsonArray categories)
        {
            gap.Add(new JsonObject { ["note"] = "coverage 里没有 categories（先重跑 verify）" });
            return gap;
        }

        foreach (var node in categories)
        {
            var category = node!.AsObject();
            var name = category["category"]?.GetValue<string>() ?? string.Empty;
            if (!names.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            gap.Add(new JsonObject
            {
                ["category"] = name,
                ["denominator"] = category["denominator"]?.GetValue<int>() ?? 0,
                ["lowered"] = category["lowered"]?.GetValue<int>() ?? 0,
                ["uncovered"] = category["uncovered"]?.GetValue<int>() ?? 0,
            });
        }

        return gap;
    }

    private static int Collection(RuleSet rules, Session session, string name) =>
        rules.Collections.Single(item => item.QualifiedId == name).Id;

    private static long Number(RuleSet rules, Session session, InstanceId instance, string typeId, string field) =>
        session.State.GetField(instance, rules.TypeByQualifiedId(typeId).FieldIndex(field)).AsInt();

    private static bool Bool(RuleSet rules, Session session, InstanceId instance, string typeId, string field) =>
        session.State.GetField(instance, rules.TypeByQualifiedId(typeId).FieldIndex(field)).AsBool();

    private static void SetNumber(RuleSet rules, Session session, InstanceId instance, string typeId, string field,
        long value)
    {
        var index = rules.TypeByQualifiedId(typeId).FieldIndex(field);
        session.State.RunAtomic(() => session.State.SetField(instance, index, Value.Of(value)));
    }

    private static string Definition(RuleSet rules, Session session, InstanceId instance) =>
        session.State.GetField(instance, rules.TypeByQualifiedId("base-spire:Card").FieldIndex("DefinitionId"))
            .AsString();

    private static InstanceId MoveToHand(RuleSet rules, Session session, string cardId)
    {
        var hand = Collection(rules, session, HandPile);
        var existing = session.State.CollectionMembers(hand)
            .FirstOrDefault(card => Definition(rules, session, card) == cardId);
        if (existing != default)
        {
            return existing;
        }

        foreach (var pileName in new[] { DrawPile, DiscardPile, ExhaustPile })
        {
            var pile = Collection(rules, session, pileName);
            var card = session.State.CollectionMembers(pile)
                .FirstOrDefault(candidate => Definition(rules, session, candidate) == cardId);
            if (card == default)
            {
                continue;
            }

            session.State.RunAtomic(() =>
            {
                session.State.RemoveFromCollection(pile, card);
                session.State.AddToCollection(hand, card);
            });
            return card;
        }

        throw new InvalidOperationException($"正式源卡组里找不到 '{cardId}'");
    }

    private static void PlayAt(RuleSet rules, Session session, InstanceId card, InstanceId target)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == PlayCardAction);
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            var pending = session.PendingDecision!;
            var chosen = pending.Candidates.Contains(target) ? target : pending.Candidates[0];
            session.Answer(new DecisionAnswer(pending.RequestId, [chosen]));
        }
    }
}
