using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

internal static class TreasureRelicBagChecks
{
    public static void Run()
    {
        Check.Run("宝箱遗物袋取出与回退", () =>
        {
            var common = SharedRelicPoolManifest.ForTreasure.First(entry => entry.Rarity == "Common");
            var rare = SharedRelicPoolManifest.ForTreasure.First(entry => entry.Rarity == "Rare");
            var bag = TreasureRelicBag.FromOrder([common, rare]);

            Check.Equal(common, bag.Pull("Common"), "Common 从袋头取出");
            Check.Equal(rare, bag.Pull("Common"), "Common 空袋向 Rare 回退");
            Check.Equal<SharedRelicPoolEntry?>(null, bag.Pull("Common"), "所有候选耗尽后返回空");
            Check.Equal(0, bag.Remaining("Rare"), "取出后袋计数递减");
        });

        Check.Run("宝箱遗物袋池外校验", () =>
        {
            var invalid = new SharedRelicPoolEntry("NotARealRelic", "Common");
            var rejected = false;
            try { _ = TreasureRelicBag.FromOrder([invalid]); }
            catch (ArgumentException) { rejected = true; }
            Check.True(rejected, "池外遗物不能进入宝箱袋");
        });
    }
}
