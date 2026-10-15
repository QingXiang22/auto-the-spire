using CoreTheSpire;

namespace BaseSpire.Slice;

/// <summary>Supported game mechanics, independent of content IDs. Stateful counters live on Engine entities.</summary>
public enum PowerRuleKind
{
    ExtraAttackPlays, DrawOnExhaust, CloneThirdAttack, ReclaimAttacksBeforeTurn,
    SelfDamageAtTurnStart, BlockAtTurnStart, StrengthOnOwnerHpLoss,
    DamageAllOnOwnerDamage, AutoPlayDrawnStrikes, AutoPlayHandAttacksAtTurnEnd, HpLossCap, EnergyAfterResetOnce,
    HandDrawNextTurnOnce
}
public sealed record PowerRule(PowerRuleKind? Kind, int Amount, DslSourceLocation Source)
{
    public string Hook { get; init; } = "";
    public string Procedure { get; init; } = "";
    public IReadOnlyDictionary<string, DslValueNode> Arguments { get; init; } = new Dictionary<string, DslValueNode>();
}
public static class PowerRuleSource
{
    private static readonly IReadOnlyDictionary<string, PowerRuleKind> Names = new Dictionary<string, PowerRuleKind>(StringComparer.Ordinal)
    {
        ["extraAttackPlays"] = PowerRuleKind.ExtraAttackPlays, ["drawOnExhaust"] = PowerRuleKind.DrawOnExhaust,
        ["cloneThirdAttack"] = PowerRuleKind.CloneThirdAttack, ["reclaimAttacksBeforeTurn"] = PowerRuleKind.ReclaimAttacksBeforeTurn,
        ["selfDamageAtTurnStart"] = PowerRuleKind.SelfDamageAtTurnStart, ["blockAtTurnStart"] = PowerRuleKind.BlockAtTurnStart,
        ["strengthOnOwnerHpLoss"] = PowerRuleKind.StrengthOnOwnerHpLoss, ["damageAllOnOwnerDamage"] = PowerRuleKind.DamageAllOnOwnerDamage,
        ["autoPlayDrawnStrikes"] = PowerRuleKind.AutoPlayDrawnStrikes, ["autoPlayHandAttacksAtTurnEnd"] = PowerRuleKind.AutoPlayHandAttacksAtTurnEnd,
        ["hpLossCap"] = PowerRuleKind.HpLossCap,
        ["energyAfterResetOnce"] = PowerRuleKind.EnergyAfterResetOnce,
        ["handDrawNextTurnOnce"] = PowerRuleKind.HandDrawNextTurnOnce,
    };
    public static IReadOnlyList<PowerRule> Read(DslValueNode value, List<string> problems)
    {
        if (value is not DslBlockNode block) { problems.Add($"{value.Location}: rules must be a block"); return []; }
        var rules = new List<PowerRule>();
        foreach (var member in block.Members)
        {
            if (member is DslPropertyNode { Name: "afterEnergyReset", Value: DslCallNode call } lifecycle)
            {
                if (call.Arguments.Any(a => a.Name is null)
                    || call.Arguments.GroupBy(a => a.Name).Any(g => g.Count() > 1))
                {
                    problems.Add($"{lifecycle.Location}: lifecycle call requires unique named arguments");
                    continue;
                }
                if (rules.Any(r => r.Hook == lifecycle.Name))
                    problems.Add($"{lifecycle.Location}: duplicate lifecycle hook '{lifecycle.Name}'");
                rules.Add(new(null, 0, lifecycle.Location)
                {
                    Hook = lifecycle.Name, Procedure = call.Name,
                    Arguments = call.Arguments.ToDictionary(a => a.Name!, a => a.Value, StringComparer.Ordinal),
                });
                continue;
            }
            if (member is not DslPropertyNode p || !Names.TryGetValue(p.Name, out var kind))
            { problems.Add($"{member.Location}: unsupported power rule"); continue; }
            var amount = p.Value is DslBooleanNode { Value: true } ? 1 : p.Value is DslNumberNode n && int.TryParse(n.Literal, out var number) ? number : 0;
            if (amount < 1 || amount > 99) { problems.Add($"{p.Location}: {p.Name} requires true or integer 1..99"); continue; }
            if (kind is not (PowerRuleKind.ExtraAttackPlays or PowerRuleKind.HpLossCap)
                && p.Value is not DslBooleanNode { Value: true })
            { problems.Add($"{p.Location}: {p.Name} requires true; magnitude comes from power stacks"); continue; }
            if (rules.Any(r => r.Kind == kind)) problems.Add($"{p.Location}: duplicate rule {p.Name}");
            rules.Add(new(kind, amount, p.Location));
        }
        return rules;
    }
}
