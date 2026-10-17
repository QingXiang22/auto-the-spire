namespace Differential;

internal static partial class NormalRewardEntry
{
    private static bool IsActRun(FixtureCase fixture) => fixture.Script.Single().Action == "normal_act1";

    // One continuous Run policy. Room count is observation, never its stopping condition.
    private static void DriveAct(Side side, List<BoundaryRecord> records, List<NormalSemanticAction> plan, bool defensive)
    {
        void Capture(string label, IReadOnlyList<DecisionSnapshot> decisions) => records.Add(new(
            records.Count, label, label, side.Actions(), decisions, side.Facts(),
            side.Streams(records.Count, label), side.Diagnostic()));
        Capture("start", []);
        for (var step = 0; step < 2000; step++)
        {
            if (side.Phase is "defeat" or "act-completed") return;
            var actions = side.Actions();
            var facts = side.Facts().ToDictionary(f => f.Path, f => f.Value);
            string action; string? token = null;
            bool Has(string name) => actions.Any(a => a.Action == "base-spire:" + name);
            string First(string name) => actions.Single(a => a.Action == "base-spire:" + name).Candidates.Order(StringComparer.Ordinal).First();
            if (Has("ChooseMapPoint"))
            {
                action = "base-spire:ChooseMapPoint";
                token = actions.Single(a => a.Action == action).Candidates.OrderBy(t => facts["map." + t + ".type"] switch
                {
                    "type:4" when int.Parse(facts["run.hp"]) < int.Parse(facts["run.max_hp"]) => -1,
                    "monster" or "type:7" => 0, "type:3" => 1, "type:4" => 2,
                    "type:2" => 3, "type:6" => 4, _ => 5,
                }).ThenBy(t => t, StringComparer.Ordinal).First();
            }
            else if (side.Phase == "event")
                action = Has("AromaOfChaosMaintainControl") ? "base-spire:AromaOfChaosMaintainControl"
                    : Has("SlipperyBridgeOvercome") ? "base-spire:SlipperyBridgeOvercome"
                    : actions.First(a => a.Action != "base-spire:DiscardPotion").Action;
            else if (side.Phase == "rewards")
            {
                action = Has("TakeRewardCard") ? "base-spire:TakeRewardCard"
                    : Has("SkipRewardPotion") ? "base-spire:" + (Has("TakeRewardPotion") ? "TakeRewardPotion" : "SkipRewardPotion")
                    : Has("SkipRewardRelic") ? "base-spire:SkipRewardRelic" : "base-spire:TakeRewardGold";
                if (action.EndsWith("TakeRewardCard", StringComparison.Ordinal)) token = First("TakeRewardCard");
            }
            else if (side.Phase == "rest")
            {
                action = int.Parse(facts["run.hp"]) < int.Parse(facts["run.max_hp"]) || !Has("SmithAtCampfire")
                    ? "base-spire:RestAtCampfire" : "base-spire:SmithAtCampfire";
                if (action.EndsWith("SmithAtCampfire", StringComparison.Ordinal)) token = First("SmithAtCampfire");
            }
            else if (side.Phase == "treasure")
                action = "base-spire:" + (Has("OpenTreasureChest") ? "OpenTreasureChest" : "SkipTreasureRelic");
            else if (side.Phase == "shop") action = "base-spire:LeaveShop";
            else if (side.Phase == "combat")
            {
                if (int.Parse(facts["run.room"]) > 6 && Has("UsePotion"))
                { action = "base-spire:UsePotion"; token = First("UsePotion"); }
                else
                {
                    token = actions.FirstOrDefault(a => a.Action == "base-spire:PlayCard")?.Candidates.OrderBy(t =>
                    {
                        var card = facts["visible." + t + ".definition"];
                        if (card == "base-spire:flame-barrier" || card == "base-spire:headbutt"
                            && facts["pile.discard.sorted"].Contains("base-spire:flame-barrier", StringComparison.Ordinal)) return -3;
                        if (defensive && int.Parse(facts["player.hp"]) <= 20 && card == "base-spire:defend-ironclad") return -2;
                        if (facts["visible." + t + ".kind"] == "power") return -2;
                        return card switch { "base-spire:bash" => 0, "base-spire:strike-ironclad" => 1,
                            "base-spire:defend-ironclad" => 2, _ => 3 };
                    }).ThenBy(t => t, StringComparer.Ordinal).FirstOrDefault();
                    action = token is null ? "base-spire:EndTurn" : "base-spire:PlayCard";
                }
            }
            else throw new CheckFailed("Normal Run reached an unadapted phase: " + side.Phase);
            var visible = VisibleCandidates(side.Facts());
            var decisions = side.Execute(action, token);
            plan.Add(new(action, token, actions, visible, decisions));
            Capture("step:" + step + ":" + action + (token is null ? "" : ":" + token), decisions);
        }
        throw new CheckFailed("Normal Run exceeded 2000 actions before a real outcome");
    }
}
