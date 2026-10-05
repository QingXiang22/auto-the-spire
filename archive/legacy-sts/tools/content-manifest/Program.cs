using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BaseSpire;
using CoreTheSpire;

namespace ContentManifest;

/// <summary>
/// B-05a 的机器产物：把 **base-spire 当前实际注册结果** 冻结成迁移 manifest。
///
/// 三件事分开报，绝不合并分母：
/// <list type="number">
/// <item><b>源文件侧</b>：每个内容源文件（嵌入的 <c>.spire</c> 与声明内容的 C# 文件）的路径、sha256、字节数，
/// 以及它**声明**的 ID 列表；</item>
/// <item><b>注册侧</b>：走真实注册路径（<c>new BaseSpirePackage()</c> → <c>ContentRegistry.Build()</c>）后
/// 每个类别**实际注册**的声明及其属性（升级形态、多人专属、依赖的 hook/原语）；</item>
/// <item><b>差异</b>：声明但未注册（不可达）、注册但不在任何源文件里的项，逐条列出——不静默删掉难项。</item>
/// </list>
///
/// 用法：<c>dotnet run --project tools/content-manifest -- [输出路径]</c>（缺省打印到 stdout）。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var registry = new ContentRegistry();
        registry.AddPackage(new BaseSpirePackage());
        var game = registry.Build();

        var manifest = new JsonObject
        {
            ["manifest_version"] = "b05a-content-manifest@1",
            ["package"] = "base-spire",
            ["registered_from"] = "new BaseSpirePackage() -> ContentRegistry.Build()",
        };

        var sources = new JsonArray();
        var declaredBySource = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var declaredIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, bytes) in EmbeddedSources())
        {
            var declarations = DeclarationsIn(name, bytes);
            var path = "base-spire/" + name["BaseSpire.ContentSource.".Length..];
            var entry = new JsonObject
            {
                ["path"] = path,
                ["sha256"] = Sha256(bytes),
                ["bytes"] = bytes.Length,
                ["declared_count"] = declarations.Count,
                ["declared_ids"] = new JsonArray(declarations.Order(StringComparer.Ordinal)
                    .Select(id => (JsonNode)JsonValue.Create(id)).ToArray()),
            };
            sources.Add(entry);
            foreach (var id in declarations)
            {
                declaredIds.Add(id);
                if (!declaredBySource.TryGetValue(id, out var list))
                {
                    declaredBySource[id] = list = [];
                }

                list.Add(path);
            }
        }

        // 声明内容的 C# 文件（这里只登记文件与摘要：它们的声明由注册结果给出）。
        var codeFiles = new JsonArray();
        foreach (var relative in new[]
                 {
                     "mod-the-spire/base-spire/BaseSpire/BaseSpirePackage.cs",
                     "mod-the-spire/base-spire/BaseSpire/IroncladCardPool.cs",
                     "mod-the-spire/base-spire/BaseSpire/StandardActMapGenerator.cs",
                 })
        {
            var full = Path.Combine(RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(full);
            codeFiles.Add(new JsonObject
            {
                ["path"] = relative,
                ["sha256"] = Sha256(bytes),
                ["bytes"] = bytes.Length,
            });
        }

        manifest["sources"] = sources;
        manifest["code_sources"] = codeFiles;

        // -- 注册侧 -----------------------------------------------------------------------------------------
        var categories = new JsonObject();
        var registeredIds = new HashSet<string>(StringComparer.Ordinal);

        var cards = new JsonArray();
        foreach (var card in game.Cards.Values.OrderBy(card => card.Id.Value, StringComparer.Ordinal))
        {
            registeredIds.Add(card.Id.Value);
            cards.Add(new JsonObject
            {
                ["id"] = card.Id.Value,
                ["source"] = SourceOf(declaredBySource, card.Id.Value),
                ["cost"] = card.Cost,
                ["cost_kind"] = card.CostKind.ToString(),
                ["type"] = card.Type.ToString(),
                ["rarity"] = card.Rarity.ToString(),
                ["has_upgrade"] = card.HasUpgrade,
                ["upgraded_cost"] = card.UpgradedCost,
                ["upgraded_effect"] = card.UpgradedEffect is not null,
                ["multiplayer_only"] = card.MultiplayerOnly,
                ["can_be_generated_in_combat"] = card.CanBeGeneratedInCombat,
                ["keywords"] = card.Keywords.ToString(),
                ["target"] = card.Target.ToString(),
                ["tags"] = Strings(card.Tags.Select(tag => tag.Value)),
                ["pools"] = Strings(card.PoolIds.Select(pool => pool.Value)),
                ["variables"] = card.Variables.Count,
                ["hooks"] = Hooks(card),
            });
        }

        var powers = new JsonArray();
        foreach (var power in game.Powers.Values.OrderBy(power => power.Id.Value, StringComparer.Ordinal))
        {
            registeredIds.Add(power.Id.Value);
            powers.Add(new JsonObject
            {
                ["id"] = power.Id.Value,
                ["source"] = SourceOf(declaredBySource, power.Id.Value),
                ["is_temporary"] = power.IsTemporary,
                ["events"] = power.Events.Count,
                ["lifecycle"] = power.Lifecycle.Count,
                ["incoming_damage_modifiers"] = power.IncomingDamageModifiers.Count,
                ["block_gain_modifiers"] = power.BlockGainModifiers.Count,
                ["variables"] = power.Variables.Count,
            });
        }

        var enemies = new JsonArray();
        foreach (var enemy in game.Enemies.Values.OrderBy(enemy => enemy.Id.Value, StringComparer.Ordinal))
        {
            registeredIds.Add(enemy.Id.Value);
            enemies.Add(new JsonObject
            {
                ["id"] = enemy.Id.Value,
                ["source"] = SourceOf(declaredBySource, enemy.Id.Value),
                ["min_hp"] = enemy.MinHp,
                ["max_hp"] = enemy.MaxHp,
                ["actions"] = enemy.Actions.Count,
            });
        }

        categories["cards"] = cards;
        categories["powers"] = powers;
        categories["enemies"] = enemies;
        categories["afflictions"] = Ids(game.Afflictions.Keys, declaredBySource, registeredIds);
        categories["potions"] = Ids(game.Potions.Keys, declaredBySource, registeredIds);
        categories["card_pools"] = Ids(game.CardPools.Keys, declaredBySource, registeredIds);
        categories["relics"] = Ids(game.Relics.Keys, declaredBySource, registeredIds);
        categories["encounters"] = Ids(game.Encounters.Keys, declaredBySource, registeredIds);
        categories["run_events"] = Ids(game.RunEvents.Keys, declaredBySource, registeredIds);
        categories["characters"] = Ids(game.Characters.Keys, declaredBySource, registeredIds);
        categories["runs"] = Ids(game.Runs.Keys, declaredBySource, registeredIds);
        // 状态变量必须与其它类别走**同一套记账**：漏加进 registered 集合会让它们被误报成"声明但未注册"，
        // 也会让注册总数少算（B-05a 要求三个数分别列出且互不污染）。
        var stateVariables = new JsonArray();
        foreach (var variable in game.StateVariables.Values
                     .OrderBy(variable => variable.Id.Value, StringComparer.Ordinal))
        {
            registeredIds.Add(variable.Id.Value);
            stateVariables.Add(new JsonObject
            {
                ["id"] = variable.Id.Value,
                ["scope"] = variable.Scope.ToString(),
                ["type"] = variable.Type.ToString(),
                ["source"] = SourceOf(declaredBySource, variable.Id.Value),
            });
        }

        categories["state_variables"] = stateVariables;
        manifest["categories"] = categories;

        // -- 计数（分别列出，不合并分母） ---------------------------------------------------------------------
        var totals = new JsonObject
        {
            ["declared_in_sources"] = declaredIds.Count,
            ["registered_total"] = registeredIds.Count,
            ["cards_registered"] = game.Cards.Count,
            ["powers_registered"] = game.Powers.Count,
            ["enemies_registered"] = game.Enemies.Count,
            ["card_pools_registered"] = game.CardPools.Count,
            ["relics_registered"] = game.Relics.Count,
            ["potions_registered"] = game.Potions.Count,
            ["characters_registered"] = game.Characters.Count,
            ["runs_registered"] = game.Runs.Count,
            ["encounters_registered"] = game.Encounters.Count,
            ["run_events_registered"] = game.RunEvents.Count,
            ["afflictions_registered"] = game.Afflictions.Count,
            ["state_variables_registered"] = game.StateVariables.Count,
            ["ironclad_card_pool_size"] = game.CardPools.TryGetValue(BaseSpireIds.IroncladCardPool, out var pool)
                ? pool.Cards.Count
                : 0,
            ["multiplayer_only_cards"] = game.Cards.Values.Count(card => card.MultiplayerOnly),
        };
        manifest["totals"] = totals;

        // -- 差异：不可达 / 无来源 ---------------------------------------------------------------------------
        var unreachable = declaredIds.Where(id => !registeredIds.Contains(id)).Order(StringComparer.Ordinal).ToList();
        var unsourced = registeredIds.Where(id => !declaredBySource.ContainsKey(id)).Order(StringComparer.Ordinal).ToList();
        manifest["differences"] = new JsonObject
        {
            ["declared_but_not_registered"] = Strings(unreachable),
            ["registered_without_spire_source"] = Strings(unsourced),
            ["note"] = "registered_without_spire_source 多为 C# 声明的内容（遗物/角色/流程）或由包在构建期展开的 ID；" +
                       "declared_but_not_registered 是**不可达声明**，必须显式处理，不得为了让分母变小而删掉源文件。",
        };

        // -- 范围分组（首个单人 Ironclad 范围） ---------------------------------------------------------------
        var ironcladCards = game.Cards.Values
            .Where(card => card.PoolIds.Any(pool => pool.Value == BaseSpireIds.IroncladCardPool.Value))
            .Select(card => card.Id.Value).Order(StringComparer.Ordinal).ToList();
        var statusCards = game.Cards.Values
            .Where(card => card.PoolIds.Count == 0)
            .Select(card => card.Id.Value).Order(StringComparer.Ordinal).ToList();
        manifest["scope"] = new JsonObject
        {
            ["ironclad_single_player_cards"] = Strings(ironcladCards),
            ["cards_without_pool"] = Strings(statusCards),
            ["multiplayer_only_cards"] = Strings(game.Cards.Values.Where(card => card.MultiplayerOnly)
                .Select(card => card.Id.Value).Order(StringComparer.Ordinal)),
        };

        var json = manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        if (args.Length > 0)
        {
            File.WriteAllText(args[0], json, new UTF8Encoding(false));
            Console.Error.WriteLine($"content manifest written: {args[0]}");
        }
        else
        {
            Console.WriteLine(json);
        }

        return 0;
    }

    // -- 源文件 ---------------------------------------------------------------------------------------------

    private static IEnumerable<(string Name, byte[] Bytes)> EmbeddedSources()
    {
        var assembly = typeof(BaseSpireContent).Assembly;
        foreach (var name in assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith("BaseSpire.ContentSource.", StringComparison.Ordinal)
                         && name.EndsWith(".spire", StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            yield return (name, memory.ToArray());
        }
    }

    private static List<string> DeclarationsIn(string resourceName, byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var path = "base-spire/" + resourceName["BaseSpire.ContentSource.".Length..];
        var package = DslSourceParser.Parse(text, path);
        var ids = new List<string>();
        try
        {
            if (resourceName.EndsWith("-cards.spire", StringComparison.Ordinal))
            {
                ids.AddRange(SpireCombatDslCompiler.CompileCards(package).Select(card => card.Id.Value));
                ids.AddRange(SpireCombatDslCompiler.CompileCardPools(package).Select(pool => pool.Id.Value));
            }
            else if (resourceName.EndsWith("potions.spire", StringComparison.Ordinal))
            {
                ids.AddRange(SpireCombatDslCompiler.CompilePotions(package).Select(potion => potion.Id.Value));
            }
            else if (resourceName.EndsWith("state.spire", StringComparison.Ordinal))
            {
                ids.AddRange(SpireStateDslCompiler.Compile(package).Select(variable => variable.Id.Value));
            }
            else
            {
                var behavior = SpireCombatBehaviorDslCompiler.Compile(package);
                ids.AddRange(behavior.Powers.Select(power => power.Id.Value));
                ids.AddRange(behavior.Enemies.Select(enemy => enemy.Id.Value));
                ids.AddRange(behavior.Afflictions.Select(affliction => affliction.Id.Value));
            }
        }
        catch (Exception failure)
        {
            ids.Add($"<parse-failed: {failure.GetType().Name}>");
        }

        return ids;
    }

    private static JsonArray Hooks(CardDefinition card)
    {
        var hooks = new List<string>();
        if (card.OnExhaust is not null || card.UpgradedOnExhaust is not null) hooks.Add("on_exhaust");
        if (card.TurnEndInHand is not null || card.UpgradedTurnEndInHand is not null) hooks.Add("turn_end_in_hand");
        if (card.OnEnterCombat is not null || card.UpgradedOnEnterCombat is not null) hooks.Add("on_enter_combat");
        if (card.BeforeCardPlayed is not null || card.UpgradedBeforeCardPlayed is not null) hooks.Add("before_card_played");
        if (card.AfterCardPlayed is not null || card.UpgradedAfterCardPlayed is not null) hooks.Add("after_card_played");
        if (card.AfterAutoPostPlay is not null || card.UpgradedAfterAutoPostPlay is not null) hooks.Add("after_auto_post_play");
        return Strings(hooks);
    }

    private static JsonArray Ids(IEnumerable<ContentId> ids,
        IReadOnlyDictionary<string, List<string>> sources, ISet<string> registered)
    {
        var array = new JsonArray();
        foreach (var id in ids.Select(item => item.Value).Order(StringComparer.Ordinal))
        {
            registered.Add(id);
            array.Add(new JsonObject
            {
                ["id"] = id,
                ["source"] = SourceOf(sources, id),
            });
        }

        return array;
    }

    private static string SourceOf(IReadOnlyDictionary<string, List<string>> sources, string id) =>
        sources.TryGetValue(id, out var list) ? list[0] : "<csharp-declared>";

    private static JsonArray Strings(IEnumerable<string> values) =>
        new(values.Order(StringComparer.Ordinal).Select(value => (JsonNode)JsonValue.Create(value)).ToArray());

    private static string Sha256(byte[] bytes) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AutoTheSpire.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }
}
