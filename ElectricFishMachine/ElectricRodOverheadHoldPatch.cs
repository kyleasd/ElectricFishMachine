using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace ElectricFishMachine;

/// <summary>
/// 选中电鱼机时沿用原版举物逻辑：<see cref="Farmer.IsCarrying"/> 为真则走路/站立用举物动画；
/// 头顶贴图在原版 <see cref="Farmer.draw"/> 流程之外单独绘制（因没有 ActiveObject）。
/// </summary>
internal static class FarmerIsCarryingForElectricRodPatch
{
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.IsCarrying))]
    private static class IsCarryingPatch
    {
        private static bool Prefix(Farmer __instance, ref bool __result)
        {
            if (!CustomToolData.ShouldHoldElectricRodOverhead(__instance))
                return true;

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Farmer), nameof(Farmer.draw))]
    private static class DrawPostfixPatch
    {
        private static void Postfix(Farmer __instance, SpriteBatch b)
        {
            if (!CustomToolData.ShouldHoldElectricRodOverhead(__instance))
                return;

            if (FarmerDrawWouldSkip(__instance))
                return;

            if (Game1.eventUp
                && (Game1.currentLocation.currentEvent == null
                    || !Game1.currentLocation.currentEvent.showActiveObject))
                return;

            Tool? tool = __instance.CurrentTool;
            if (tool == null)
                return;

            int parentIndex = tool.CurrentParentTileIndex;
            Texture2D sheet = Game1.toolSpriteSheet;
            Rectangle sourceRect = new(
                parentIndex * 16 % sheet.Width,
                parentIndex * 16 / sheet.Width * 16,
                16,
                32);

            float x = __instance.getLocalPosition(Game1.viewport).X
                + ((__instance.rotation < 0f) ? -8f : ((__instance.rotation > 0f) ? 8f : 0f))
                + __instance.FarmerSprite.CurrentAnimationFrame.xOffset * 4f;

            float y = __instance.getLocalPosition(Game1.viewport).Y - 128f
                + __instance.FarmerSprite.CurrentAnimationFrame.positionOffset * 4f
                + FarmerRenderer.featureYOffsetPerFrame[__instance.FarmerSprite.CurrentFrame] * 4f;

            float layerDepth = Math.Max(0f, (__instance.StandingPixel.Y + 64) / 10000f);

            b.Draw(
                sheet,
                new Vector2((int)x, (int)y),
                sourceRect,
                Color.White,
                0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                layerDepth);
        }

        private static bool FarmerDrawWouldSkip(Farmer who)
        {
            if (who.currentLocation == null)
                return true;

            if (!who.currentLocation.Equals(Game1.currentLocation)
                && !who.IsLocalPlayer
                && !Game1.currentLocation.IsTemporary
                && !who.isFakeEventActor)
                return true;

            if (who.hidden.Value
                && (who.currentLocation.currentEvent == null || who != who.currentLocation.currentEvent.farmer)
                && (!who.IsLocalPlayer || Game1.locationRequest == null))
                return true;

            if (who.viewingLocation.Value != null && who.IsLocalPlayer)
                return true;

            return false;
        }
    }
}
