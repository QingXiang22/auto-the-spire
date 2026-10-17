using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Definition recipes and explicit copy policies. Used by setup, rewards and card commands.</summary>
public sealed class CardDefinitions(CardSchema schema, EnumTypeSymbol kind, SliceAuthorModel? source,
    IReadOnlyDictionary<string, FieldSymbol> variables)
{
    /// <summary>本配方使用的卡牌实体字段表（商店等其它房间建长期卡实例时复用，不另建卡实体）。</summary>
    public CardSchema Schema { get; } = schema;

    public IReadOnlyList<CardDefinition> Compiled { get; } =
        [.. (source?.Cards ?? []).Select(CardDefinition.FromSource)];

    /// <summary>
    /// 定义 id → 源排序键里的 **Entry**（源 `ModelDb.GetEntry` = `StringHelper.Slugify(类型名)`）。
    /// 表由**声明名**算出（正式 `.spire` 的卡名就是源类名），比内容 id 的逆向转写精确：
    /// 连续大写结尾之类的内容 id 会丢掉词边界。表里没有的定义回退到
    /// <see cref="SourceStableShuffle.FallbackEntry"/>（当前内容两者一致，见 `SourceStableShuffleChecks`）。
    /// </summary>
    private readonly Dictionary<string, string> sourceEntries = BuildSourceEntries(source);

    public string SourceEntryOf(string id) =>
        sourceEntries.TryGetValue(id, out var entry) ? entry : SourceStableShuffle.FallbackEntry(id);

    /// <summary>该定义的源排序键（`Entry@升级等级`），创建/升级路径统一从这里取。</summary>
    public string SourceOrderKeyOf(string id, bool upgraded) =>
        SourceStableShuffle.Key(SourceEntryOf(id), upgraded);

    private static Dictionary<string, string> BuildSourceEntries(SliceAuthorModel? model)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var card in model?.Cards ?? [])
        {
            entries[card.Id] = SourceStableShuffle.Slugify(card.Name);
        }

        return entries;
    }

    public CardDefinition Definition(string id) => Compiled.FirstOrDefault(card => card.Id == id)
        ?? throw Faults.Schema($"Card definition '{id}' is not registered");
    public (FieldSymbol Field, Expr Value)[] Fields(IrScope scope, SliceCard card, bool upgraded,
        bool unplayable = false, bool ethereal = false) =>
    [
        (schema.Definition, scope.Const(card.ContentId)), (schema.Kind, scope.EnumConst(kind, card.Kind)),
        (schema.Upgraded, scope.Const(upgraded)), (schema.Unplayable, scope.Const(unplayable)),
        (schema.Ethereal, scope.Const(ethereal)),
        (schema.Cost, scope.Const((long)(upgraded ? card.EffectiveUpgradedCost : card.Cost))),
        (schema.CostModifier, scope.Const(0L)),
        (schema.Amount, scope.Const((long)(upgraded ? card.UpgradedAmount : card.NormalAmount))),
        (schema.PowerId, scope.Const(card.AppliedPowerId)),
        (schema.PowerStacks, scope.Const((long)(upgraded ? card.UpgradedPowerStacks : card.NormalPowerStacks))),
        (schema.SourceOrderKey, scope.Const(SourceStableShuffle.Key(
            SourceStableShuffle.Slugify(card.Name), upgraded))),
    ];

    public (FieldSymbol Field, Expr Value)[] New(IrScope scope, AuthorCard card, bool upgraded)
        => New(scope, Definition(card.Id), upgraded);

    public (FieldSymbol Field, Expr Value)[] New(IrScope scope, CardDefinition card, bool upgraded)
    {
        var variant = card.Variant(upgraded);
        return
        [
            (schema.Definition, scope.Const(card.Id)), (schema.Kind, scope.EnumConst(kind, card.Kind)),
            (schema.Upgraded, scope.Const(upgraded)), (schema.Unplayable, scope.Const(variant.Unplayable)),
            (schema.Ethereal, scope.Const(variant.Ethereal)), (schema.Cost, scope.Const((long)variant.Cost)),
            (schema.CostModifier, scope.Const(0L)), (schema.Amount, scope.Const((long)variant.DisplayAmount)),
            (schema.PowerId, scope.Const(variant.DisplayPowerId)), (schema.PowerStacks, scope.Const((long)variant.DisplayStacks)),
            (schema.SourceOrderKey, scope.Const(SourceOrderKeyOf(card.Id, upgraded))),
            .. variables.Select(entry => (entry.Value, scope.Const((long)variant.Variables.GetValueOrDefault(entry.Key)))),
        ];
    }

    public (FieldSymbol Field, Expr Value)[] Copy(IrScope scope, Expr card, bool clone) =>
    [
        (schema.Definition, scope.Field(card, schema.Definition)), (schema.Kind, scope.Field(card, schema.Kind)),
        (schema.Upgraded, scope.Field(card, schema.Upgraded)), (schema.Unplayable, scope.Field(card, schema.Unplayable)),
        (schema.Ethereal, scope.Field(card, schema.Ethereal)), (schema.Cost, scope.Field(card, schema.Cost)),
        (schema.CostModifier, clone ? scope.Field(card, schema.CostModifier) : scope.Const(0L)),
        (schema.Amount, scope.Field(card, schema.Amount)), (schema.PowerId, scope.Field(card, schema.PowerId)),
        (schema.PowerStacks, scope.Field(card, schema.PowerStacks)),
        (schema.SourceOrderKey, scope.Field(card, schema.SourceOrderKey)),
        (schema.Enchantment, scope.Field(card, schema.Enchantment)),
        (schema.EnchantmentAmount, scope.Field(card, schema.EnchantmentAmount)),
        (schema.EnchantmentDisabled, scope.Field(card, schema.EnchantmentDisabled)),
        (clone ? schema.CloneOf : schema.LongTermSource, card),
        (schema.FreeUntilPlayed, clone ? scope.Field(card, schema.FreeUntilPlayed) : scope.Const(false)),
        .. variables.Select(entry => (entry.Value, (Expr)scope.Field(card, entry.Value))),
    ];
    public Inst Upgrade(IrScope scope, Expr held)
    {
        var cards = Compiled;
        Inst Add(FieldSymbol field, long delta) => scope.SetField(held, field,
            scope.Add(scope.Field(held, field), scope.Const(delta)));
        return scope.If(scope.Not(scope.Field(held, schema.Upgraded)),
            [.. cards.Where(card => card.Upgradeable).Select(card => scope.If(scope.Equal(scope.Field(held, schema.Definition), scope.Const(card.Id)),
                [
                scope.SetField(held, schema.Upgraded, scope.Const(true)),
                // 排序键带着升级等级：升级必须一起换，否则重洗会按普通形态排序。
                scope.SetField(held, schema.SourceOrderKey, scope.Const(SourceOrderKeyOf(card.Id, upgraded: true))),
                Add(schema.Cost, card.Upgraded.Cost - card.Normal.Cost),
                Add(schema.Amount, card.Upgraded.DisplayAmount - card.Normal.DisplayAmount),
                Add(schema.PowerStacks, card.Upgraded.DisplayStacks - card.Normal.DisplayStacks),
                scope.SetField(held, schema.Unplayable, scope.Const(card.Upgraded.Unplayable)),
                scope.SetField(held, schema.Ethereal, scope.Const(card.Upgraded.Ethereal)),
                .. card.Upgraded.Variables.Select(v => Add(variables[v.Key], v.Value - card.Normal.Variables.GetValueOrDefault(v.Key))) ]))]);
    }

    public Expr CanUpgrade(IrScope scope, Expr held)
    {
        Expr registered = scope.Const(false);
        foreach (var card in Compiled.Where(card => card.Upgradeable))
            registered = scope.Or(registered, scope.Equal(scope.Field(held, schema.Definition), scope.Const(card.Id)));
        return scope.And(scope.Not(scope.Field(held, schema.Upgraded)), registered);
    }

}
