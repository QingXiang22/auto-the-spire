using System.Globalization;

namespace Differential;

internal sealed record NormalMapCall(string Kind, string Context, long Before, long After, long Selected, string[] Candidates);
internal sealed record NormalMapGaussian(int Mean, int Deviation, int Minimum, int Maximum, int Result, long Before, long After);
internal sealed record NormalMapCandidates(NormalMapCall[] Calls, NormalMapGaussian[] Gaussians);

internal static class NormalMapCandidatesTrace
{
    internal const string Stream = "base-spire:run:act_1_map";
    internal static NotSupportedException Missing(string reason) => new("missing_dimension: " + reason);
    internal static string Coordinate(long col, long row) => col.ToString(CultureInfo.InvariantCulture) + "," + row.ToString(CultureInfo.InvariantCulture);
    internal static NormalMapCandidates Validate(NormalMapCandidates value)
    {
        long cursor = 0;
        foreach (var c in value.Calls)
        {
            if (c.Before != cursor++ || c.After != cursor || string.IsNullOrEmpty(c.Context)) throw Missing("map call order or cursor");
            if (c.Kind == "gaussian")
            {
                if (c.Candidates.Length != 0 || c.Selected < 0 || c.Selected >= 9007199254740992L) throw Missing("map Gaussian input");
            }
            else if (c.Kind is not ("start" or "offsets" or "assign" or "repair" or "prune")
                || c.Candidates.Length < 2 || c.Selected < 0 || c.Selected >= c.Candidates.Length)
                throw Missing("map candidate kind, count or index");
        }
        var inputs = value.Calls.Where(c => c.Kind == "gaussian").ToArray();
        if (value.Gaussians.Length != 2 || value.Gaussians[0].Before != 0 || value.Gaussians[1].Before != value.Gaussians[0].After
            || value.Gaussians[^1].After != inputs.Length) throw Missing("map Gaussian coverage");
        foreach (var g in value.Gaussians)
        {
            if (g.After <= g.Before || (g.After - g.Before) % 2 != 0 || g.Deviation != 1 || g.Minimum > g.Maximum)
                throw Missing("map Gaussian parameters or attempts");
            for (var i = g.Before; i < g.After; i += 2)
            {
                var left = 1d - inputs[i].Selected / 9007199254740992d;
                var right = 1d - inputs[i + 1].Selected / 9007199254740992d;
                var result = checked((int)Math.Round(g.Mean + Math.Sqrt(-2d * Math.Log(left)) * Math.Sin(Math.PI * 2d * right) * g.Deviation));
                var accepted = result >= g.Minimum && result <= g.Maximum;
                if (accepted != (i + 2 == g.After) || accepted && result != g.Result) throw Missing("map Gaussian rejection or actual result");
            }
        }
        return value;
    }
}
