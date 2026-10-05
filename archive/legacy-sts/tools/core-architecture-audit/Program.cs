using System.Diagnostics;
using System.Text.Json;
using BaseSpire;
using CoreTheSpire;

var definition = Build(new BaseSpirePackage());
Console.WriteLine(JsonSerializer.Serialize(new
{
    kind = "content", definition.Version,
    cards = definition.Cards.Count, powers = definition.Powers.Count,
    enemies = definition.Enemies.Count, encounters = definition.Encounters.Count,
    potions = definition.Potions.Count, relics = definition.Relics.Count,
    characters = definition.Characters.Count, events = definition.RunEvents.Count,
    ironcladPool = definition.CardPools[BaseSpireIds.IroncladCardPool].Cards.Count
}));

var hookCard = Compile("HookProbe", "selectSelf = {}",
    "onExhaust = { draw = { amount = 1 } }");
var allyCard = Compile("AllyProbe",
    "selectOne = { from = context.allies where = alive }", "");
Console.WriteLine(JsonSerializer.Serialize(new
{
    kind = "dsl_contracts", target = allyCard.Target.ToString(),
    emptyUpgradedHooksInherit = hookCard.ResolveExhaustEffect(new CardInstance("hook", hookCard.Id, 1)) is not null,
    difficultyProfilesShareVersion = definition.Version == Build(new BaseSpirePackage(true, true)).Version
}));

var orderedDefinition = Build(new BaseSpirePackage(), new OrderProbePackage());
var orderedEnvironment = new CombatEnvironment(orderedDefinition);
var orderedSession = orderedEnvironment.Start(7, "player", 80, 80,
    [ContentId.Parse("architecture-audit:order-probe")], BaseSpireIds.DampCultist.ToString());
var orderedResult = orderedEnvironment.Step(orderedSession, new PlayCardAction(0, "player"));
Console.WriteLine(JsonSerializer.Serialize(new
{
    kind = "native_handler_order",
    blockAmounts = orderedResult.Events.Where(e => e.Type == "block").Select(e => e.Amount).ToArray()
}));

Console.WriteLine(JsonSerializer.Serialize(new
{
    kind = "host", runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
    processors = System.Environment.ProcessorCount,
    configuration =
#if DEBUG
        "Debug"
#else
        "Release"
#endif
}));
var environment = new CombatEnvironment(definition);
var deck = BaseSpireContent.IroncladStarterDeck();
GameSession NewSession() => environment.Start(42, "player", 1_000_000_000, 1_000_000_000,
    deck, BaseSpireIds.DampCultist.ToString(),
    characterId: BaseSpireIds.Ironclad);

// Public API measurements include observation construction. This is a small synthetic
// fixture, not a training workload or a measurement of the executor in isolation.
var warm = NewSession();
for (var i = 0; i < 200; i++)
{
    _ = environment.Observe(warm);
    _ = environment.LegalActions(warm);
    _ = environment.Step(warm, new EndTurnAction());
}
for (var sample = 0; sample < 3; sample++)
{
    var session = NewSession();
    Measure("observe_10_cards_1_enemy", 10_000, sample, () => environment.Observe(session));
    Measure("legal_actions_10_cards_1_enemy", 10_000, sample, () => environment.LegalActions(session));
    var turnSession = NewSession();
    var endTurn = new EndTurnAction();
    Measure("end_turn_10_card_deck_1_enemy", 1_000, sample,
        () => environment.Step(turnSession, endTurn));
}

static void Measure(string name, int iterations, int sample, Func<object> operation)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    var timer = Stopwatch.StartNew();
    object? last = null;
    for (var i = 0; i < iterations; i++) last = operation();
    timer.Stop();
    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
    GC.KeepAlive(last);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        kind = "measurement", name, sample, iterations,
        microsecondsPerOperation = timer.Elapsed.TotalMicroseconds / iterations,
        allocatedBytesPerOperation = (double)allocated / iterations
    }));
}

static GameDefinition Build(params IRulePackage[] packages)
{
    var registry = new ContentRegistry();
    foreach (var package in packages) registry.AddPackage(package);
    return registry.Build();
}

static CardDefinition Compile(string name, string target, string normalHooks)
{
    var source = $$"""
        using namespace architecture-audit;
        card {{name}} {
          normal = {
            type = skill rarity = common cost = 0 keywords = {}
            target = { {{target}} } play = {} hooks = { {{normalHooks}} }
          }
          upgraded = {
            type = skill rarity = common cost = 0 keywords = {}
            target = { {{target}} } play = {} hooks = {}
          }
        }
        """;
    return SpireCombatDslCompiler.CompileCards(DslSourceParser.Parse(source, "architecture-audit.spire")).Single();
}

sealed class OrderProbePackage : IRulePackage
{
    public PackageInfo Info { get; } = new("architecture-audit", "1");
    public void Register(ContentRegistry registry)
    {
        var handler = new OrderProbeHandler();
        registry.Register(handler);
        registry.Register(new CardDefinition(ContentId.Parse("architecture-audit:order-probe"), 0,
            CardType.Skill, new SequenceEffect([
                new CustomEffect(handler.Id, JsonSerializer.SerializeToElement(new { })),
                new GainBlockEffect(20)
            ]), Target: TargetSelector.Self));
    }
}

sealed class OrderProbeHandler : ICustomEffectHandler
{
    public ContentId Id { get; } = ContentId.Parse("architecture-audit:order-handler");
    public void Execute(EffectExecutionContext context, JsonElement payload)
    {
        context.GainBlock(1);
        context.GainBlock(2);
    }
}
