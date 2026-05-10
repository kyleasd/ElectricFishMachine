using HarmonyLib;
using StardewValley;
using StardewValley.Tools;

namespace ElectricFishMachine;

/// <summary>
/// 禁用「电鱼机」抛竿：拦截 <see cref="FishingRod.beginUsing"/>，并像原版「体力不足」分支一样解除使用工具状态，避免卡住不能走。
/// </summary>
[HarmonyPatch(typeof(FishingRod), nameof(FishingRod.beginUsing))]
internal static class FishingRodDisableCastPatch
{
    private static bool Prefix(FishingRod __instance, GameLocation location, int x, int y, Farmer who, ref bool __result)
    {
        if (!CustomToolData.IsElectricFishMachineRod(__instance))
            return true;

        if (who.IsLocalPlayer)
        {
            who.CanMove = !Game1.eventUp;
            who.UsingTool = false;
            who.canReleaseTool = false;
            who.FarmerSprite.PauseForSingleAnimation = false;
        }

        __instance.doneFishing(null);
        __result = true;
        return false;
    }
}
