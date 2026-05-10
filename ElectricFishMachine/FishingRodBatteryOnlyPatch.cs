using HarmonyLib;
using StardewValley.Tools;

namespace ElectricFishMachine;

/// <summary>
/// 游戏只会从本体程序集解析自定义 ClassName，模组里的子类会变成错误物品。
/// 因此对「电鱼机」使用原版 <see cref="FishingRod"/>，再在此处限制挂件只能是电池组。
/// </summary>
[HarmonyPatch(typeof(FishingRod), nameof(FishingRod.canThisBeAttached))]
internal static class FishingRodBatteryOnlyPatch
{
    internal const string BatteryPackItemId = "787";

    private static bool Prefix(FishingRod __instance, StardewValley.Object? o, ref bool __result)
    {
        if (!CustomToolData.IsElectricFishMachineRod(__instance))
            return true;

        if (o == null)
        {
            __result = false;
            return false;
        }

        __result = IsBatteryPack(o);
        return false;
    }

    internal static bool IsBatteryPack(StardewValley.Object obj)
    {
        if (obj.bigCraftable.Value)
            return false;

        return obj.ItemId == BatteryPackItemId
            || string.Equals(obj.QualifiedItemId, "(O)" + BatteryPackItemId, StringComparison.OrdinalIgnoreCase);
    }
}
