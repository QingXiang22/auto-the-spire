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
            if (origin.Minimum < 1 || origin.Maximum > 10000 || origin.Minimum >= origin.Maximum
                || origin.Hp < origin.Minimum || origin.Hp >= origin.Maximum)
                throw Missing("native HP creation bounds");
            var available = Enumerable.Range((int)origin.Minimum, (int)(origin.Maximum - origin.Minimum))
                .Where(hp => !origins.Take(i).Any(previous => previous.Hp == hp)).ToArray();
            long selected;
            if (available.Length == 0)
            {
                if (call.Minimum != origin.Minimum.ToString(CultureInfo.InvariantCulture)
                    || call.Maximum != origin.Maximum.ToString(CultureInfo.InvariantCulture))
                    throw Missing("native exhausted HP parameters");
                selected = long.Parse(call.Result, CultureInfo.InvariantCulture);
            }
            else
            {
                if (call.Minimum != "0" || call.Maximum != available.Length.ToString(CultureInfo.InvariantCulture))
                    throw Missing("native HP offset parameters");
                var offset = long.Parse(call.Result, CultureInfo.InvariantCulture);
                if (offset < 0 || offset >= available.Length) throw Missing("native HP offset result");
                selected = available[offset];
            }
            if (call.Stream != "base-spire:run:niche" || call.Kind != "integer" || selected != origin.Hp)
                throw Missing("native initial HP does not match its actual draw");
            // Both producers now execute the same HP offset operation. Retain its actual parameters/result.
            result.Add(call);
        }
        return NormalScalarTrace.Canonical(result);
    }
    private static NotSupportedException Missing(string detail) => new("missing_dimension: " + detail);
}
