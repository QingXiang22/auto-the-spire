using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Run;

/// <summary>Current supported permanent deck operations; source modifiers/history remain separate capabilities.</summary>
public sealed class RunDeck(CardDefinitions definitions, CollectionSymbol cards)
{
    public CardDefinitions Definitions { get; } = definitions;
    public CollectionSymbol Cards { get; } = cards;
    public Func<IrScope, Expr, Inst[]> BeforeRemoved { get; init; } = (_, _) => [];
    public Func<IrScope, Expr, Inst[]> AfterAdded { get; init; } = (_, _) => [];
    private int nextLocal;

    /// <summary>Source deck selectors skip empty domains and auto-select a unique candidate.</summary>
    public Inst[] SelectOne(IrScope scope, Func<Expr, Expr> eligible, string prompt,
        Func<Expr, Inst[]> onSelected)
    {
        var candidates = scope.Where(scope.Collection(Cards), eligible);
        var selected = scope.Local($"selectedPermanentCards{nextLocal++}",
            FieldTypeSchema.ListOf(FieldTypeSchema.RefTo(Definitions.Schema.Type)));
        var unique = scope.Local($"uniquePermanentCard{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        return [scope.IfElse(scope.Equal(scope.Count(candidates), scope.Const(1L)),
            [scope.SetLocal(unique, scope.First(candidates)), .. onSelected(scope.LocalRef(unique))],
            [scope.If(scope.Greater(scope.Count(candidates), scope.Const(1L)),
                [scope.Decide(scope.Const(Value.OfParticipant(SliceManifest.Player)), DecisionResponseKind.EntityRefList,
                    prompt, selected, candidates, scope.Const(1L), scope.Const(1L)),
                 scope.ForEach(new RefListQuery(selected), onSelected)])])];
    }

    public Inst[] SelectForUpgrade(IrScope scope) => SelectOne(scope,
        card => Definitions.CanUpgrade(scope, card), "Choose a permanent card to upgrade",
        card => [Definitions.Upgrade(scope, card)]);

    /// <summary>Source deck selection: auto-select all when candidates do not exceed count; otherwise one exact-count decision.</summary>
    public Inst[] SelectMany(IrScope scope, int count, Func<Expr, Expr> eligible, string prompt,
        Func<Expr, Inst[]> onSelected)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 1) return SelectOne(scope, eligible, prompt, onSelected);
        var candidates = scope.Where(scope.Collection(Cards), eligible);
        var selected = scope.Local($"selectedPermanentCards{nextLocal++}",
            FieldTypeSchema.ListOf(FieldTypeSchema.RefTo(Definitions.Schema.Type)));
        // Engine ForEach snapshots its query before the first effect; appended replacement cards cannot re-enter.
        return [scope.IfElse(scope.LessOrEqual(scope.Count(candidates), scope.Const((long)count)),
            [scope.ForEach(candidates, onSelected)],
            [scope.Decide(scope.Const(Value.OfParticipant(SliceManifest.Player)), DecisionResponseKind.EntityRefList,
                prompt, selected, candidates, scope.Const((long)count), scope.Const((long)count)),
             scope.ForEach(new RefListQuery(selected), onSelected)])];
    }

    public Inst[] SelectForTransform(IrScope scope, Expr run, string stream) => SelectOne(scope,
        card => CanTransform(scope, card), "Choose a permanent card to transform",
        card => TransformToRandom(scope, run, card, stream));

    // Current registered content has no Eternal attachment support; do not infer transformability from upgradeability.
    public Expr CanTransform(IrScope scope, Expr card)
    {
        Expr eligible = scope.Const(false);
        foreach (var definition in Definitions.Compiled.Where(c => c.Kind != "Quest" && !c.Tags.Contains("eternal")))
            eligible = scope.Or(eligible, scope.Equal(scope.Field(card, Definitions.Schema.Definition), scope.Const(definition.Id)));
        return eligible;
    }

    public Expr CanRemove(IrScope scope, Expr card) => Definitions.Compiled.Where(c => !c.Tags.Contains("eternal"))
        .Select(c => (Expr)scope.Equal(scope.Field(card, Definitions.Schema.Definition), scope.Const(c.Id)))
        .Aggregate(scope.Const(false), (left, right) => scope.Or(left, right));

    public Expr HasRarity(IrScope scope, Expr card, string rarity) => Definitions.Compiled
        .Where(c => c.Normal.Rarity == rarity || c.Upgraded.Rarity == rarity)
        .Select(c => c.Normal.Rarity == rarity && c.Upgraded.Rarity == rarity
            ? (Expr)scope.Equal(scope.Field(card, Definitions.Schema.Definition), scope.Const(c.Id))
            : scope.And(scope.Equal(scope.Field(card, Definitions.Schema.Definition), scope.Const(c.Id)),
                c.Upgraded.Rarity == rarity ? scope.Field(card, Definitions.Schema.Upgraded) : scope.Not(scope.Field(card, Definitions.Schema.Upgraded))))
        .Aggregate(scope.Const(false), (left, right) => scope.Or(left, right));

    /// <summary>One source-compatible NextItem draw over the supplied ordered permanent-card query.</summary>
    public Inst[] SelectRandom(IrScope scope, Query candidates, string stream, Func<Expr, Inst[]> onSelected)
    {
        var index = scope.Local($"randomPermanentIndex{nextLocal++}", FieldTypeSchema.Int(0, 999999));
        var cursor = scope.Local($"randomPermanentCursor{nextLocal++}", FieldTypeSchema.Int(0, 999999));
        var selected = scope.Local($"randomPermanentCard{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        return [scope.If(scope.Equal(scope.Count(candidates), scope.Const(0L)), scope.Fail("Random permanent-card choice has no candidates")),
            scope.SampleInt(stream, scope.Const(0L), scope.Count(candidates), index),
            scope.SetLocal(cursor, scope.Const(0L)),
            scope.ForEach(candidates, card => [
                scope.If(scope.Equal(scope.LocalRef(index), scope.LocalRef(cursor)), scope.SetLocal(selected, card)),
                scope.SetLocal(cursor, scope.Add(scope.LocalRef(cursor), scope.Const(1L)))]),
            .. onSelected(scope.LocalRef(selected))];
    }

    /// <summary>Normal Ironclad pool, source order, one draw excluding the original; persistent replacements append.</summary>
    public Inst[] TransformToRandom(IrScope scope, Expr run, Expr original, string stream)
    {
        var selected = scope.Local($"transformedPermanentCard{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        var held = scope.LocalRef(selected);
        var definitionField = Definitions.Schema.Definition;
        // Resolve the complete source pool before filtering, never the currently supported subset.
        var pool = IroncladRewardPool.SourceOrder.Where(id => !IroncladRewardPool.MultiplayerOnly.Contains(id))
            .Select(id => Definitions.Definition("base-spire:" + id)).ToArray();
        var options = pool.Where(c => c.Normal.Rarity is "common" or "uncommon" or "rare").ToArray();
        Expr supported = scope.Const(false);
        foreach (var card in pool.Where(c => c.Normal.Rarity is "basic" or "common" or "uncommon" or "rare"))
            supported = scope.Or(supported, scope.Equal(scope.Field(held, definitionField), scope.Const(card.Id)));
        var count = scope.Local($"transformationCount{nextLocal++}", FieldTypeSchema.Int(0, options.Length));
        var pick = scope.Local($"transformationPick{nextLocal++}", FieldTypeSchema.Int(0, options.Length));
        var position = scope.Local($"transformationPosition{nextLocal++}", FieldTypeSchema.Int(0, options.Length));
        var replacement = scope.Local($"transformationReplacement{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        List<Inst> steps = [scope.SetLocal(selected, original),
            scope.If(scope.Or(scope.Less(scope.CollectionIndex(Cards, held), scope.Const(0L)), scope.Not(CanTransform(scope, held))),
                scope.Fail("Permanent transformation requires a transformable deck member")),
            scope.If(scope.Not(supported), scope.Fail("Permanent transformation source pool is not implemented for this card")),
            scope.SetLocal(count, scope.Const((long)options.Length))];
        foreach (var card in options)
            steps.Add(scope.If(scope.Equal(scope.Field(held, definitionField), scope.Const(card.Id)),
                scope.SetLocal(count, scope.Subtract(scope.LocalRef(count), scope.Const(1L)))));
        steps.Add(scope.SampleInt(stream, scope.Const(0L), scope.LocalRef(count), pick));
        steps.Add(scope.SetLocal(position, scope.Const(0L)));
        foreach (var card in options)
            steps.Add(scope.If(scope.Not(scope.Equal(scope.Field(held, definitionField), scope.Const(card.Id))),
                [scope.If(scope.Equal(scope.LocalRef(position), scope.LocalRef(pick)),
                    scope.CreateEntity(Definitions.Schema.Type, scope.EntityScope(run), replacement, Definitions.New(scope, card, false))),
                 scope.SetLocal(position, scope.Add(scope.LocalRef(position), scope.Const(1L)))]));
        steps.Add(scope.Destroy(held));
        steps.AddRange(Acquire(scope, scope.LocalRef(replacement)));
        return [.. steps];
    }

    public Inst[] Create(IrScope scope, Expr run, CardDefinition definition, bool upgraded = false)
    {
        var created = scope.Local($"permanentCard{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        return [scope.CreateEntity(Definitions.Schema.Type, scope.EntityScope(run), created,
            Definitions.New(scope, definition, upgraded)), .. Acquire(scope, scope.LocalRef(created))];
    }

    /// <summary>The caller removes any source candidate membership first. The same instance enters the deck.</summary>
    public Inst[] Acquire(IrScope scope, Expr card)
    {
        var selected = scope.Local($"acquiredPermanentCard{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        var read = scope.LocalRef(selected);
        return [scope.SetLocal(selected, card), scope.AddToCollection(Cards, read), .. AfterAdded(scope, read)];
    }

    public Inst[] Remove(IrScope scope, Expr card)
    {
        var selected = scope.Local($"removedPermanentCard{nextLocal++}", FieldTypeSchema.RefTo(Definitions.Schema.Type));
        var read = scope.LocalRef(selected);
        return [scope.SetLocal(selected, card), scope.If(scope.Less(scope.CollectionIndex(Cards, read), scope.Const(0L)),
            scope.Fail("Permanent card removal requires a deck member")),
         .. BeforeRemoved(scope, read), scope.Destroy(read)];
    }
}
