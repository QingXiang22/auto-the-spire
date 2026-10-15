using System.Globalization;
using CoreTheSpire.Engine;

namespace Differential;

/// <summary>
/// base 侧的状态读取：既产出**规范化事实**（跨后端可比），也产出**完整内部诊断**（只给自己看）。
///
/// 规范化事实用同一套路径表达两侧都能读到的规则事实；完整诊断里的实例 id、引擎类型名、
/// 集合名都是本后端私有的，不参与比较（ENV §6.2）。
/// </summary>
internal static class EngineSnapshot
{
    private const string PlayerType = "base-spire:Player";
    private const string MonsterType = "base-spire:Monster";
    private const string CardType = "base-spire:Card";
    private const string PowerType = "base-spire:Power";
    private const string DefinitionField = "DefinitionId";
    private const string PowersRelation = "base-spire:Powers";
    private const string MonstersCollection = "base-spire:Monsters";

    private static readonly string[] OrderedPiles =
        ["base-spire:DrawPile", "base-spire:Hand", "base-spire:DiscardPile", "base-spire:ExhaustPile"];

    private static readonly (string Collection, string Key)[] PileKeys =
    [
        ("base-spire:DrawPile", "draw"),
        ("base-spire:Hand", "hand"),
        ("base-spire:DiscardPile", "discard"),
        ("base-spire:ExhaustPile", "exhaust"),
    ];

    // -- 规范化事实（跨后端比较用） -----------------------------------------------------------------------

    /// <summary>
    /// 规范化事实。`exactPileOrder` = 是否连**抽牌堆/弃牌堆的顺序**也比：
    /// 这两个牌区的顺序由洗牌随机决定，在 ENV-01 提供原版兼容随机之前两侧必然不同，
    /// 因此那时只比构成（`sorted`），比顺序只会把 RNG 差异误报成规则差异。
    /// 手牌与消耗区始终比顺序（受控夹具里由夹具决定）。
    /// </summary>
    public static IReadOnlyList<FactSnapshot> Facts(Session session, bool exactPileOrder, bool observeDrawTop = false)
    {
        var facts = new List<FactSnapshot>();
        var playerType = session.RuleSet.TypeByQualifiedId(PlayerType);
        var monsterType = session.RuleSet.TypeByQualifiedId(MonsterType);
        var cardType = session.RuleSet.TypeByQualifiedId(CardType);
        var player = Player(session);

        facts.Add(new FactSnapshot("player.hp", Number(session, player, playerType, "Hp")));
        facts.Add(new FactSnapshot("player.max_hp", Number(session, player, playerType, "MaxHp")));
        facts.Add(new FactSnapshot("player.block", Number(session, player, playerType, "Block")));
        facts.Add(new FactSnapshot("player.energy", Number(session, player, playerType, "Energy")));

        var monsters = LivingEnemies(session);
        var enemyKeys = EnemyKeys(monsters.Select(monster =>
            Text(session, monster, monsterType, DefinitionField)));
        for (var index = 0; index < monsters.Count; index++)
        {
            var prefix = $"enemy.{enemyKeys[index]}";
            var hp = long.Parse(Number(session, monsters[index], monsterType, "Hp"), CultureInfo.InvariantCulture);
            facts.Add(new FactSnapshot($"{prefix}.hp", hp.ToString(CultureInfo.InvariantCulture)));
            facts.Add(new FactSnapshot($"{prefix}.max_hp", Number(session, monsters[index], monsterType, "MaxHp")));
            facts.Add(new FactSnapshot($"{prefix}.block", Number(session, monsters[index], monsterType, "Block")));
            facts.Add(new FactSnapshot($"{prefix}.alive", hp > 0 ? "true" : "false"));
        }

        foreach (var (collection, key) in PileKeys)
        {
            var definition = session.RuleSet.Collections.FirstOrDefault(item => item.QualifiedId == collection);
            if (definition is null)
            {
                continue;
            }

            var members = session.State.CollectionMembers(definition.Id);
            facts.Add(new FactSnapshot($"pile.{key}.size", members.Count.ToString(CultureInfo.InvariantCulture)));
            var keys = members.Select(member => CardKey(session, cardType, member)).ToList();
            facts.Add(new FactSnapshot($"pile.{key}.sorted", string.Join(",", keys.Order(StringComparer.Ordinal))));
            if (observeDrawTop && key == "draw")
            {
                facts.Add(new FactSnapshot("pile.draw.top", keys.FirstOrDefault() ?? "<empty>"));
            }
            if (exactPileOrder || key is "hand" or "exhaust")
            {
                facts.Add(new FactSnapshot($"pile.{key}.order", string.Join(",", keys)));
            }
        }

        AddPowerFacts(session, facts, player, "player");
        for (var index = 0; index < monsters.Count; index++)
        {
            AddPowerFacts(session, facts, monsters[index], $"enemy.{enemyKeys[index]}");
        }

        return Records.Facts(facts);
    }

    /// <summary>
    /// 敌人事实的键：用**内容 id**而不是存活下标。死亡后参考会把敌人移出列表、我们保留在集合里，
    /// 用下标会让"同一只敌人的血量"错位成两条无关差异，掩盖真正的事实。
    /// 同种敌人出现多次时加 <c>#n</c> 区分（顺序仍是语义）。
    /// </summary>
    public static IReadOnlyList<string> EnemyKeys(IEnumerable<string> definitionIds)
    {
        var ids = definitionIds.ToList();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var keys = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            var ordinal = seen.TryGetValue(id, out var count) ? count : 0;
            seen[id] = ordinal + 1;
            keys.Add(seen[id] > 1 || ids.Count(item => item == id) > 1 ? $"{id}#{ordinal}" : id);
        }

        return keys;
    }

    /// <summary>
    /// **活着参战的敌人**：取 `Monsters` 集合的成员，而不是"所有 Monster 类型的实例"。
    ///
    /// 这一条曾经让我误判：切片在怪物死亡时会 `RemoveFromCollection(monsters, …)`
    /// （`CombatSlicePackage.cs:665`，与 `sts2/src/Core/Commands/CreatureCmd.cs:523-531` 的"死亡后离场"一致），
    /// 但实例本身还在状态里；按类型枚举会把尸体也算成敌人，于是两侧看起来"一边保留一边移除"。
    /// 敌人身份必须按**参战集合**投影。
    /// </summary>
    private static IReadOnlyList<InstanceId> LivingEnemies(Session session)
    {
        var definition = session.RuleSet.Collections.FirstOrDefault(item => item.QualifiedId == MonstersCollection);
        return definition is not null
            ? session.State.CollectionMembers(definition.Id)
            : session.State.AllInstances(session.RuleSet.TypeByQualifiedId(MonsterType).Id);
    }

    private static void AddPowerFacts(Session session, List<FactSnapshot> facts, InstanceId owner, string ownerSlot)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId(PowerType);
        var relation = rules.Relations.FirstOrDefault(item => item.QualifiedId == PowersRelation);
        if (relation is null)
        {
            return;
        }

        var owned = session.State.RelationTargets(relation.Id, owner)
            .Where(instance => session.State.TypeIdOf(instance) == powerType.Id)
            .ToList();
        facts.Add(new FactSnapshot($"power.{ownerSlot}.count", owned.Count.ToString(CultureInfo.InvariantCulture)));
        for (var index = 0; index < owned.Count; index++)
        {
            facts.Add(new FactSnapshot($"power.{ownerSlot}.{index}.id",
                Text(session, owned[index], powerType, DefinitionField)));
            facts.Add(new FactSnapshot($"power.{ownerSlot}.{index}.stacks",
                Number(session, owned[index], powerType, "Stacks")));
        }
    }

    /// <summary>牌的身份键：内容 id（+ 升级标记）。牌区顺序是语义，因此逐位置比较。</summary>
    private static string CardKey(Session session, EntityTypeDefinition cardType, InstanceId card)
    {
        var id = Text(session, card, cardType, DefinitionField);
        var upgraded = session.State.GetField(card, cardType.FieldIndex("Upgraded")).AsBool();
        return upgraded ? id + "+" : id;
    }

    // -- 决策点 -------------------------------------------------------------------------------------------

    /// <summary>把当前待回答的决策点描述成可比形式（候选按语义槽位；回答后再补 <c>Chosen</c>）。</summary>
    public static DecisionSnapshot? DescribeDecision(Session session, ParticipantId participant)
    {
        if (session.Status != SessionStatus.AwaitingDecision)
        {
            return null;
        }

        var request = session.PendingDecision;
        if (request is null || request.Participant != participant)
        {
            return null;
        }

        var slots = AssignSlots(session);
        return new DecisionSnapshot(
            request.Kind.ToString(),
            request.Prompt,
            [.. request.Candidates.Select(candidate => SlotOf(slots, candidate))],
            request.Minimum,
            request.Maximum,
            null);
    }

    public static IReadOnlyList<StreamSnapshot> Streams(Session session) =>
    [
        .. session.Random.Diagnostics()
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => new StreamSnapshot(item.Name, item.Draws,
                "0x" + item.StateFingerprint.ToString("x16", CultureInfo.InvariantCulture))),
    ];

    public static long TotalDraws(Session session) => session.Random.TotalDraws;

    // -- 完整内部诊断（只给 base 侧看） -------------------------------------------------------------------

    public static DiagnosticSnapshot Capture(Session session)
    {
        var rules = session.RuleSet;
        var state = session.State;
        var slots = AssignSlots(session);

        var collections = new List<CollectionSnapshot>();
        foreach (var collection in rules.Collections.OrderBy(item => item.QualifiedId, StringComparer.Ordinal))
        {
            collections.Add(new CollectionSnapshot(collection.QualifiedId,
                state.CollectionMembers(collection.Id).Select(member => SlotOf(slots, member)).ToList()));
        }

        var instances = new List<InstanceSnapshot>();
        foreach (var (instance, slot) in slots.OrderBy(pair => pair.Value, StringComparer.Ordinal))
        {
            var type = rules.Type(state.TypeIdOf(instance));
            instances.Add(new InstanceSnapshot(slot, type.QualifiedId, DefinitionOf(state, type, instance),
                [.. type.Fields.Select(field => new FieldSnapshot(field.Name, Format(state.GetField(instance, field.Index), slots)))]));
        }

        return new DiagnosticSnapshot(state.Version, state.InstanceCount, state.StateHash(), collections, instances);
    }

    /// <summary>
    /// 当前合法动作：动作 id + 候选的语义槽位 + 可绑定参数组合数（ENV §6.1：候选集合本身也比较）。
    ///
    /// **候选取"可提交绑定里出现过的候选参数取值"，不是候选参数的整个定义域**：
    /// 定义域会包含当前**不可打出**的牌（能量不足、不可打出关键词…），而参考侧用的是
    /// `CardModel.CanPlay` 过滤后的集合。用定义域会把"两边对合法性判断一致"误报成差异。
    /// </summary>
    public static IReadOnlyList<ActionOption> LegalActions(Session session, ParticipantId participant)
    {
        var slots = AssignSlots(session);
        return
        [
            .. session.LegalActions(participant)
                .Select(action => new ActionOption(
                    action.Definition.QualifiedId,
                    BoundCandidates(action, slots),
                    action.Bindings.Count))
                .OrderBy(option => option.Action, StringComparer.Ordinal),
        ];
    }

    private static IReadOnlyList<string> BoundCandidates(LegalAction action,
        IReadOnlyDictionary<InstanceId, string> slots)
    {
        var index = action.Definition.CandidateParameterIndex;
        if (index < 0)
        {
            return [];
        }

        var candidates = new List<string>();
        foreach (var binding in action.Bindings)
        {
            if (index >= binding.Count)
            {
                continue;
            }

            var slot = SlotOf(slots, binding[index].AsRef());
            if (!candidates.Contains(slot, StringComparer.Ordinal))
            {
                candidates.Add(slot);
            }
        }

        return candidates;
    }

    // -- 语义槽位 -----------------------------------------------------------------------------------------

    /// <summary>语义槽位名 → 实例的反向映射：夹具脚本用槽位名指代实例（如 <c>enemy:0</c>、<c>card:discard:0</c>）。</summary>
    public static IReadOnlyDictionary<string, InstanceId> SlotMap(Session session)
    {
        var map = new Dictionary<string, InstanceId>(StringComparer.Ordinal);
        foreach (var (instance, slot) in AssignSlots(session))
        {
            map[slot] = instance;
        }

        return map;
    }

    private static Dictionary<InstanceId, string> AssignSlots(Session session)
    {
        var rules = session.RuleSet;
        var state = session.State;
        var slots = new Dictionary<InstanceId, string>();

        var players = state.AllInstances(rules.TypeByQualifiedId(PlayerType).Id);
        for (var index = 0; index < players.Count; index++)
        {
            slots[players[index]] = index == 0 ? "player" : $"player:{index}";
        }

        var monsters = LivingEnemies(session);
        for (var index = 0; index < monsters.Count; index++)
        {
            slots[monsters[index]] = $"enemy:{index}";
        }

        foreach (var pile in OrderedPiles)
        {
            var definition = rules.Collections.FirstOrDefault(item => item.QualifiedId == pile);
            if (definition is null)
            {
                continue;
            }

            var pileKey = pile[(pile.LastIndexOf(':') + 1)..].Replace("Pile", string.Empty).ToLowerInvariant();
            var members = state.CollectionMembers(definition.Id);
            for (var index = 0; index < members.Count; index++)
            {
                slots[members[index]] = $"card:{pileKey}:{index}";
            }
        }

        var powerTypeId = rules.TypeByQualifiedId(PowerType).Id;
        var powersRelation = rules.Relations.FirstOrDefault(item => item.QualifiedId == PowersRelation);
        if (powersRelation is not null)
        {
            foreach (var owner in slots.Keys.ToList())
            {
                if (state.TypeIdOf(owner) == powerTypeId)
                {
                    continue;
                }

                var owned = state.RelationTargets(powersRelation.Id, owner);
                for (var index = 0; index < owned.Count; index++)
                {
                    slots[owned[index]] = $"power:{slots[owner]}:{index}";
                }
            }
        }

        foreach (var instance in state.AllInstances())
        {
            if (slots.ContainsKey(instance))
            {
                continue;
            }

            var typeId = state.TypeIdOf(instance);
            var ordinal = state.AllInstances(typeId).TakeWhile(candidate => candidate != instance).Count();
            slots[instance] = $"other:{rules.Type(typeId).QualifiedId}:{ordinal}";
        }

        return slots;
    }

    private static string SlotOf(IReadOnlyDictionary<InstanceId, string> slots, InstanceId instance) =>
        slots.TryGetValue(instance, out var slot) ? slot : $"unmapped:{instance.Value}";

    private static InstanceId Player(Session session) =>
        session.State.AllInstances(session.RuleSet.TypeByQualifiedId(PlayerType).Id).Single();

    private static string Number(Session session, InstanceId instance, EntityTypeDefinition type, string field) =>
        session.State.GetField(instance, type.FieldIndex(field)).AsInt().ToString(CultureInfo.InvariantCulture);

    private static string Text(Session session, InstanceId instance, EntityTypeDefinition type, string field) =>
        session.State.GetField(instance, type.FieldIndex(field)).AsString();

    private static string DefinitionOf(SessionState state, EntityTypeDefinition type, InstanceId instance) =>
        type.TryFieldIndex(DefinitionField, out var index) ? state.GetField(instance, index).AsString() : string.Empty;

    private static string Format(Value value, IReadOnlyDictionary<InstanceId, string> slots) => value.Kind switch
    {
        ValueKind.None => "none",
        ValueKind.Bool => value.AsBool() ? "true" : "false",
        ValueKind.Int => value.AsInt().ToString(CultureInfo.InvariantCulture),
        ValueKind.Fixed => value.AsDecimal().ToString(CultureInfo.InvariantCulture),
        ValueKind.Enum => "enum:" + value.AsEnumOrdinal().ToString(CultureInfo.InvariantCulture),
        ValueKind.String => value.AsString(),
        ValueKind.Ref => "ref:" + SlotOf(slots, value.AsRef()),
        ValueKind.Participant => "participant:" + value.AsParticipant().Value.ToString(CultureInfo.InvariantCulture),
        ValueKind.Scope => "scope:" + value.AsScope().Value.ToString(CultureInfo.InvariantCulture),
        ValueKind.RefList => "refs:[" + string.Join(",", value.AsRefList().Select(item => SlotOf(slots, item))) + "]",
        ValueKind.IntList => "ints:[" + string.Join(",",
            value.AsIntList().Select(item => item.ToString(CultureInfo.InvariantCulture))) + "]",
        _ => "kind:" + value.Kind.ToString(),
    };
}
