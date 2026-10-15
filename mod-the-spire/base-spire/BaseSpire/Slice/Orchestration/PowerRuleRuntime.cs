using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;

namespace BaseSpire.Slice.Orchestration;

public sealed record PowerRuleBindings(ProcedureSymbol Apply, ProcedureSymbol Draw, ProcedureSymbol LoseHp,
    ProcedureSymbol Damage, ProcedureSymbol Block, ProcedureSymbol Play,
    Func<IrScope, Expr, Inst[]> Entered, Func<IrScope, Expr, string, Expr> HasTag,
    Func<IrScope, Expr, Inst[]> CloneToHand, Func<IrScope, Expr, Inst[]> EnterCard,
    Func<IrScope, Expr> Alive)
{
    public Func<IrScope, Expr, Inst[]> GainEnergy { get; init; } = (_, _) =>
        throw new InvalidOperationException("Power energy gain binding missing");
}

/// <summary>Compiles registered mechanics once. Hooks receive Engine-owned state; no card or power IDs are hardcoded.</summary>
public sealed class PowerRuleRuntime
{
    private readonly EnginePackageBuilder package;
    private readonly IReadOnlyList<AuthorPower> definitions;
    private readonly IReadOnlyList<CardDefinition> cards;
    private readonly CardSchema card;
    private readonly CardDefinitions recipes;
    private readonly CardPileSymbols piles;
    private readonly CardPlayHistory history;
    private readonly EnumTypeSymbol kinds;
    private readonly EntityTypeSymbol player;
    private readonly ScopeTypeSymbol battle;
    private readonly RelationSymbol powers;
    private readonly FieldSymbol id, stacks, hp, energy, costRule;
    private readonly CollectionSymbol monsters, longDeck, selection;
    private readonly FieldSymbol selfDamage, deferred, pending, selectedCard, amountOnTurnStart;
    private readonly EntityTypeSymbol selectionEntry;
    private readonly Dictionary<string, ProcedureBuilder> procedures = new(StringComparer.Ordinal);
    public FieldSymbol ActiveCard { get; }
    public FieldSymbol EndingTurn { get; }
    public FieldSymbol EtherealExhaust { get; }

    public PowerRuleRuntime(EnginePackageBuilder package, SliceAuthorModel? model, CardSchema card,
        CardDefinitions recipes, CardPileSymbols piles, CardPlayHistory history, EnumTypeSymbol kinds,
        EntityTypeSymbol player, EntityTypeSymbol power, ScopeTypeSymbol battle, RelationSymbol powers,
        FieldSymbol id, FieldSymbol stacks, FieldSymbol hp, FieldSymbol energy, CollectionSymbol monsters,
        CollectionSymbol? longDeck)
    {
        this.package=package; definitions=model?.Powers ?? []; cards=recipes.Compiled; this.card=card;
        this.recipes=recipes; this.piles=piles; this.history=history; this.kinds=kinds; this.player=player;
        this.battle=battle; this.powers=powers; this.id=id; this.stacks=stacks; this.hp=hp; this.energy=energy;
        this.monsters=monsters; this.longDeck=longDeck!;
        ActiveCard=player.AddField("ActiveCard", FieldTypeSchema.RefTo(card.Type), visibility:FieldVisibility.Hidden, optional:true);
        EndingTurn=player.AddField("EndingTurn",FieldTypeSchema.Bool(),visibility:FieldVisibility.Hidden);
        EtherealExhaust=player.AddField("EtherealExhaust",FieldTypeSchema.Bool(),visibility:FieldVisibility.Hidden);
        selfDamage=power.AddField("SelfDamage",FieldTypeSchema.Int(0,999),visibility:FieldVisibility.Hidden);
        deferred=power.AddField("DeferredExhausts",FieldTypeSchema.Int(0,999),visibility:FieldVisibility.Hidden);
        amountOnTurnStart=power.AddField("AmountOnTurnStart",FieldTypeSchema.Int(-99,99),visibility:FieldVisibility.Hidden);
        pending=card.Type.AddField("PendingStrength",FieldTypeSchema.Int(0,999),visibility:FieldVisibility.Hidden);
        costRule=card.Type.AddField("ReducesCostOnAttack",FieldTypeSchema.Bool(),visibility:FieldVisibility.Hidden);
        selectionEntry=package.EntityType("PowerCardSelectionEntry");selectedCard=selectionEntry.AddField("Card",FieldTypeSchema.RefTo(card.Type),visibility:FieldVisibility.Hidden);
        selection=package.Collection("PowerCardSelection",selectionEntry);selection.IdentityVisible=false;selection.OrderVisible=false;
        foreach(var name in new[]{"CaptureTurnStart","LostHp","ReceivedDamage","BeforeHandDraw","AfterEnergyReset","AfterTurnStart","AfterTurnEnd","Exhaust","EndTurnAuto"})
            procedures[name]=package.Procedure("PowerRules"+name);
        foreach(var name in new[]{"BeforePlay","AfterPlay","AfterDraw","EnterCard"})
        {
            var proc=package.Procedure("PowerRules"+name); proc.Param("Card",FieldTypeSchema.RefTo(card.Type)); procedures[name]=proc;
        }
    }
    public Inst Call(IrScope s,string name,params Expr[] args)=>s.Call(procedures[name].Symbol,args);
    private Expr Owner(IrScope s)=>s.First(s.Type(player,battle));
    private Expr Attack(IrScope s,Expr c)=>s.Equal(s.Field(c,card.Kind),s.EnumConst(kinds,"Attack"));
    private Query Owned(IrScope s)=>s.Relation(powers,Owner(s));
    private IEnumerable<(AuthorPower Power,PowerRule Rule)> Rules(PowerRuleKind kind)=>definitions
        .SelectMany(p=>p.Rules.Where(r=>r.Kind==kind).Select(r=>(p,r)));
    private Expr Present(IrScope s,Expr p,string definition)=>s.And(s.Equal(s.Field(p,id),s.Const(definition)),s.Greater(s.Field(p,stacks),s.Const(0L)));
    private Inst Each(IrScope s,AuthorPower p,Func<Expr,Inst[]> body)=>s.ForEach(s.Where(Owned(s),x=>Present(s,x,p.Id)),body);
    private Inst Tick(IrScope s,Expr p)=>s.SetField(p,stacks,s.Maximum(s.Const(0L),s.Subtract(s.Field(p,stacks),s.Const(1L))));
    public (Inst[] Capture,Expr Count) Replay(IrScope s,Expr playing,Expr requested)
    {
        var slot=s.Local("attackExtraPlays",FieldTypeSchema.Int(1,9999));var steps=new List<Inst>{s.SetLocal(slot,requested)};
        foreach(var (p,r) in Rules(PowerRuleKind.ExtraAttackPlays))
            steps.Add(s.If(Attack(s,playing),Each(s,p,instance=>[
                s.SetLocal(slot,s.Add(s.LocalRef(slot),s.Const((long)r.Amount))),Tick(s,instance)])));
        return ([..steps],s.LocalRef(slot));
    }
    public Inst[] CapHpLoss(IrScope s,Expr target,int damageSlot,Expr absorbed)
    {
        var result=new List<Inst>();
        foreach(var (p,r) in Rules(PowerRuleKind.HpLossCap))
            result.Add(s.ForEach(s.Where(s.Relation(powers,target),x=>Present(s,x,p.Id)),instance=>[
                s.If(s.Greater(s.LocalRef(damageSlot),absorbed),
                    s.SetLocal(damageSlot,s.Add(absorbed,s.Minimum(s.Subtract(s.LocalRef(damageSlot),absorbed),s.Const((long)r.Amount)))),Tick(s,instance))]));
        return [..result];
    }
    public Inst[] ModifyVariable(IrScope s,string powerId,string variable,long amount)
    {
        if(variable!="self-damage")throw Faults.Schema($"Power variable '{variable}' is not registered");
        return [s.ForEach(s.Where(Owned(s),x=>Present(s,x,powerId)),p=>[s.SetField(p,selfDamage,s.Add(s.Field(p,selfDamage),s.Const(amount)))])];
    }
    public (Inst[] Capture,Expr Count) HandDraw(IrScope s,Expr baseCount)
    {
        var count=s.Local("handDrawCount",FieldTypeSchema.Int(0,9999));
        var steps=new List<Inst>{s.SetLocal(count,baseCount)};
        foreach(var (p,_) in Rules(PowerRuleKind.HandDrawNextTurnOnce))
            steps.Add(Each(s,p,instance=>[
                s.If(s.Greater(s.Field(instance,amountOnTurnStart),s.Const(0L)),
                    s.SetLocal(count,s.Add(s.LocalRef(count),s.Field(instance,stacks))))]));
        return ([..steps],s.LocalRef(count));
    }
    public void Build(PowerRuleBindings b)
    {
        Body("CaptureTurnStart",s=>[s.ForEach(Owned(s),p=>[
            s.SetField(p,amountOnTurnStart,s.Field(p,stacks))])]);
        var gain = package.Procedure("HostGainDeferredEnergy");
        gain.Param("owner", FieldTypeSchema.RefTo(player));
        gain.Param("amount", FieldTypeSchema.Int());
        gain.Body(gain.If(gain.Equal(gain.Param(0), Owner(gain)), b.GainEnergy(gain, gain.Param(1))));
        const string resource = "BaseSpire.ContentSource.lifecycle-rules.spire";
        using var stream = typeof(PowerRuleRuntime).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing rule library '{resource}'");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd().Replace("using namespace base-spire;", $"using namespace {package.PackageId};", StringComparison.Ordinal);
        EngineDslCompiler.BindInto(text, package, resource);
        var deferredEnergy = package.Declarations.OfType<ProcedureSymbol>().Single(p => p.Name == "GrantDeferredEnergy");
        Inst Lifecycle(IrScope s, AuthorPower definition, PowerRule rule)
        {
            var procedure = package.Declarations.OfType<ProcedureSymbol>().SingleOrDefault(p => p.Name == rule.Procedure)
                ?? throw Faults.Schema($"{rule.Source}: unknown lifecycle procedure '{rule.Procedure}'");
            var expected = procedure.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            if (!expected.SetEquals(rule.Arguments.Keys))
                throw Faults.Schema($"{rule.Source}: '{rule.Procedure}' requires exactly [{string.Join(", ", expected)}]");
            return Each(s, definition, instance =>
            {
                Expr Argument(CoreTheSpire.DslValueNode value) => value switch
                {
                    CoreTheSpire.DslNameNode { Name: "context.owner" } => Owner(s),
                    CoreTheSpire.DslNameNode { Name: "context.power" } => instance,
                    CoreTheSpire.DslNameNode { Name: "context.power.stacks" } => s.Field(instance, stacks),
                    CoreTheSpire.DslNumberNode n when n.TryGetInt32(out var number) => s.Const((long)number),
                    _ => throw Faults.Schema($"{value.Location}: unavailable lifecycle context or argument"),
                };
                return [s.Call(procedure, procedure.Parameters.Select(p => Argument(rule.Arguments[p.Name])).ToArray())];
            });
        }
        Body("AfterEnergyReset",s=>Rules(PowerRuleKind.EnergyAfterResetOnce).Select(x=>Each(s,x.Power,p=>[
            s.Call(deferredEnergy, Owner(s), p, s.Field(p,stacks))]))
            .Concat(definitions.SelectMany(p => p.Rules.Where(r => r.Hook == "afterEnergyReset")
                .Select(r => Lifecycle(s, p, r)))).ToArray());
        Body("LostHp",s=>Rules(PowerRuleKind.StrengthOnOwnerHpLoss).Select(x=>Each(s,x.Power,p=>[
            s.If(s.Not(s.Field(Owner(s),EndingTurn)),s.IfElse(s.IsNone(s.Field(Owner(s),ActiveCard)),
                [s.Call(b.Apply,Owner(s),s.Const(SliceManifest.StrengthId),s.Field(p,stacks),s.Const(false),s.Const(false))],
                [s.SetField(s.Field(Owner(s),ActiveCard),pending,s.Add(s.Field(s.Field(Owner(s),ActiveCard),pending),s.Field(p,stacks)))]))])).ToArray());
        Body("ReceivedDamage",s=>Rules(PowerRuleKind.DamageAllOnOwnerDamage).Select(x=>Each(s,x.Power,p=>[
            s.If(s.Not(s.Field(Owner(s),EndingTurn)),s.ForEach(s.Collection(monsters),enemy=>[
                s.Call(b.Damage,Owner(s),enemy,s.Field(p,stacks),s.Const(false))]))])).ToArray());
        Body("Exhaust",s=>Rules(PowerRuleKind.DrawOnExhaust).Select(x=>Each(s,x.Power,p=>[
            s.IfElse(s.Field(Owner(s),EtherealExhaust),[s.SetField(p,deferred,s.Add(s.Field(p,deferred),s.Const(1L)))],
                [s.Call(b.Draw,s.Field(p,stacks))])])).ToArray());
        Body("AfterTurnEnd",s=>Rules(PowerRuleKind.DrawOnExhaust).Select(x=>Each(s,x.Power,p=>{
            var count=s.Local("deferredDraw",FieldTypeSchema.Int(0,9999));return new Inst[]{
                s.SetLocal(count,s.Multiply(s.Field(p,deferred),s.Field(p,stacks))),s.SetField(p,deferred,s.Const(0L)),s.Call(b.Draw,s.LocalRef(count))};})).ToArray());
        Body("AfterTurnStart",s=>Rules(PowerRuleKind.SelfDamageAtTurnStart).Select(x=>Each(s,x.Power,p=>[
            s.If(s.Greater(s.Field(p,selfDamage),s.Const(0L)),s.Call(b.LoseHp,s.Field(p,selfDamage)),Call(s,"ReceivedDamage"))]))
            .Concat(Rules(PowerRuleKind.BlockAtTurnStart).Select(x=>Each(s,x.Power,p=>[s.Call(b.Block,s.Field(p,stacks))])))
            .Concat(Rules(PowerRuleKind.HandDrawNextTurnOnce).Select(x=>Each(s,x.Power,p=>[
                s.If(s.Greater(s.Field(p,amountOnTurnStart),s.Const(0L)),s.Destroy(p))]))).ToArray());
        Body("BeforeHandDraw",s=>Rules(PowerRuleKind.ReclaimAttacksBeforeTurn).Select(x=>Each(s,x.Power,p=>[
            s.ForEach(s.Collection(selection),c=>[s.Destroy(c)]),
            s.ForEach(s.Where(s.Collection(piles.Discard),c=>Attack(s,c)),c=>[s.CreateEntity(selectionEntry,s.EntityScope(Owner(s)),s.Local("selectionEntry",FieldTypeSchema.RefTo(selectionEntry)),[(selectedCard,c)],selection)]),
            s.ShuffleCollection(selection,SliceManifest.RandomCardStream),
            s.ForEach(s.Take(s.Collection(selection),s.Field(p,stacks)),c=>[
                s.If(s.Less(s.Count(s.Collection(piles.Hand)),s.Const((long)SliceManifest.MaxHandSize)),
                    s.RemoveFromCollection(piles.Discard,s.Field(c,selectedCard)),s.AddToCollection(piles.Hand,s.Field(c,selectedCard))),recipes.Upgrade(s,s.Field(c,selectedCard))]),
            s.ForEach(s.Collection(selection),c=>[s.Destroy(c)])])).ToArray());
        Body("BeforePlay",s=>{
            var playing=s.Param(0);var steps=new List<Inst>();
            steps.Add(s.If(Attack(s,playing),s.ForEach(s.Where(s.Type(card.Type,battle),c=>s.Field(c,costRule)),c=>[
                s.SetField(c,card.CostModifier,s.Maximum(s.Const(-999L),s.Subtract(s.Field(c,card.CostModifier),s.Const(1L))))])));
            foreach(var (p,r) in Rules(PowerRuleKind.CloneThirdAttack))steps.Add(s.If(s.And(Attack(s,playing),
                s.Equal(s.Count(s.Where(s.Collection(history.Entries),e=>s.Equal(s.Field(e,history.Kind),s.EnumConst(kinds,"Attack")))),s.Const(3L))),
                Each(s,p,instance=>[s.Repeat(s.Field(instance,stacks),b.CloneToHand(s,playing))])));
            return [..steps];
        });
        Body("AfterPlay",s=>{var playing=s.Param(0);return [s.If(s.Greater(s.Field(playing,pending),s.Const(0L)),
            s.Call(b.Apply,Owner(s),s.Const(SliceManifest.StrengthId),s.Field(playing,pending),s.Const(false),s.Const(false)),s.SetField(playing,pending,s.Const(0L)))];});
        Body("AfterDraw",s=>Rules(PowerRuleKind.AutoPlayDrawnStrikes).Select(x=>Each(s,x.Power,p=>[
            s.If(s.And(b.HasTag(s,s.Param(0),"base-spire:strike"),b.Alive(s)),
                s.Call(b.Play,s.Param(0),s.Const((long)CardEffectPile.Hand),s.Const(true),s.Const(false),s.Const(1L)))])).ToArray());
        Body("EndTurnAuto",s=>{
            var steps=new List<Inst>();
            foreach(var (p,r) in Rules(PowerRuleKind.AutoPlayHandAttacksAtTurnEnd))
                steps.Add(Each(s,p,instance=>{
                    var pick=s.Local("endAutoCard",FieldTypeSchema.RefTo(card.Type));
                    var eligible=s.Where(s.Collection(piles.Hand),c=>s.And(Attack(s,c),s.Not(s.Field(c,card.Unplayable))));
                    return [s.Repeat(s.Field(instance,stacks),[s.If(s.And(b.Alive(s),s.Greater(s.Count(eligible),s.Const(0L))),
                        s.SamplePick(VanillaRandomLayout.EngineStreamName("Shuffle"),eligible,pick),
                        s.Call(b.Play,s.LocalRef(pick),s.Const((long)CardEffectPile.Hand),s.Const(true),s.Const(false),s.Const(1L)))])];}));
            return [..steps];
        });
        Body("EnterCard",s=>{
            var c=s.Param(0);var steps=new List<Inst>();
            foreach(var definition in cards.Where(c=>c.CostReductionPerAttack))
                steps.Add(s.If(s.Equal(s.Field(c,card.Definition),s.Const(definition.Id)),
                    s.SetField(c,costRule,s.Const(true)),
                    s.If(s.IsNone(s.Field(c,card.CloneOf)),
                        s.SetField(c,card.CostModifier,s.Subtract(s.Const(0L),history.FinishedCount(s,kinds,"Attack"))))));
            return [..steps];
        });
        void Body(string name,Func<ProcedureBuilder,Inst[]> create)
        {var s=procedures[name];var steps=create(s);s.Body(steps.Length==0?[s.If(s.Const(false))]:steps);}
    }
}
