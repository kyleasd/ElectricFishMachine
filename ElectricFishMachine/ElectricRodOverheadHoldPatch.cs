using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Tools;

namespace ElectricFishMachine;

/// <summary>
/// 选中电鱼机时沿用原版举物逻辑：<see cref="Farmer.IsCarrying"/> 为真则走路/站立用举物动画；
/// 头顶贴图在原版 <see cref="Farmer.draw"/> 流程之外单独绘制（因没有 ActiveObject）。
/// </summary>
internal static class FarmerIsCarryingForElectricRodPatch
{
    /// <summary>在停下、未暂停单帧动画且非抛竿状态时，把姿势拉回举物待机（供 Update 末尾与 Draw 前调用）。</summary>
    private static void SnapElectricRodCarryingIdleIfNeeded(Farmer who)
    {
        if (!CustomToolData.ShouldHoldElectricRodOverhead(who))
            return;

        if (who.FarmerSprite.PauseForSingleAnimation)
            return;

        FishingRod? rodElectric = who.CurrentTool as FishingRod;
        bool electricRod = CustomToolData.IsElectricFishMachineRod(rodElectric);

        // 电鱼机不走抛竿动画，但原版仍可能把 UsingTool 置 true 一帧；若此处因 UsingTool 跳过则永远拉不回举物待机。
        if (!electricRod && who.UsingTool)
            return;

        if (who.isMoving())
            return;

        if (who.isRidingHorse() || who.IsSitting())
            return;

        if (electricRod && (rodElectric!.isTimingCast || rodElectric.isCasting))
            return;

        who.showCarrying();
    }

    /// <summary>原版多处会调 <see cref="Farmer.showNotCarrying"/> 把手垂下；举电鱼机时直接跳过。</summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.showNotCarrying))]
    [HarmonyPriority(Priority.First)]
    private static class ShowNotCarryingPrefixPatch
    {
        private static bool Prefix(Farmer __instance)
        {
            if (CustomToolData.ShouldHoldElectricRodOverhead(__instance))
                return false;

            return true;
        }
    }

    /// <summary>在绘制人物前再对齐一次举物待机，避免 Update 之后、Draw 之前其它逻辑改回非举物帧。</summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.draw))]
    [HarmonyPriority(Priority.First)]
    private static class DrawPrefixSnapCarryingPatch
    {
        private static void Prefix(Farmer __instance)
        {
            SnapElectricRodCarryingIdleIfNeeded(__instance);
        }
    }

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

    /// <summary>
    /// 其它补丁或原版实现若仍把 <see cref="Farmer.IsCarrying"/> 判成 false，此处再强制为 true，避免停下时一帧落回「未举物」。
    /// </summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.IsCarrying))]
    private static class IsCarryingPostfixPatch
    {
        private static void Postfix(Farmer __instance, ref bool __result)
        {
            if (CustomToolData.ShouldHoldElectricRodOverhead(__instance))
                __result = true;
        }
    }

    /// <summary>
    /// 原版 <see cref="Farmer.updateMovementAnimation"/> 在「刚停下」的一帧里仍可能走到 <see cref="Farmer.showNotCarrying"/>（手垂下），
    /// 在本方法整段逻辑结束后再 <see cref="Farmer.showCarrying"/> 拉回举物待机。
    /// </summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.updateMovementAnimation))]
    private static class UpdateMovementAnimationElectricRodPostfixPatch
    {
        private static void Postfix(Farmer __instance)
        {
            SnapElectricRodCarryingIdleIfNeeded(__instance);
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
            if (tool is not FishingRod rod || !CustomToolData.IsElectricFishMachineRod(rod))
                return;

            Texture2D sheet = Game1.content.Load<Texture2D>(ElectricFishMachineContent.ElectricFishMachineTexture);
            int overheadFrame = CustomToolData.GetOverheadElectricFishSpriteFrameIndex(__instance);
            Rectangle sourceRect = ElectricFishMachineContent.GetSpriteSourceRect(overheadFrame);
            // 与物品栏一致：UiSpriteScaleMultiplier，并按 pixelZoom 相对 UI 基准 4 换算；单帧用 sourceRect 尺寸参与封顶，避免整张条带宽度误判导致过小。
            const float targetW = 40f * 2f;
            const float targetH = 80f * 2f;
            float maxScale = Math.Min(targetW / sourceRect.Width, targetH / sourceRect.Height);
            float drawScalar = ElectricFishMachineContent.UiSpriteScaleMultiplier * (Game1.pixelZoom / 4f);
            drawScalar = Math.Min(drawScalar, maxScale);
            Vector2 drawScale = new Vector2(drawScalar, drawScalar);
            float drawnW = sourceRect.Width * drawScalar;
            float drawnH = sourceRect.Height * drawScalar;
            // 与原版举鱼竿占位一致：宽 16×缩放、高 32×缩放（通常 64×128），贴图在该矩形内居中；再略上移避免贴头发
            float slotW = 16f * Game1.pixelZoom;
            float slotH = 32f * Game1.pixelZoom;
            const float overheadExtraLiftPx = 16f;

            float xBase = __instance.getLocalPosition(Game1.viewport).X
                + ((__instance.rotation < 0f) ? -8f : ((__instance.rotation > 0f) ? 8f : 0f))
                + __instance.FarmerSprite.CurrentAnimationFrame.xOffset * 4f;
            float x = xBase + (slotW - drawnW) * 0.5f;

            float yBase = __instance.getLocalPosition(Game1.viewport).Y - slotH
                + __instance.FarmerSprite.CurrentAnimationFrame.positionOffset * 4f
                + FarmerRenderer.featureYOffsetPerFrame[__instance.FarmerSprite.CurrentFrame] * 4f;
            float y = yBase + (slotH - drawnH) * 0.25f - overheadExtraLiftPx;

            float layerDepth = Math.Max(0f, (__instance.StandingPixel.Y + 64) / 10000f);

            b.Draw(
                sheet,
                new Vector2((int)x, (int)y),
                sourceRect,
                Color.White,
                0f,
                Vector2.Zero,
                drawScale,
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
