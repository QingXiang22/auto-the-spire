using System.Globalization;

namespace Differential;

internal sealed record NormalHpOrigin(string Enemy, long Minimum, long Maximum, long Hp);

internal static class NormalHpTrace
{
    internal static NormalScalarCall[] Normalize(NormalHpOrigin[] origins, NormalScalarCall[] offsets)
    {
        if (origins.Length == 0 || origins.Length != offsets.Length) throw Missing("native HP creation/draw association");
        var result = new List<NormalScalarCall>();
        for (var i = 0; i < origins.Length; i++)
        {
            var origin = origins[i]; var call = offsets[i];
            if (origin.Minimum >= origin.Maximum || origin.Hp < origin.Minimum || origin.Hp >= origin.Maximum
                || origins.Take(i).Any(previous => previous.Hp >= origin.Minimum && previous.Hp < origin.Maximum))
                throw Missing("native HP exclusions require ordered available-candidate mapping");
            if (call.Stream != "base-spire:run:niche" || call.Kind != "integer" || call.Minimum != "0"
                || call.Maximum != (origin.Maximum - origin.Minimum).ToString(CultureInfo.InvariantCulture))
                throw Missing("native HP offset parameters");
            var hp = checked(origin.Minimum + long.Parse(call.Result, CultureInfo.InvariantCulture));
            if (hp != origin.Hp) throw Missing("native initial HP does not match its actual draw");
            result.Add(NormalScalarTrace.Integer(call.Stream, call.Before, call.After, origin.Minimum, origin.Maximum, hp));
        }
        return NormalScalarTrace.Canonical(result);
    }
    private static NotSupportedException Missing(string detail) => new("missing_dimension: " + detail);
}
