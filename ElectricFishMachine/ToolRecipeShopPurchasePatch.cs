using HarmonyLib;
using StardewValley;

namespace ElectricFishMachine;

/// <summary>
/// 原版 <see cref="StardewValley.Object"/> 在 <c>IsRecipe</c> 时会让 <c>actionWhenPurchased</c> 返回 true，商店立刻 <c>LearnRecipe</c>；
/// <see cref="Tool"/> 仅调用 <see cref="StardewValley.Item.actionWhenPurchased"/>（恒为 false），工具类「配方」会卡在光标上。
/// 对电鱼机配方让返回值与 Object 一致，购买即学会。
/// </summary>
[HarmonyPatch(typeof(Tool), nameof(Tool.actionWhenPurchased))]
internal static class ToolRecipeShopPurchasePatch
{
    private static void Postfix(Tool __instance, ref bool __result)
    {
        if (__result || !__instance.IsRecipe)
            return;

        if (string.Equals(__instance.Name, CustomToolData.ToolId, StringComparison.OrdinalIgnoreCase))
            __result = true;
    }
}
