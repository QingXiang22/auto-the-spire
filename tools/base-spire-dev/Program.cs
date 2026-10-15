using System.Text.Json;
using CoreTheSpire;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

const ulong seed = 20261001UL;
try
{
    if (args.Length < 2 || args[0] is not ("check" or "play")
        || (args[0] == "play" && (args.Length < 4 || args[^1] is not ("normal" or "upgraded"))))
        throw new ArgumentException("Usage: check FILE.spire [FILE...] | play FILE.spire [FILE...] QUALIFIED_CARD_ID normal|upgraded");

    var original = FormalSpirePackage.Load().Model;
    var files = (args[0] == "play" ? args[1..^2] : args[1..]).Select(Path.GetFullPath).ToArray();
    var syntax = files.Select(file => DslSourceParser.Parse(File.ReadAllText(file), file)).ToArray();
    var other = syntax.SelectMany(package => package.Members.OfType<DslDeclarationNode>())
        .FirstOrDefault(declaration => declaration.Kind is not ("card" or "power"));
    if (other is not null) throw new ArgumentException($"This card probe does not load '{other.Kind}' declarations at {other.Location}");
    var powerSources = syntax.Select((package, i) => SpirePowerSource.Lower(package, files[i])).ToArray();
    var powers = original.Powers.Concat(powerSources.SelectMany(source => source.Powers)).ToArray();
    var duplicatePower = powers.GroupBy(power => power.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
    if (duplicatePower is not null) throw new ArgumentException($"Duplicate content id '{duplicatePower.Key}'");
    var sources = syntax.Select((package, i) => {
        var links = powers.ToDictionary(power => power.Id, power => power.Id, StringComparer.Ordinal);
        foreach (var group in powers.GroupBy(power => power.Name, StringComparer.Ordinal))
        {
            var local = group.Where(power => power.Id.StartsWith(package.Name + ":", StringComparison.Ordinal)).ToArray();
            if (local.Length == 1) links[group.Key] = local[0].Id;
            else if (group.Count() == 1) links[group.Key] = group.Single().Id;
        }
        return SpireCardSource.Lower(package, files[i], links);
    }).ToArray();
    var cards = sources.SelectMany(source => source.Cards).ToArray();
    var declared = sources.Sum(source => source.DeclaredCount);
    var parsed = sources.Sum(source => source.ParsedCount);
    var lowered = sources.Sum(source => source.LoweredCount);
    var unsupported = sources.SelectMany(source => source.Unsupported).Concat(powerSources.SelectMany(source => source.Unsupported)).ToArray();
    if (declared + powerSources.Sum(source => source.DeclaredCount) == 0 || unsupported.Length != 0)
    {
        Write(new { scope = "controlled-card-probe", declared, parsed, lowered, registered = 0,
            unsupported = unsupported.Select(item => new { name = item.Name, reason = item.Reason }) });
        return 2;
    }
    var merged = original.Cards.Concat(cards).ToArray();
    var duplicate = merged.GroupBy(card => card.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
    if (duplicate is not null) throw new ArgumentException($"Duplicate content id '{duplicate.Key}'; this tool composes additions, not overrides");
    var chosen = args[0] == "play" ? cards.Single(card => card.Id == args[^2]) : null;
    var upgraded = chosen is not null && args[^1] == "upgraded";
    if (upgraded && !chosen!.Upgradeable) throw new ArgumentException($"Card '{chosen.Id}' cannot be upgraded");
    // Keep base definitions; only the probe deck is controlled. No map/reward factory is substituted.
    var model = original with { Cards = merged, Powers = powers, Deck = (chosen is null ? cards : [chosen])
        .Select(card => new AuthorDeckEntry(card.Id, 1, upgraded)).ToArray() };
    var builder = new RuleSetBuilder();
    builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
    var session = new Session(builder.Build(), seed, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
    session.Start();
    EnsureHealthy(session);
    var cardType = session.RuleSet.TypeByQualifiedId("base-spire:Card");
    string Definition(InstanceId card) => session.State.GetField(card, cardType.FieldIndex("DefinitionId")).AsString();
    var present = session.State.AllInstances(cardType.Id).Select(Definition).ToHashSet(StringComparer.Ordinal);
    var expectedInstances = chosen is null ? cards : [chosen];
    var registered = expectedInstances.Count(card => present.Contains(card.Id));
    if (registered != expectedInstances.Length) throw new InvalidOperationException("Some probe deck cards have no runtime instance");

    object? play = null;
    if (chosen is not null)
    {
        var card = session.State.CollectionMembers(Collection(session, "base-spire:Hand")).Single(card => Definition(card) == chosen.Id);
        var player = session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Single();
        var target = session.State.CollectionMembers(Collection(session, "base-spire:Monsters")).First();
        var before = Snapshot(session, player, target);
        var action = session.LegalActions(SliceManifest.Player).Single(action => action.Definition.QualifiedId == "base-spire:PlayCard");
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
        var decisions = 0;
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            var pending = session.PendingDecision!;
            var answer = pending.Candidates.Contains(target) ? target : pending.Candidates.First();
            session.Answer(new(pending.RequestId, [answer]));
            decisions++;
        }
        EnsureHealthy(session);
        play = new { card = chosen.Id, variant = args[^1], decisions, before, after = Snapshot(session, player, target),
            discarded = session.State.CollectionMembers(Collection(session, "base-spire:DiscardPile")).Contains(card),
            exhausted = session.State.CollectionMembers(Collection(session, "base-spire:ExhaustPile")).Contains(card) };
    }
    Write(new { scope = "controlled-card-probe", seed, ruleset = session.RuleSet.Identity, declared,
        parsed, lowered, registered, ids = cards.Select(card => card.Id),
        powerDeclared = powerSources.Sum(source => source.DeclaredCount), powerLowered = powerSources.Sum(source => source.LoweredCount),
        powerIds = powerSources.SelectMany(source => source.Powers).Select(power => power.Id),
        status = session.Status.ToString(), play });
    return 0;
}
catch (Exception error)
{
    Write(new { scope = "controlled-card-probe", error = error.Message });
    return 2;
}

static int Collection(Session session, string id) => session.RuleSet.Collections.Single(collection => collection.QualifiedId == id).Id;
static object Snapshot(Session session, InstanceId player, InstanceId target)
{
    long Number(InstanceId instance, string type, string field) =>
        session.State.GetField(instance, session.RuleSet.TypeByQualifiedId(type).FieldIndex(field)).AsInt();
    return new { playerHp = Number(player, "base-spire:Player", "Hp"), block = Number(player, "base-spire:Player", "Block"),
        energy = Number(player, "base-spire:Player", "Energy"), targetHp = Number(target, "base-spire:Monster", "Hp"),
        handCount = session.State.CollectionMembers(Collection(session, "base-spire:Hand")).Count };
}
static void EnsureHealthy(Session session)
{
    if (session.Status == SessionStatus.Faulted) throw new InvalidOperationException(session.Fault?.ToString());
}
static void Write(object value) => Console.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
