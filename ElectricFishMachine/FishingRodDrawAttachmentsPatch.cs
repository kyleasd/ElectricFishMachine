using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Tools;

namespace ElectricFishMachine;

/// <summary>
/// 电鱼机改为<strong>单挂件槽</strong>（仅 <c>attachments[0]</c>），避免原版按 <c>attachmentSlots()</c> 预留两行高度却在第二行作画而产生的空白。
/// 绘制方式对齐原版浮漂格（空 <c>37</c> / 有物 <c>10</c>）。
/// </summary>
[HarmonyPatch(typeof(FishingRod), nameof(FishingRod.drawAttachments))]
internal static class FishingRodDrawAttachmentsPatch
{
    private static bool Prefix(FishingRod __instance, SpriteBatch b, int x, int y)
    {
        if (!CustomToolData.IsElectricFishMachineRod(__instance))
            return true;

        y += __instance.enchantments.Count > 0 ? 8 : 4;

        Netcode.NetObjectArray<StardewValley.Object>? attachments = __instance.attachments;
        StardewValley.Object? cell = attachments is { Count: > 0 } ? attachments[0] : null;

        Vector2 pos = new(x, y);

        if (cell == null)
        {
            b.Draw(
                Game1.menuTexture,
                pos,
                Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 37),
                Color.White,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.86f);
        }
        else
        {
            b.Draw(
                Game1.menuTexture,
                pos,
                Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 10),
                Color.White,
                0f,
                Vector2.Zero,
                1f,
                SpriteEffects.None,
                0.86f);
            cell.drawInMenu(b, pos, 1f);
        }

        ElectricFishBatteryBarRenderer.DrawIfBatteryInSlotZero(__instance, b, x, y);

        return false;
    }
}
