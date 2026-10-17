using CoreTheSpire;
using BaseSpire.SinglePlayer;

namespace BaseSpire.Slice;

/// <summary>Generation metadata remains complete even when a use program cannot be lowered.</summary>
public sealed record PotionDefinition(string Id, string Name, string Rarity, string Usage,
    string Target, string Source, string UnsupportedUseReason);

public static class SpirePotionSource
{
    public const string Resource = "BaseSpire.ContentSource.potions.spire";
    public sealed record Source(IReadOnlyList<PotionDefinition> Pool, IReadOnlyList<AuthorPotion> Potions);

    public static Source Load(IReadOnlyDictionary<string, string> powers) =>
        Parse(EncounterContent.ReadText(Resource), Resource, powers);

    public static Source Parse(string text, string sourceName, IReadOnlyDictionary<string, string> powers)
    {
        var package = DslSourceParser.Parse(text, sourceName);
        var pool = new List<PotionDefinition>();
        var potions = new List<AuthorPotion>();
        foreach (var node in package.Members.OfType<DslDeclarationNode>().Where(n => n.Kind == "potion"))
        {
            var fields = node.Members.OfType<DslPropertyNode>().ToDictionary(p => p.Name, StringComparer.Ordinal);
            if (fields.Count != node.Members.Count || fields.Keys.Any(k => k is not
                ("rarity" or "usage" or "target" or "source" or "unsupportedUse" or "use" or "unpowered")))
                throw new FormatException($"{node.Location}: unsupported potion field");
            string Get(string key) => fields.TryGetValue(key, out var p) ? p.Value switch
            {
                DslNameNode n => n.Name, DslStringNode s => s.Value,
                _ => throw new FormatException($"{p.Location}: {key} must be a name or string"),
            } : throw new FormatException($"{node.Location}: missing {key}");
            var rarity = Get("rarity"); var usage = Get("usage"); var target = Get("target");
            if (!PotionRarities.All.Contains(rarity) || usage is not ("combatOnly" or "anyTime" or "automatic")
                || target is not ("enemy" or "self" or "allEnemies"))
                throw new FormatException($"{node.Location}: invalid potion metadata");
            var id = $"{package.Name}:{SpireCardSource.ToId(node.Name)}";
            if (pool.Any(p => p.Id == id)) throw new FormatException($"{node.Location}: duplicate potion {id}");
            var reason = fields.ContainsKey("unsupportedUse") ? Get("unsupportedUse") : "";
            if (fields.ContainsKey("use") == (reason.Length > 0))
                throw new FormatException($"{node.Location}: specify a use program or a nonempty unsupportedUse reason");
            var metadata = new PotionDefinition(id, node.Name, rarity, usage, target, Get("source"), reason);
            if (reason.Length == 0)
            {
                try
                {
                    if (usage != "combatOnly") throw new FormatException("noncombat/automatic use is not bound yet");
                    var program = SpireCardSource.ReadPotionProgram(fields["use"].Value, target, powers);
                    if (program.NeedsSelectedTarget && target != "enemy")
                        throw new FormatException("selected enemy target requires target=enemy");
                    var unpowered = fields.TryGetValue("unpowered", out var powered)
                        ? powered.Value is DslBooleanNode flag ? flag.Value
                            : throw new FormatException("unpowered must be boolean") : false;
                    potions.Add(new(id, node.Name, rarity, PotionUsages.CombatOnly, target,
                        0, AmountSource.Literal, "", 0, [], unpowered, sourceName, node.Location.Line)
                        { Program = program });
                }
                catch (FormatException error) { metadata = metadata with { UnsupportedUseReason = error.Message }; }
            }
            pool.Add(metadata);
        }
        return new(pool, potions);
    }
}
