#if REFERENCE_SIM
namespace Differential;

internal static class NormalReferenceEventExclusions
{
    // Locked normal-path dispatch glue. The actual invoked model methods are
    // classified independently below; a new override is never made a default by
    // its dispatcher's presence here. Outputs also remain in the raw journal and
    // the independent state, public, candidate, history and random comparisons.
    private static readonly HashSet<string> DispatchGlue = [
        "AfterAutoPrePlayPhaseEntered", "AfterCardChangedPiles", "AfterCardDrawn", "AfterMapGenerated",
        "AfterModifyingCardPlayResultLocation", "AfterModifyingHandDraw", "AfterModifyingHpLostAfterOsty",
        "AfterModifyingHpLostBeforeOsty", "AfterModifyingPowerAmountReceived", "AfterPowerAmountChanged",
        "AfterRoomEntered", "BeforeCombatRewardOffered", "BeforeDeath", "BeforePowerAmountChanged", "BeforeRoomEntered",
        "CanRerollCardReward", "ModifyAttackHitCount", "ModifyCardBeingAddedToDeck", "ModifyCardRewardCreationOptions",
        "ModifyDamage", "ModifyEnergyCostInCombat", "ModifyGeneratedMap", "ModifyHandDraw", "ModifyHpLost",
        "ModifyMaxEnergy", "ModifyPowerAmountGiven", "ModifyPowerAmountReceived", "ModifyRewards", "ModifyStarCostInCombat",
        "ModifyUnblockedDamageTarget", "ShouldAllowAncient", "ShouldAllowHitting", "ShouldCreatureBeRemovedFromCombatAfterDeath",
        "ShouldDie", "ShouldDieLate", "ShouldDraw", "ShouldFlush", "ShouldForcePotionReward", "ShouldPlay",
        "ShouldPlayerResetEnergy", "ShouldPowerBeRemovedAfterOwnerDeath", "ShouldPowerBeRemovedOnDeath", "ShouldProcurePotion",
        "ShouldStopCombatFromEnding", "ShouldTakeExtraTurn", "TryModifyCardRewardOptionLate", "TryModifyCardRewardOptions"];
    private static readonly HashSet<string> Queries = ["ModifyEnergyCostInCombat", "ShouldPlay", "ModifyStarCostInCombat", "ShouldDraw", "ShouldAllowHitting"];

    internal static NormalEventExclusion[] Audit(NormalNativeHookRecord[] records, IReadOnlyList<NormalEventSource> sources, List<string> unmapped)
    {
        var bySequence = records.ToDictionary(r => r.Sequence);
        var mapped = sources.Select(s => bySequence[s.Sequence].Call).ToHashSet();
        var entries = records.Where(r => r.Stage == "enter").ToDictionary(r => r.Call);
        var excluded = new Dictionary<(string Subject, string Reason), int>();
        foreach (var r in records.Where(r => r.Stage is "enter" or "point"))
        {
            if (r.Context != "query" && mapped.Contains(r.Call) && r.Call != 0) continue;
            string? reason = null;
            if (r.Context == "query")
            {
                if (r.Kind == "dispatch" && Queries.Contains(r.Name)) reason = "read-only-public-query: actual inputs/results retained and public candidates compared";
                else if (r.Kind == "listener" && r.Method?.DefaultImplementation is not null) reason = "read-only-query-default: " + r.Method.DefaultImplementation;
                // A real override needs a semantic mapping even when a query has no state delta.
            }
            else if (r.Kind == "listener")
            {
                if (r.Method?.DefaultImplementation is { } body) reason = "actual-default-body: " + body;
                else if (r.Method?.DefaultDeclaration == true && r.Name is "AfterDeath" or "AfterCardGenerated" or "TryModifyCardRewardOptionLate")
                    reason = "virtual-forwarding: actual child invocation audited independently";
                else if (r.Method?.DeclaringType == "Sts2Sim.Core.Models.Relics.BoomingConch" && r.Name == "AfterSideTurnStart"
                    && r.Arguments[0].Scalar == "Enemy") reason = "guarded-override: actual participants exclude owner; completed invocation preserves energy";
            }
            else if (r.Kind == "dispatch")
            {
                if (entries.TryGetValue(r.Parent, out var parent) && parent.Kind == "dispatch" && parent.Name == r.Name)
                    reason = "dispatch-forwarding: actual enclosing overload and every model callback retained";
                else if (DispatchGlue.Contains(r.Name)) reason = "dispatch-glue: actual default bodies, forwarding and overrides audited individually; intrinsic outputs independently compared";
            }
            else if (r.Kind == "observer" && sources.Any(s => s.Sequence == r.Sequence)) continue;
            else if (r.Kind == "observer" && r.Name == "SideTurnEndListener") reason = "listener-boundary: paired against adjacent actual model entry/exit, instance and side";
            if (reason is null) unmapped.Add(r.Sequence + ":unclassified:" + r.Kind + ":" + r.Method?.DeclaringType + "." + r.Name);
            else
            {
                var key = (r.Method?.DeclaringType + "." + r.Name, reason);
                excluded[key] = excluded.GetValueOrDefault(key) + 1;
            }
        }
        return excluded.Select(p => new NormalEventExclusion(p.Key.Subject, p.Key.Reason, p.Value)).ToArray();
    }
}
#endif
