using BaseSpire.Slice;

namespace BaseSpire.SinglePlayer;

/// <summary>
/// 正式单人构建的**内容组装 + 范围结算**结果（只读）。
///
/// 五个状态分开：声明 / 解析 / 降级 / 未支持（逐条原因）/ 实际进包。调用方（工厂/构建描述）
/// 只读它，不得再自行改内容。
/// </summary>
public sealed record SinglePlayerContent(
    SliceAuthorModel Model,
    IReadOnlyList<string> CardIds,
    IReadOnlyList<string> PowerIds,
    IReadOnlyList<string> EnemyIds,
    /// <summary>进池的弱遭遇（来源 `isWeak = true` 且敌人全部可执行）。</summary>
    IReadOnlyList<SliceEncounter> WeakEncounters,
    /// <summary>进池的普通遭遇。</summary>
    IReadOnlyList<SliceEncounter> NormalEncounters,
    /// <summary>正式源里的幕末 Boss 遭遇。</summary>
    IReadOnlyList<SliceEncounter> BossEncounters,
    /// <summary>被排除的遭遇 + 原因（不是"没这回事"，是"还没实现"）。</summary>
    IReadOnlyList<(string Item, string Reason)> ExcludedEncounters,
    /// <summary>正式源里未支持的内容 + 原因。</summary>
    IReadOnlyList<(string Item, string Reason)> Unsupported,
    /// <summary>实际加载的每个源资源的内容摘要（R3：让"声称的来源"与"真正加载的东西"可对照）。</summary>
    IReadOnlyList<(string Resource, string Digest)> SourceDigests,
    /// <summary>本局开局的起始遗物声明（来自角色注册表，效果已按实现范围校验）。</summary>
    IReadOnlyList<SliceRelic> StartingRelics,
    /// <summary>幕开头从弱池取的房间数（来源 `NumberOfWeakEncounters`）。</summary>
    int WeakRoomCount,
    /// <summary>整幕普通遭遇队列长度（来源 `GetNumberOfRooms(false)` = `BaseNumberOfRooms`）。</summary>
    int RoomCount)
{
    /// <summary>全部实际加载内容的聚合摘要（顺序稳定：按资源名排序）。</summary>
    public string ContentDigest => EncounterContent.Digest(string.Join("\n",
        SourceDigests.OrderBy(entry => entry.Resource, StringComparer.Ordinal)
            .Select(entry => $"{entry.Resource}={entry.Digest}")));

    public SliceEncounterPool EncounterPool =>
        new([.. WeakEncounters], [.. NormalEncounters], WeakRoomCount, RoomCount,
            VanillaRandomLayout.EngineStreamName("UpFront"),
            VanillaRandomLayout.EngineStreamName("Shuffle"),
            [.. BossEncounters]);
}

/// <summary>
/// 正式内容的**唯一组装点**：卡牌 / 能力 / 敌人 / 遭遇池 / 角色开局，全部来自正式源。
///
/// 硬性边界（BASE-01）：
/// <list type="bullet">
/// <item>不再读取冻结的 `.author` 原型文本——敌人、药水、run 段都不再来自它；</item>
/// <item>不再使用"每张降级卡各一张"的诊断牌组——起始牌组来自
///       <see cref="BaseSpireCharacters"/>（原版角色起始配置）；</item>
/// <item>不再使用固定遭遇——战斗房间的敌人由**正式遭遇池**在运行时决定；</item>
/// <item>任何"能声明但运行时执行不了"的内容（敌人行动、能力、遭遇形态）**逐条记账**，
///       既不出现在包里，也不被静默当成已支持。</item>
/// </list>
/// </summary>
public static class SinglePlayerContentSource
{
    /// <summary>组装正式内容。同一个进程里缓存（只读、可共享：不同会话不共享状态）。</summary>
    private static readonly Lazy<SinglePlayerContent> Cached = new(Build, isThreadSafe: true);

    public static SinglePlayerContent Shared => Cached.Value;

    private static SinglePlayerContent Build()
    {
        var unsupported = new List<(string Item, string Reason)>();
        // R3：把**实际读到**的每个源资源的内容摘要记下来。没有这一步，"绑定了某一源基线"只是标签。
        var sourceDigests = new List<(string Resource, string Digest)>();

        // ① 能力：正式 `*-powers.spire` **以及** `enemies*.spire` 里的 `power` 声明。
        //    敌人行动会 `applyPower(Ritual, …)` 一类，只加载 `-powers.spire` 会让这些敌人变成
        //    "引用了没加载的能力"，从而整条被记为未支持——那不是内容缺失，是源筛选错了。
        var powers = new List<AuthorPower>();
        var powerResources = SpirePowerSource.PowerDeclarationResources();
        foreach (var resource in powerResources)
        {
            sourceDigests.Add((resource, EncounterContent.Digest(EncounterContent.ReadText(resource))));
            var source = SpirePowerSource.LoadResource(resource);
            powers.AddRange(source.Powers);
            unsupported.AddRange(source.Unsupported.Select(entry =>
                ($"power:{entry.Name}", $"{source.Name}: {entry.Reason}")));
        }

        // 链接表：正式能力名 → 正式 id（由**已加载**的能力给出，不手写）。
        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var power in powers)
        {
            links.TryAdd(power.Name, power.Id);
        }

        var potionSource = SpirePotionSource.Load(links);
        sourceDigests.Add((SpirePotionSource.Resource,
            EncounterContent.Digest(EncounterContent.ReadText(SpirePotionSource.Resource))));
        unsupported.AddRange(potionSource.Pool.Where(p => p.UnsupportedUseReason.Length > 0)
            .Select(p => ($"potion:{p.Name}", p.UnsupportedUseReason)));

        // ② 卡牌：正式 `*-cards.spire`。
        var cards = new List<AuthorCard>();
        foreach (var resource in SpireCardSource.CardResources())
        {
            sourceDigests.Add((resource, EncounterContent.Digest(EncounterContent.ReadText(resource))));
            var source = SpireCardSource.LoadResource(resource, links);
            cards.AddRange(source.Cards);
            unsupported.AddRange(source.Unsupported.Select(entry =>
                ($"card:{entry.Name}", $"{source.Name}: {entry.Reason}")));
        }

        // 卡名 → 正式 id（敌人的 `addCards` 按它解析；与能力同一套"只认已加载内容"的规则）。
        var cardLinks = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            cardLinks.TryAdd(card.Name, card.Id);
        }

        // ③ 敌人：正式 `enemies*.spire`。引用未加载能力/卡牌的敌人整条记为未支持。
        var enemies = new List<AuthorEnemy>();
        foreach (var resource in SpireEnemySource.EnemyResources())
        {
            sourceDigests.Add((resource, EncounterContent.Digest(EncounterContent.ReadText(resource))));
            var source = SpireEnemySource.LoadResource(resource, links, cardLinks);
            enemies.AddRange(source.Enemies);
            unsupported.AddRange(source.Unsupported.Select(entry =>
                ($"enemy:{entry.Name}", $"{source.Name}: {entry.Reason}")));
        }

        var enemyById = enemies.ToDictionary(enemy => enemy.Id, StringComparer.Ordinal);
        var cardIds = new HashSet<string>(cards.Select(card => card.Id), StringComparer.Ordinal);

        // ④ 遭遇池：正式 JSON，只收"敌人全部可执行且形态唯一"的遭遇。
        foreach (var resource in new[] { EncounterContent.EncountersResource, EncounterContent.ActsResource })
        {
            sourceDigests.Add((resource, EncounterContent.Digest(EncounterContent.ReadText(resource))));
        }

        var declarations = EncounterContent.LoadEncounters().ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var acts = EncounterContent.LoadActPools();
        var act = acts.FirstOrDefault() ?? throw new InvalidOperationException(
            $"{EncounterContent.ActsSourcePath} 里没有任何幕池：正式构建需要幕与遭遇池才能开局");
        var weak = new List<SliceEncounter>();
        var normal = new List<SliceEncounter>();
        var bosses = new List<SliceEncounter>();
        var excluded = new List<(string Item, string Reason)>();
        foreach (var encounterId in act.WeakNormalEncounters.Concat(act.RegularEncounters))
        {
            if (!declarations.TryGetValue(encounterId, out var declaration))
            {
                excluded.Add((encounterId, "幕池引用了 encounters.json 里没有的遭遇：源内容不一致"));
                continue;
            }

            var reason = ExclusionReason(declaration, enemyById);
            if (reason.Length > 0)
            {
                excluded.Add((encounterId, reason));
                continue;
            }

            var target = declaration.IsWeak ? weak : normal;
            // 固定阵容用 `EnemyIds`；带编队的遭遇把**编队程序**（权重/槽位/唯一组）一起带上——
            // 敌人在出生时按遭遇派生流抽取，不再在构建期塌成一份名单。
            target.Add(new SliceEncounter(declaration.Id, declaration.IsWeak
                ? SliceEncounterPool.WeakKind
                : SliceEncounterPool.NormalKind,
                declaration.AllPossibleEnemyIds,
                declaration.Tags,
                declaration.Formations,
                declaration.Slots));
        }

        foreach (var encounterId in act.BossEncounters)
        {
            if (!declarations.TryGetValue(encounterId, out var declaration))
            {
                excluded.Add((encounterId, "幕池引用了 encounters.json 里没有的 Boss 遭遇：源内容不一致"));
                continue;
            }

            var reason = ExclusionReason(declaration, enemyById, allowBoss: true);
            if (reason.Length > 0)
            {
                excluded.Add((encounterId, reason));
                continue;
            }

            bosses.Add(new SliceEncounter(declaration.Id, "boss", declaration.AllPossibleEnemyIds,
                declaration.Tags, declaration.Formations, declaration.Slots));
        }

        if (weak.Count == 0 || normal.Count == 0)
        {
            throw new InvalidOperationException(
                $"正式遭遇池可用内容不足（弱 {weak.Count} / 普通 {normal.Count}）："
                + "没有可开局遭遇就不能建正式会话。已排除项见构建描述。");
        }

        // ⑤ 起始牌组 / run 初值：角色注册表（正式起始配置），不是 `.author`，也不是诊断牌组。
        //
        // R2：**牌序是规则的一部分**。原版按声明顺序加牌（`Ironclad.cs:43-55` 的
        // Strike×5 → Defend×4 → Bash），`PopulateCombatState` 再按 `Deck.Cards` 顺序复制进战斗牌堆、
        // 之后才洗牌（`CardPile.RandomizeOrderInternal`）。因此这里：
        //   ① 不做任何按内容 id 的排序；
        //   ② 用 GroupBy（保留**首次出现**顺序）+ 计数压缩成 `deck { … xN }`，并**当场验证**
        //      "展开后逐位等于声明顺序"——压缩不改变实际顺序这件事不能只靠注释声称。
        var character = BaseSpireCharacters.ById(BaseSpireCharacters.IroncladId);
        var missing = character.Deck.Where(cardId => !cardIds.Contains(cardId)).Distinct(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"角色 {character.Name} 的起始牌组引用了未加载的卡：{string.Join(", ", missing)}");
        }

        var deckEntries = character.Deck
            .GroupBy(cardId => cardId, StringComparer.Ordinal)
            .Select(group => new AuthorDeckEntry(group.Key, group.Count()))
            .ToList();
        var expanded = deckEntries.SelectMany(entry => Enumerable.Repeat(entry.CardId, entry.Count)).ToList();
        if (!expanded.SequenceEqual(character.Deck, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"起始牌组的压缩改变了实际顺序：声明 [{string.Join(",", character.Deck)}] "
                + $"→ 展开 [{string.Join(",", expanded)}]。牌序参与洗牌输入，不能排序或重排");
        }

        // 起始遗物：只接受正式声明表里登记、且触发点/种类在实现范围内的遗物。
        var startingRelics = new List<SliceRelic>();
        foreach (var relicId in character.Relics)
        {
            var declared = SpireRelics.ById(relicId);
            var reason = SpireRelics.UnsupportedReason(declared);
            if (reason.Length > 0)
            {
                throw new InvalidOperationException($"起始遗物 {relicId} 无法在正式构建里实现：{reason}");
            }

            startingRelics.Add(declared);
        }

        var model = new SliceAuthorModel(
            "base-spire",
            BaseSpireScope.Dev,
            deckEntries,
            cards,
            powers,
            enemies,
            potionSource.Potions,
            [],
            new AuthorRun(character.MaxHp, character.MaxHp, character.Gold))
            { PotionPool = potionSource.Pool };

        return new SinglePlayerContent(
            model,
            [.. cards.Select(card => card.Id).Order(StringComparer.Ordinal)],
            [.. powers.Select(power => power.Id).Order(StringComparer.Ordinal)],
            [.. enemies.Select(enemy => enemy.Id).Order(StringComparer.Ordinal)],
            weak,
            normal,
            bosses,
            [.. excluded.OrderBy(entry => entry.Item, StringComparer.Ordinal)],
            [.. unsupported.OrderBy(entry => entry.Item, StringComparer.Ordinal)],
            sourceDigests,
            startingRelics,
            // 队列长度与弱遭遇数取**正式幕声明**，不取"已支持子集的长度"：
            // Overgrowth 是 3 条弱 + 补齐到 15 条普通队列（`Overgrowth.cs:45,47`），
            // 不能写成 3 + 15，也不能因为内容未齐就把房间数缩成池大小。
            act.WeakNormalRoomCount,
            act.NormalRoomCount);
    }

    /// <summary>一条遭遇能不能进池；返回空串表示可以，否则是具体原因（写进构建描述）。</summary>
    private static string ExclusionReason(EncounterDeclaration declaration,
        IReadOnlyDictionary<string, AuthorEnemy> enemies, bool allowBoss = false)
    {
        if (declaration.RoomType != "monster"
            && !(allowBoss && declaration.RoomType == "boss"))
        {
            return $"房间类型 '{declaration.RoomType}' 未接入（目前只有普通战斗房间）";
        }

        var possible = declaration.AllPossibleEnemyIds;
        if (possible.Count == 0)
        {
            return "没有 enemyIds 也没有可用的编队：无法确定敌人";
        }

        var missing = possible.Where(enemyId => !enemies.ContainsKey(enemyId)).ToList();
        if (missing.Count > 0)
        {
            return $"敌人 '{string.Join(", ", missing)}' 未加载"
                + "（它的行动程序或引用的能力/卡牌尚未支持）";
        }

        foreach (var formation in declaration.Formations)
        {
            if (formation.EnemyIds.Count == 0 && formation.Slots.Count == 0)
            {
                return $"formations 里有一条既没有 enemyIds 也没有 enemySlots：无法确定阵容";
            }
        }

        return string.Empty;
    }
}
