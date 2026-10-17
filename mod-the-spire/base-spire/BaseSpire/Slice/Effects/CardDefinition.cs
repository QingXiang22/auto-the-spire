using System.Collections.Immutable;
using CoreTheSpire;

namespace BaseSpire.Slice.Effects;

public enum CardResultPolicy { Discard, Exhaust }

/// <summary>The complete immutable variant consumed by production rule compilation.</summary>
public sealed record CardVariant(int Cost, bool Unpowered, CardResultPolicy Result,
    CardEffectProgram Program, ImmutableDictionary<string, CardEffectProgram> Hooks,
    ImmutableDictionary<string, int> Variables)
{
    public CardEffectAmount? DamageValue { get; init; }
    public bool IsXCost { get; init; }
    public int StarCost { get; init; }
    public bool IsStarXCost { get; init; }
    public bool Retain { get; init; }
    public bool Unplayable { get; init; }
    public bool Ethereal { get; init; }
    public bool Innate { get; init; }
    public bool Sly { get; init; }
    public string Rarity { get; init; } = "";
    // Diagnostic instance fields are not executable effect facts.
    public int DisplayAmount { get; init; }
    public string DisplayPowerId { get; init; } = "";
    public int DisplayStacks { get; init; }
}

public sealed record CardDefinition(string Id, CardVariant Normal, CardVariant Upgraded)
{
    public string Kind { get; init; } = "";
    public bool Upgradeable { get; init; }
    public bool CanGenerateInCombat { get; init; }
    public bool MultiplayerOnly { get; init; }
    public bool CostReductionPerAttack { get; init; }
    public string DamageVariable { get; init; } = "";
    public ImmutableArray<string> Pools { get; init; } = [];
    public ImmutableArray<string> Tags { get; init; } = [];
    public CardVariant Variant(bool upgraded) => upgraded ? Upgraded : Normal;

    // The source frontend and old .author fixtures are adapted exactly once, before rule compilation.
    public static CardDefinition FromSource(AuthorCard source)
    {
        CardVariant Read(bool upgraded)
        {
            var program = upgraded ? source.UpgradedProgram : source.NormalProgram;
            var hooks = upgraded ? source.UpgradedHooks : source.NormalHooks;
            var formal = program is not null;
            var location = new DslSourceLocation(source.SourcePath, source.SourceLine, 1);
            var result = formal
                ? (upgraded ? source.UpgradedResult : source.NormalResult)
                : (upgraded ? source.UpgradedSteps : source.NormalSteps).Any(s => s.Kind == AuthorSteps.ExhaustSelf)
                    ? CardResultPolicy.Exhaust : CardResultPolicy.Discard;
            var bodies = formal
                ? hooks.Programs ?? ImmutableDictionary<string, CardEffectProgram>.Empty
                : AuthorSteps.Hooks.ToImmutableDictionary(name => name,
                    name => LegacyCardAdapter.Hook(source, upgraded, hooks.For(name), location));
            return new(upgraded ? source.UpgradedCost : source.NormalCost,
                upgraded ? source.UpgradedUnpowered : source.NormalUnpowered, result,
                program ?? LegacyCardAdapter.Play(source, upgraded, location), bodies,
                (upgraded ? source.UpgradedVariables : source.NormalVariables).ToImmutableDictionary(StringComparer.Ordinal))
            {
                DamageValue = source.DamageVariable.Length > 0
                    ? new CardEffectAmount(CardAmountSource.Variable, 0, 1, source.DamageVariable)
                    : formal ? (upgraded ? source.UpgradedDamageValue : source.NormalDamageValue)
                    : source.Kind == "Attack" ? CardEffectAmount.FromAuthor(
                        upgraded ? source.UpgradedAmountSource : source.NormalAmountSource,
                        (upgraded ? source.UpgradedAmountSource : source.NormalAmountSource) == AmountSource.Literal
                            ? (upgraded ? source.UpgradedAmount : source.NormalAmount)
                            : (upgraded ? source.UpgradedAmountBase : source.NormalAmountBase),
                        upgraded ? source.UpgradedAmountPerCount : source.NormalAmountPerCount) : null,
                IsXCost = (upgraded ? source.UpgradedXCost : source.NormalXCost) ?? source.IsXCost,
                StarCost = upgraded ? source.UpgradedStarCost : source.NormalStarCost,
                IsStarXCost = upgraded ? source.UpgradedStarXCost : source.NormalStarXCost,
                Retain = upgraded ? source.UpgradedRetain : source.NormalRetain,
                Unplayable = upgraded ? source.UpgradedUnplayable : source.NormalUnplayable,
                Ethereal = upgraded ? source.UpgradedEthereal : source.NormalEthereal,
                Innate = upgraded ? source.UpgradedInnate : source.NormalInnate,
                Sly = upgraded ? source.UpgradedSly : source.NormalSly,
                Rarity = upgraded ? source.UpgradedRarity : source.Rarity,
                DisplayAmount = upgraded ? source.UpgradedAmount : source.NormalAmount,
                DisplayPowerId = source.PowerId,
                DisplayStacks = upgraded ? source.UpgradedStacks : source.NormalStacks,
            };
        }
        return new(source.Id, Read(false), Read(true))
        {
            Kind = source.Kind, Upgradeable = source.Upgradeable, CanGenerateInCombat = source.CanGenerateInCombat,
            MultiplayerOnly = source.MultiplayerOnly, CostReductionPerAttack = source.CostReductionPerAttack,
            DamageVariable = source.DamageVariable, Pools = [.. source.Pools], Tags = [.. source.Tags],
        };
    }
}
