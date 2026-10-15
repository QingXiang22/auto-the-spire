using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>Owns potion identity, stable belt slots, reward generation and acquisition. No session state in C#.</summary>
public sealed class PotionInventoryRules
{
    public EntityTypeSymbol Entity { get; }
    public FieldSymbol Definition { get; }
    public FieldSymbol Rarity { get; }
    public FieldSymbol Target { get; }
    public FieldSymbol Usage { get; }
    public FieldSymbol SlotIndex { get; }
    public FieldSymbol UseSupported { get; }
    public FieldSymbol UnsupportedReason { get; }
    public FieldSymbol Amount { get; }
    public FieldSymbol PowerId { get; }
    public FieldSymbol PowerStacks { get; }
    public CollectionSymbol Belt { get; }
    public CollectionSymbol Rewards { get; }
    public IReadOnlyList<PotionDefinition> Pool { get; }
    private readonly IReadOnlyDictionary<string, AuthorPotion> effects;
    private readonly ProcedureBuilder generate;
    private int nextLocal;
    public Func<IrScope, Expr, Expr> ShouldAcquire { get; init; } = (scope, _) => scope.Const(true);
    public Func<IrScope, Expr, Inst[]> AfterAcquired { get; init; } = (_, _) => [];

    public PotionInventoryRules(EnginePackageBuilder package, SliceAuthorModel model)
    {
        effects = model.Potions.ToDictionary(p => p.Id, StringComparer.Ordinal);
        Pool = model.PotionPool.Count > 0 ? model.PotionPool : [.. model.Potions.Select(p =>
            new PotionDefinition(p.Id, p.Name, p.Rarity, p.Usage, p.Target, p.SourcePath, ""))];
        Entity = package.EntityType("Potion");
        Definition = Entity.AddField("DefinitionId", FieldTypeSchema.String(64));
        Rarity = Entity.AddField("Rarity", FieldTypeSchema.String(16));
        Target = Entity.AddField("Target", FieldTypeSchema.String(16));
        Usage = Entity.AddField("Usage", FieldTypeSchema.String(16));
        SlotIndex = Entity.AddField("SlotIndex", FieldTypeSchema.Int(-1, SliceManifest.PotionSlotCount - 1));
        UseSupported = Entity.AddField("UseSupported", FieldTypeSchema.Bool());
        UnsupportedReason = Entity.AddField("UnsupportedUseReason", FieldTypeSchema.String(2048));
        Amount = Entity.AddField("Amount", FieldTypeSchema.Int(0, 999));
        PowerId = Entity.AddField("PowerId", FieldTypeSchema.String(64));
        PowerStacks = Entity.AddField("PowerStacks", FieldTypeSchema.Int(-99, 99));
        Belt = package.Collection("PotionBelt", Entity); Belt.Capacity = SliceManifest.PotionSlotCount;
        Rewards = package.Collection("RewardPotions", Entity); Rewards.Capacity = 1;
        generate = BuildGenerate(package);
    }

    public IReadOnlyList<(FieldSymbol Field, Expr Value)> Fields(IrScope s, string id, Expr slot)
    {
        var definition = Pool.Single(p => p.Id == id);
        effects.TryGetValue(id, out var effect);
        return [
            (Definition, s.Const(id)), (Rarity, s.Const(definition.Rarity)), (Target, s.Const(definition.Target)),
            (Usage, s.Const(definition.Usage)), (SlotIndex, slot), (UseSupported, s.Const(effect is not null)),
            (UnsupportedReason, s.Const(definition.UnsupportedUseReason)),
            (Amount, s.Const((long)(effect?.Amount ?? 0))), (PowerId, s.Const(effect?.PowerId ?? "")),
            (PowerStacks, s.Const((long)(effect?.PowerStacks ?? 0))),
        ];
    }

    public Expr HasSpace(IrScope s) => s.Less(s.Count(s.Collection(Belt)), s.Const((long)SliceManifest.PotionSlotCount));
    public IReadOnlyList<Inst> ClearReward(IrScope s) =>
        [s.ForEach(s.Collection(Rewards), potion => [s.Destroy(potion)])];
    public IReadOnlyList<Inst> Generate(IrScope s, Expr owner) => [s.Call(generate.Symbol, owner)];

    public IReadOnlyList<Inst> Acquire(IrScope s)
    {
        return Acquire(s, s.First(s.Collection(Rewards)), Rewards);
    }

    public IReadOnlyList<Inst> Acquire(IrScope s, Expr potion, CollectionSymbol? source = null)
    {
        var suffix = nextLocal++;
        var selectedSlot = s.Local($"receivedPotion{suffix}", FieldTypeSchema.RefTo(Entity));
        var selected = s.LocalRef(selectedSlot);
        var free = s.Local($"firstFreePotionSlot{suffix}", FieldTypeSchema.Int(-1, SliceManifest.PotionSlotCount - 1));
        var steps = new List<Inst> {
            s.SetLocal(selectedSlot, potion),
            s.SetLocal(free, s.Const(-1L)),
        };
        for (var index = 0; index < SliceManifest.PotionSlotCount; index++)
        {
            var slot = s.Const((long)index);
            steps.Add(s.If(s.And(s.Equal(s.LocalRef(free), s.Const(-1L)),
                s.Not(s.Exists(s.Where(s.Collection(Belt), p => s.Equal(s.Field(p, SlotIndex), slot))))),
                s.SetLocal(free, slot)));
        }
        var acquired = new List<Inst> { s.SetField(selected, SlotIndex, s.LocalRef(free)) };
        if (source is not null) acquired.Add(s.RemoveFromCollection(source, selected));
        acquired.Add(s.AddToCollection(Belt, selected, InsertPosition.AtIndex,
            s.Count(s.Where(s.Collection(Belt), entry => s.Less(s.Field(entry, SlotIndex), s.LocalRef(free))))));
        acquired.AddRange(AfterAcquired(s, selected));
        steps.Add(s.If(s.And(s.GreaterOrEqual(s.LocalRef(free), s.Const(0L)),
            s.And(s.Less(s.CollectionIndex(Belt, selected), s.Const(0L)), ShouldAcquire(s, selected))), acquired.ToArray()));
        return steps;
    }

    public void RegisterDiscard(EnginePackageBuilder package, Func<IrScope, Expr>? allowed = null)
    {
        var action = package.Action("DiscardPotion");
        var parameter = action.Parameter("Potion", FieldTypeSchema.RefTo(Entity));
        var body = package.ActionBody(action);
        body.Role(SliceManifest.PlayerRole);
        body.Candidates("Potion", body.Collection(Belt));
        body.Requires(body.And(body.Exists(body.Collection(Belt)), allowed?.Invoke(body) ?? body.Const(true)));
        body.Body(body.Destroy(body.Param(parameter.DeclarationIndex)));
    }

    private ProcedureBuilder BuildGenerate(EnginePackageBuilder package)
    {
        // Existing pure pool count/pick computations preserve declared order. Only the potion
        // rarity roll differs from card odds; neither backend owns RNG or mutable inventory.
        (ComputeAssetSymbol Asset, FieldSymbol[] Inputs, FieldSymbol Output) Asset(
            string name, string entry, params (string Name, FieldTypeSchema Type)[] inputs)
        {
            var a = package.ComputeAsset(name); a.BackendId = BaseSpireComputeBackend.BackendId;
            a.SourcePath = "sts2/src/Core/Factories/PotionFactory.cs";
            a.SourceHash = "PotionFactory:RareChance=0.1f;UncommonChance=0.25f;ordered-rarity-next-item@1";
            a.Entry = entry;
            var fields = inputs.Select(input => a.Input(input.Name, input.Type)).ToArray();
            return (a, fields, a.Output("Result", FieldTypeSchema.Int(0, 999)));
        }
        var intType = FieldTypeSchema.Int(0, 999);
        var specType = FieldTypeSchema.String(4096);
        var rarityAsset = Asset("PotionRarity", "PotionRarity", ("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1)));
        var countAsset = Asset("PotionPoolCount", RewardCompute.CardCountEntry,
            ("Spec", specType), ("Rarity", intType), ("IdxA", intType), ("IdxB", intType));
        var pickAsset = Asset("PotionPoolPick", RewardCompute.CardPickEntry,
            ("Spec", specType), ("Rarity", intType), ("Index", intType), ("IdxA", intType), ("IdxB", intType));
        var spec = RewardPoolSpec.Encode(Pool.Select((p, i) => new RewardPoolEntry(i,
            p.Rarity switch { "rare" => RewardRarity.Rare, "uncommon" => RewardRarity.Uncommon, _ => RewardRarity.Common }, false)));
        var p = package.Procedure("GenerateRewardPotion");
        var ownerSlot = p.Param("Owner", FieldTypeSchema.AnyRef());
        var bits = p.Local("potionBits", FieldTypeSchema.Int(0, RewardCompute.Span - 1));
        var rarity = p.Local("potionRarity", intType); var count = p.Local("potionCount", intType);
        var pick = p.Local("potionPick", intType); var index = p.Local("potionIndex", intType);
        var created = p.Local("rewardPotion", FieldTypeSchema.Binding());
        Inst Compute((ComputeAssetSymbol Asset, FieldSymbol[] Inputs, FieldSymbol Output) a, int output, params Expr[] values) =>
            p.Compute(a.Asset, a.Inputs.Select((field, i) => (field, values[i])).ToArray(), [(a.Output, output)]);
        var stream = VanillaRandomLayout.EnginePlayerStreamName("Rewards");
        var none = p.Const((long)RewardCompute.NoPoolIndex);
        p.Body([
            .. ClearReward(p),
            p.SampleInt(stream, p.Const(0L), p.Const(RewardCompute.Span), bits),
            Compute(rarityAsset, rarity, p.LocalRef(bits)),
            Compute(countAsset, count, p.Const(spec), p.LocalRef(rarity), none, none),
            p.SampleInt(stream, p.Const(0L), p.LocalRef(count), pick),
            Compute(pickAsset, index, p.Const(spec), p.LocalRef(rarity), p.LocalRef(pick), none, none),
            .. Pool.Select((entry, i) => p.If(p.Equal(p.LocalRef(index), p.Const((long)i)),
                p.CreateEntity(Entity, p.EntityScope(p.Param(ownerSlot)), created, Fields(p, entry.Id, p.Const(-1L)), Rewards))),
        ]);
        return p;
    }
}
