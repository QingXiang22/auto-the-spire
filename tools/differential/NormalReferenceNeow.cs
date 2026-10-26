#if REFERENCE_SIM
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;

namespace Differential;

internal static class NormalReferenceNeow
{
    internal static NormalNeowOption Read(EventOption option, long position)
    {
        // The locked AncientEventModel.RelicOption constructs an actual Obtain callback;
        // EventOption.IsEnabled reads whether that callback exists. This is not a DSL
        // support list or a claim that all downstream content mechanics have been verified.
        var type = typeof(Neow).Assembly.GetType("Sts2Sim.Core.Models.Relics." + option.Key);
        if (type is null || !typeof(RelicModel).IsAssignableFrom(type) || !ModelDb.Contains(type))
            throw new NotSupportedException("missing_dimension: native Neow option has no registered relic callback target: " + option.Key);
        if (option.IsEnabled == option.IsLocked) throw new NotSupportedException("missing_dimension: inconsistent native option callback state");
        return new(NormalReferenceRun.Id(ModelDb.Get(type).Id), position, option.IsEnabled);
    }
}
#endif
