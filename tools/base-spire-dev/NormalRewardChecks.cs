using System.Globalization;
using System.Text.Json;
using CoreTheSpire;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunNormalRewardChecks(DslPackage package, Action<string, Action> check)
    {
        var names = new List<string>();
        void Case(string name, Action body) { check(name, body); names.Add(name); }
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-reward-vectors.json")!);
        using var copy = new Copy(package.Root);
        copy.Include("normal-reward-probes.spire", """
            action OfferNormalProbe { role = player body = {
              call = { procedure = WhenCombatEnded args = { owner = first(type(Player)) victory = true } }
            } }
            action UnsupportedPotionProbe { role = player body = {
              call = { procedure = UsePotion args = { owner = first(type(Player)) potion = first(collection(Potions)) } }
            } }
            """);
        var input = copy.Load();
        Session Offered(string seed = "777", DslPackage? rules = null)
        {
            var s = (rules ?? input).CreateSession(1, ["player"], seed); s.Start(); Healthy(s);
            Submit(s, "Ironclad.Initialize"); Submit(s, "OfferNormalProbe"); return s;
        }
        InstanceId Context(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("CurrentRewardSet")).AsRef();
        string PotionId(Session s, InstanceId p) => s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Potion").FieldIndex("DefinitionId")).AsString();

        Case("NR01: all 48 source potion candidates preserve order, rarity and explicit effect support", () =>
        {
            var s = Start(package); var blueprint = s.RuleSet.TypeByQualifiedId("base-spire:PotionBlueprint");
            var actual = Members(s, "PotionLibrary"); var expected = data.RootElement.GetProperty("potions").EnumerateArray().ToArray();
            Equal(48, actual.Count, "three character and 45 shared candidates");
            for (var i = 0; i < expected.Length; i++)
            {
                Equal(expected[i].GetProperty("id").GetString(), s.State.GetField(actual[i], blueprint.FieldIndex("DefinitionId")).AsString(), "source pool position");
                Equal(expected[i].GetProperty("rarity").GetString(), s.State.GetField(actual[i], blueprint.FieldIndex("Rarity")).AsString(), "source rarity");
                Equal(expected[i].GetProperty("effect_supported").GetBoolean(), s.State.GetField(actual[i], blueprint.FieldIndex("EffectSupported")).AsBool(), "metadata is not an effect implementation");
            }
            Equal(7, actual.Count(b => s.State.GetField(b, blueprint.FieldIndex("EffectSupported")).AsBool()), "seven effects have real consumers, including FlexPotion");
        });
        foreach (var vector in data.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var seed = vector.GetProperty("seed").GetString()!;
            Case("NR source full ordinary rewards, ordered candidates and every random call: " + JsonSerializer.Serialize(seed), () =>
            {
                var s = input.CreateSession(1, ["player"], seed, new SessionDebugOptions(MaxRecords: 100_000)); s.Start(); Healthy(s);
                Submit(s, "Ironclad.Initialize");
                foreach (var expected in vector.GetProperty("offers").EnumerateArray())
                {
                    var start = s.Debug!.Records.Count;
                    Submit(s, "OfferNormalProbe");
                    Equal(expected.GetProperty("gold").GetInt64(), Field(s, "GoldRewardAmount"), "gold after potion drop roll");
                    var potion = expected.GetProperty("potion"); var offered = Members(s, "PotionRewardOptions");
                    Equal(potion.ValueKind == JsonValueKind.Null ? 0 : 1, offered.Count, "actual potion drop");
                    if (offered.Count == 1) Equal(potion.GetString(), PotionId(s, offered[0]), "full source potion result");
                    var cards = Members(s, "CardRewardOptions"); var expectedCards = expected.GetProperty("cards").EnumerateArray().ToArray();
                    for (var i = 0; i < 3; i++)
                    {
                        Equal(expectedCards[i].GetProperty("id").GetString(), CardDefinition(s, cards[i]), "card after potion population");
                        Equal(expectedCards[i].GetProperty("upgraded").GetBoolean(), CardUpgraded(s, cards[i]), "upgrade result");
                    }
                    Equal(expected.GetProperty("odds_bits").GetInt64(), Field(s, "PotionRewardOdds"), "binary32 pity odds across visits");
                    Equal(expected.GetProperty("offset_bits").GetInt64(), Field(s, "CardRarityOffset"), "binary32 card offset");
                    Equal(expected.GetProperty("counter").GetInt64(), s.Random.DrawCount(RewardStream), "complete shared cursor");
                    Equal(expected.GetProperty("visit").GetInt64(), IntField(s, Context(s), "CombatRewardSet", "VisitId"), "visit identity");
                    var calls = s.Debug.Records.Skip(start).Where(r => r.Random is not null).ToArray();
                    var expectedCalls = expected.GetProperty("calls").EnumerateArray().ToArray();
                    Equal(expectedCalls.Length, calls.Length, "no missing or extra draws"); Equal(0L, s.Debug.DroppedRecords, "complete debug evidence");
                    for (var i = 0; i < calls.Length; i++)
                    {
                        var call = calls[i].Random!; var e = expectedCalls[i]; var op = e.GetProperty("operation").GetString();
                        Equal(RewardStream, calls[i].Subject, "source stream");
                        Equal(e.GetProperty("before").GetInt64(), call.Before, "cursor before " + e.GetProperty("label"));
                        Equal(e.GetProperty("after").GetInt64(), call.After, "cursor after");
                        Equal(op == "sampleFloat32" ? "double" : op == "samplePick" ? "pick" : "int", call.Operation, "draw operation");
                        if (op == "sampleFloat32")
                        {
                            Equal("0", call.Minimum, "float minimum"); Equal("1", call.Maximum, "float maximum");
                            Equal(e.GetProperty("result").GetInt64(), (long)BitConverter.SingleToUInt32Bits((float)double.Parse(call.Result, CultureInfo.InvariantCulture)), "independent float result");
                        }
                        else
                        {
                            Equal(e.GetProperty("min").ToString(), call.Minimum, "integer minimum"); Equal(e.GetProperty("max").ToString(), call.Maximum, "integer maximum");
                            if (op == "samplePick")
                            {
                                True(!call.CandidatesTruncated, "complete candidates");
                                var type = e.GetProperty("label").GetString() == "potion-pick" ? "PotionBlueprint" : "CardBlueprint";
                                var field = s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex("DefinitionId");
                                var ids = call.Candidates!.Select(c => s.State.GetField(c, field).AsString()).ToArray();
                                True(ids.SequenceEqual(e.GetProperty("candidates").EnumerateArray().Select(x => x.GetString())), "source ordered candidate domain");
                                Equal(e.GetProperty("result").GetString(), ids[int.Parse(call.Result, CultureInfo.InvariantCulture)], "selected identity");
                            }
                            else Equal(e.GetProperty("result").ToString(), call.Result, "integer result");
                        }
                    }
                    var state = s.State.StateHash(); var cursor = s.Random.TotalDraws;
                    s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
                    Equal(state, s.State.StateHash(), "queries preserve state"); Equal(cursor, s.Random.TotalDraws, "queries preserve RNG");
                    Submit(s, "CloseDevelopmentRewards");
                    Equal(0, Members(s, "PotionRewardOptions").Count, "close clears pending potion"); Equal(cursor, s.Random.TotalDraws, "close does not reroll");
                }
            });
        }
        // Pick an independently generated first-visit drop; no production RNG search or forced potion injection.
        var dropSeed = data.RootElement.GetProperty("vectors").EnumerateArray().First(v => v.GetProperty("offers")[0].GetProperty("potion").ValueKind == JsonValueKind.String).GetProperty("seed").GetString()!;
        Case("NR02: full inventory preserves offer, discard reuses first free slot, claim transfers the same instance once", () =>
        {
            using var ordered = new Copy(input.Root);
            ordered.Include("potion-order.spire", """
                procedure InspectPotionObtain { parameters = { owner = Ref(Player) potion = Ref(Potion) } body = {
                  if = { condition = less(collectionIndex(Potions, potion), 0) then = { fail = { message = "obtain notification precedes inventory transfer" } } }
                } }
                subscription InspectPotionObtain { event = PotionObtained reaction = InspectPotionObtain }
                """);
            var s = ordered.Load().CreateSession(1, ["player"], dropSeed); s.Start(); Healthy(s); Submit(s, "Ironclad.Initialize");
            Submit(s, "BlockPotion.Obtain"); Submit(s, "EnergyPotion.Obtain"); Submit(s, "FruitJuice.Obtain");
            Submit(s, "OfferNormalProbe"); var offer = Members(s, "PotionRewardOptions").Single(); var context = Context(s);
            var before = s.State.StateHash(); var cursor = s.Random.TotalDraws;
            Submit(s, "ClaimDevelopmentPotionReward"); Equal(before, s.State.StateHash(), "source TooFull leaves all reward state unchanged");
            var discarded = Members(s, "Potions")[1]; Submit(s, "DiscardPotion", discarded);
            True(!s.State.Exists(discarded), "discard destroys old instance");
            Submit(s, "ClaimDevelopmentPotionReward");
            True(Members(s, "Potions").Contains(offer), "same offered identity acquired"); Equal(1L, IntField(s, offer, "Potion", "Slot"), "first free source slot reused");
            True(RewardPending(s) && FieldBool(s, "CardRewardPending"), "other rewards remain");
            RejectRewardAction(s, "ClaimDevelopmentPotionReward"); Equal(cursor, s.Random.TotalDraws, "full retry, discard and claim do not draw");
            Submit(s, "ClaimDevelopmentGoldReward"); Submit(s, "ClaimDevelopmentCardReward", Members(s, "CardRewardOptions")[0]); Submit(s, "CloseDevelopmentRewards");
            True(s.State.Exists(offer), "closing never destroys acquired potion"); True(s.State.GetField(context, s.RuleSet.TypeByQualifiedId("base-spire:CombatRewardSet").FieldIndex("Closed")).AsBool(), "closed visit receipt retained");
        });
        Case("NR03: skip, close, unsupported effects and fresh visits preserve ownership boundaries", () =>
        {
            foreach (var skip in new[] { false, true })
            {
                var s = Offered(dropSeed); var old = Members(s, "PotionRewardOptions").Single(); var cursor = s.Random.TotalDraws;
                if (skip) { Submit(s, "SkipDevelopmentPotionReward"); True(RewardPending(s) && FieldBool(s, "CardRewardPending"), "potion skip is independent"); }
                Submit(s, "CloseDevelopmentRewards"); True(!s.State.Exists(old), "unclaimed instance destroyed"); Equal(cursor, s.Random.TotalDraws, "skip/close no draw");
                Submit(s, "OfferNormalProbe"); True(Members(s, "PotionRewardOptions").All(p => p != old), "new visit never reuses discarded offer");
            }
            var unsupportedSeed = data.RootElement.GetProperty("vectors").EnumerateArray().First(v => {
                var p = v.GetProperty("offers")[0].GetProperty("potion");
                return p.ValueKind == JsonValueKind.String && !data.RootElement.GetProperty("potions").EnumerateArray().Single(x => x.GetProperty("id").GetString() == p.GetString()).GetProperty("effect_supported").GetBoolean();
            }).GetProperty("seed").GetString()!;
            var u = Offered(unsupportedSeed); var potion = Members(u, "PotionRewardOptions").Single(); Submit(u, "ClaimDevelopmentPotionReward");
            True(Members(u, "Potions").Contains(potion), "unsupported effect does not shrink source reward pool");
            True(!u.LegalActions(u.Participant(0)).Any(a => a.Definition.QualifiedId is "base-spire:UsePotion" or "base-spire:UsePotionOnTarget"), "unsupported effects never offered as empty use");
            var state = u.State.StateHash(); var draws = u.Random.TotalDraws;
            Throws<EngineFaultException>(() => Submit(u, "UnsupportedPotionProbe"));
            True(u.Fault!.ToString().Contains("unsupported_capability", StringComparison.Ordinal), "direct use reports the unsupported effect");
            Equal(state, u.State.StateHash(), "unsupported effect cannot consume a potion"); Equal(draws, u.Random.TotalDraws, "unsupported effect cannot draw");
        });
        Case("NR04: unsupported contexts, duplicate visits and missing pool fail before random consumption", () =>
        {
            foreach (var args in new[] { "visitId = 1 source = \"elite\" actIndex = 0 ascension = 0", "visitId = 1 source = \"monster\" actIndex = 1 ascension = 0", "visitId = 1 source = \"monster\" actIndex = 0 ascension = 1", "visitId = 0 source = \"monster\" actIndex = 0 ascension = 0" })
            {
                using var invalid = new Copy(package.Root);
                invalid.Include("invalid-reward.spire", "action InvalidReward { role = player body = { call = { procedure = OfferNormalCombatRewards args = { owner = first(type(Player)) " + args + " } } } }");
                var s = Start(invalid.Load()); Submit(s, "Ironclad.Initialize"); Throws<EngineFaultException>(() => Submit(s, "InvalidReward"));
                Equal(0L, s.Random.TotalDraws, "unsupported input fails before random draws"); Equal(0, Members(s, "CardRewardOptions").Count, "no partial cards");
            }
            using var duplicate = new Copy(input.Root);
            duplicate.Include("duplicate-reward.spire", "action DuplicateReward { role = player body = { call = { procedure = OfferNormalCombatRewards args = { owner = first(type(Player)) visitId = 1 source = \"monster\" actIndex = 0 ascension = 0 } } } }");
            var d = Offered(dropSeed, duplicate.Load()); Submit(d, "CloseDevelopmentRewards"); var before = d.Random.TotalDraws;
            Throws<EngineFaultException>(() => Submit(d, "DuplicateReward")); Equal(before, d.Random.TotalDraws, "closed visit cannot generate again");
            var missing = Start(input); Submit(missing, "Ironclad.Initialize"); var row = Members(missing, "PotionLibrary")[0]; missing.State.RunAtomic(() => missing.State.DestroyInstance(row));
            Throws<EngineFaultException>(() => Submit(missing, "OfferNormalProbe")); Equal(0L, missing.Random.TotalDraws, "missing real pool never becomes filtered success");
        });
        Case("NR05: acquisition can pause after transfer and resumes without duplicate obtain or reroll", () =>
        {
            using var paused = new Copy(input.Root);
            paused.Include("pause-potion.spire", """
                procedure PausePotion { parameters = { owner = Ref(Player) potion = Ref(Potion) } locals = { answer = Bool } body = {
                  decide = { participant = field(owner, Player.Owner) kind = Bool prompt = "obtain probe" into = answer }
                } }
                subscription PausePotion { event = PotionObtained reaction = PausePotion }
                """);
            var s = Offered(dropSeed, paused.Load()); var offer = Members(s, "PotionRewardOptions").Single(); var cursor = s.Random.TotalDraws;
            Submit(s, "ClaimDevelopmentPotionReward"); Equal(SessionStatus.AwaitingDecision, s.Status, "hook pauses actual acquisition");
            True(Members(s, "Potions").Contains(offer), "transfer before hook"); var request = s.PendingDecision!; var state = s.State.StateHash();
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request.RequestId + 1, Flag: true))); Equal(state, s.State.StateHash(), "bad answer atomic");
            s.Answer(new DecisionAnswer(request.RequestId, Flag: true)); Healthy(s); Equal(1, Members(s, "Potions").Count, "one acquisition");
            Equal(0, Members(s, "PotionRewardOptions").Count, "resumed claim finishes once"); Equal(cursor, s.Random.TotalDraws, "resume does not reroll");
            RejectRewardAction(s, "ClaimDevelopmentPotionReward");
        });
        return new() { ["base-spire:normal-combat-rewards"] = names.ToArray() };
    }
}
