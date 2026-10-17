using CoreTheSpire;
using BaseSpire.Slice.Effects;

namespace BaseSpire.Slice;

public static partial class SpireCardSource
{
    // Potion effects share the existing syntax reader, linker and compiler. Only bindings that
    // are meaningful without a played-card instance may be admitted by this frontend.
    public static CardEffectProgram ReadPotionProgram(DslValueNode value, string target,
        IReadOnlyDictionary<string, string> powers)
    {
        var problems = new List<string>();
        var program = ReadEffectProgram(value, "potion.use", target, null, false, ref problems, out _);
        foreach (var effect in program.Effects)
            if (effect is not PrimitiveCardEffect { Amount.Source: CardAmountSource.Literal }
                && effect is not GenerateCardsEffect { Choose: true }
                && !(effect is ForEachCardEffect { From: CardEffectPile.Hand, Filter: CardEffectCardFilter.Any } each
                    && each.Body.Effects.All(child => child is UpgradeSelectedCardEffect)))
                problems.Add($"{effect.Source}: potion effect requires an unbound card mechanism");
        string ResolvePower(string name, DslSourceLocation location)
        {
            if (powers.TryGetValue(name, out var id) || powers.Values.Contains(name, StringComparer.Ordinal))
                return id ?? name;
            problems.Add($"{location}: unregistered power '{name}'");
            return name;
        }
        program = LinkEffectProgram(program, ResolvePower, (id, _) => id, (id, _) => id);
        if (program.Effects.Length == 0) problems.Add($"{value.Location}: empty potion use is not supported");
        if (problems.Count > 0) throw new FormatException(string.Join("; ", problems));
        return program;
    }
}
