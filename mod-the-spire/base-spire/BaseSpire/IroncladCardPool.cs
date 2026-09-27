using CoreTheSpire;

namespace BaseSpire;

internal static class IroncladCardPool
{
    public static void Register(ContentRegistry registry) => registry.Register(new CardPoolDefinition(
        BaseSpireIds.IroncladCardPool,
        [
            BaseSpireIds.Anger, BaseSpireIds.Armaments, BaseSpireIds.Bash, BaseSpireIds.Barricade,
            BaseSpireIds.BattleTrance, BaseSpireIds.Bloodletting, BaseSpireIds.Bludgeon, BaseSpireIds.BodySlam,
            BaseSpireIds.BurningPact, BaseSpireIds.Clothesline, BaseSpireIds.DefendIronclad,
            BaseSpireIds.DemonForm, BaseSpireIds.FeelNoPain, BaseSpireIds.Headbutt, BaseSpireIds.Inflame,
            BaseSpireIds.Impervious, BaseSpireIds.IronWave, BaseSpireIds.Offering, BaseSpireIds.PommelStrike,
            BaseSpireIds.SecondWind, BaseSpireIds.ShrugItOff, BaseSpireIds.StrikeIronclad,
            BaseSpireIds.SwordBoomerang, BaseSpireIds.Thunderclap, BaseSpireIds.TrueGrit,
            BaseSpireIds.TwinStrike, BaseSpireIds.Uppercut, BaseSpireIds.Whirlwind
        ]));
}
