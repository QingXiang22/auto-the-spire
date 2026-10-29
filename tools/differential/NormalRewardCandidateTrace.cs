using System.Globalization;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal sealed record NormalRewardCandidateCall(string Kind, long Before, long After, int Selected, string[] Candidates);
internal sealed record NormalRewardDraw(string Operation, long Before, long After, double Minimum, double Maximum, int Arity, string Result);

internal static class NormalRewardCandidateTrace
{
    internal const string Stream = "base-spire:player:rewards";
    internal static NormalRewardCandidateCall[] Dsl(IReadOnlyList<SessionDebugRecord> records, long dropped)
    {
        if (dropped != 0) throw Missing("dropped reward candidate records");
        var calls = new List<NormalRewardCandidateCall>();
        foreach (var record in records.Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == Stream && r.Random?.Operation == "pick"))
        {
            if (record.TextTruncated || record.Random is not { Candidates: { } roots, CandidatesTruncated: false } draw
                || record.Snapshot is not { Complete: true } snapshot || roots.Count != draw.CandidateCount || roots.Count == 0
                || draw.Minimum != "0" || draw.Maximum != roots.Count.ToString(CultureInfo.InvariantCulture))
                throw Missing("incomplete reward candidate snapshot or bounds");
            var entities = snapshot.Entities.ToDictionary(e => e.Instance);
            var items = roots.Select(id => entities.TryGetValue(id, out var entity) ? entity : throw Missing("missing reward candidate root")).ToArray();
            var kind = items[0].Type switch
            {
                "base-spire:PotionBlueprint" => "potion",
                "base-spire:CardBlueprint" => "card",
                _ => throw Missing("unclassified reward candidate type")
            };
            if (items.Any(e => e.Type != items[0].Type)) throw Missing("mixed reward candidate types");
            var candidates = items.Select(e => e.Fields.Single(f => f.Name == "DefinitionId").Value is { Truncated: false, Scalar: { } id }
                ? id : throw Missing("missing reward candidate identity")).ToArray();
            calls.Add(new(kind, draw.Before, draw.After, int.Parse(draw.Result, CultureInfo.InvariantCulture), candidates));
        }
        return Canonical(calls);
    }

    internal static NormalRewardCandidateCall[] Canonical(IEnumerable<NormalRewardCandidateCall> source)
    {
        var calls = source.ToArray(); long previous = -1;
        var selectedCards = new HashSet<string>(StringComparer.Ordinal);
        foreach (var call in calls)
        {
            if (call.Kind is not ("potion" or "card") || call.Before <= previous || call.After != call.Before + 1
                || call.Selected < 0 || call.Selected >= call.Candidates.Length
                || call.Candidates.Any(string.IsNullOrWhiteSpace) || call.Candidates.Distinct(StringComparer.Ordinal).Count() != call.Candidates.Length)
                throw Missing("invalid reward candidate identity, order, index or cursor");
            if (call.Kind == "card")
            {
                if (call.Candidates.Any(selectedCards.Contains)) throw Missing("previously selected reward card remains eligible");
                selectedCards.Add(call.Candidates[call.Selected]);
            }
            previous = call.Before;
        }
        return calls;
    }
    internal static NotSupportedException Missing(string reason) => new("missing_dimension: " + reason);
}

#if REFERENCE_SIM
// Own-source copies made before native reward generation; no DSL pool or fixture is used.
internal sealed class NormalReferenceRewardCandidates
{
    private readonly CardModel[] cards;
    private readonly PotionModel[] potions;
    internal object Sources => new
    {
        cards = cards.Select(c => new { id = NormalReferenceRun.Id(c.Id), rarity = c.Rarity.ToString() }).ToArray(),
        potions = potions.Select(p => new { id = NormalReferenceRun.Id(p.Id), rarity = p.Rarity.ToString() }).ToArray()
    };
    internal NormalReferenceRewardCandidates(Player player)
    {
        var options = new CardCreationOptions([player.Character.CardPool], CardCreationSource.Encounter, CardRarityOddsType.RegularEncounter)
            .WithFlags(CardCreationFlags.IsCardReward | CardCreationFlags.IsFromCombat);
        cards = options.GetPossibleCards(player).ToArray();
        potions = PotionFactory.GetOutOfCombatPool(player).ToArray();
    }
    internal NormalRewardCandidateCall[] Capture(NormalRewardDraw[] draws, RewardsSet rewards)
    {
        var index = 0; var result = new List<NormalRewardCandidateCall>();
        var selected = new HashSet<CardModel>();
        NormalRewardDraw Next(string operation, int arity)
        {
            if (index >= draws.Length) throw NormalRewardCandidateTrace.Missing("missing native reward draw");
            var draw = draws[index++];
            if (draw.Operation != operation || draw.Arity != arity || draw.After != draw.Before + 1
                || index > 1 && draw.Before != draws[index - 2].After)
                throw NormalRewardCandidateTrace.Missing("unexpected native reward operation or cursor");
            return draw;
        }
        void Float() { var draw = Next("NextFloat", 2); if (draw.Minimum != 0 || draw.Maximum != 1) throw NormalRewardCandidateTrace.Missing("native reward float bounds"); }
        void Pick(string kind, string[] candidates, string actual)
        {
            var draw = Next("NextInt", 2); var picked = int.Parse(draw.Result, CultureInfo.InvariantCulture);
            if (draw.Minimum != 0 || draw.Maximum != candidates.Length || picked < 0 || picked >= candidates.Length || candidates[picked] != actual)
                throw NormalRewardCandidateTrace.Missing("native reward selection disagrees with its own source pool or actual offer");
            result.Add(new(kind, draw.Before, draw.After, picked, candidates));
        }
        Float(); // Presence roll precedes Populate of every reward.
        Next("NextInt", 2); // Gold has no candidate domain; its actual bounds are compared by scalar_calls.
        if (rewards.Potion?.Potion is { } potion)
        {
            Float();
            Pick("potion", potions.Where(p => p.Rarity == potion.Rarity).Select(p => NormalReferenceRun.Id(p.Id)).ToArray(), NormalReferenceRun.Id(potion.Id));
        }
        if (rewards.Card.Options.Count != 3) throw NormalRewardCandidateTrace.Missing("unsupported native reward option count");
        foreach (var card in rewards.Card.Options)
        {
            Float();
            var candidates = cards.Where(c => c.Rarity == card.Rarity && !selected.Contains(c)).ToArray();
            Pick("card", candidates.Select(c => NormalReferenceRun.Id(c.Id)).ToArray(), NormalReferenceRun.Id(card.Id));
            selected.Add(candidates[result[^1].Selected]);
            Float();
        }
        if (index != draws.Length) throw NormalRewardCandidateTrace.Missing("unclassified native reward draws");
        return NormalRewardCandidateTrace.Canonical(result);
    }
}
#endif
