using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Tools;

namespace ElectricFishMachine;

/// <summary>
/// 原版菜单绘制按「标准格」放大工具图标；电鱼机整张贴图会显得过大。
/// 必须打在 <see cref="Tool"/> 的<strong>具体</strong> <c>drawInMenu</c> 上：打在抽象 <see cref="Item.drawInMenu"/> 会导致 Harmony IL 报错。
/// </summary>
[HarmonyPatch]
internal static class ElectricFishMachineMenuDrawPatch
{
    /// <summary>与 <see cref="StardewValley.Object.drawInMenu"/> 开头一致，配方在菜单里会略透明、略放大。</summary>
    private static readonly MethodInfo? AdjustMenuDrawForRecipes = ResolveAdjustMenuDrawForRecipes();

    private static MethodInfo? ResolveAdjustMenuDrawForRecipes()
    {
        Type[] sig = { typeof(float).MakeByRefType(), typeof(float).MakeByRefType() };
        MethodInfo? fromItem = AccessTools.Method(typeof(Item), "AdjustMenuDrawForRecipes", sig);
        if (fromItem != null)
            return fromItem;

        return AccessTools.Method(typeof(StardewValley.Object), "AdjustMenuDrawForRecipes", sig);
    }

    private static readonly Type[] MenuDrawArgTypes =
    {
        typeof(SpriteBatch),
        typeof(Vector2),
        typeof(float),
        typeof(float),
        typeof(float),
        typeof(StackDrawType),
        typeof(Color),
        typeof(bool)
    };

    /// <summary>相对原版传入的 <c>scaleSize</c> 的乘数（与举过头顶共用 <see cref="ElectricFishMachineContent.UiSpriteScaleMultiplier"/>）。</summary>
    private static float MenuScaleCorrection => ElectricFishMachineContent.UiSpriteScaleMultiplier;

    private static MethodBase TargetMethod()
    {
        // 若 FishingRod 单独 override，必须打在该类型上；否则打在 Tool 的具体实现上。
        // 不能打在抽象 Item.drawInMenu 上，否则 Harmony 生成替换方法时会报 IL label 错误。
        MethodInfo? fromRod = AccessTools.DeclaredMethod(typeof(FishingRod), nameof(FishingRod.drawInMenu), MenuDrawArgTypes);
        if (fromRod != null && !fromRod.IsAbstract)
            return fromRod;

        MethodInfo? fromTool = AccessTools.DeclaredMethod(typeof(Tool), nameof(Tool.drawInMenu), MenuDrawArgTypes);
        if (fromTool != null && !fromTool.IsAbstract)
            return fromTool;

        MethodInfo? chained = AccessTools.Method(typeof(FishingRod), nameof(FishingRod.drawInMenu), MenuDrawArgTypes);
        if (chained != null && !chained.IsAbstract)
            return chained;

        throw new InvalidOperationException(
            "ElectricFishMachine: 找不到可修补的 Tool/FishingRod.drawInMenu(SpriteBatch, Vector2, float×3, StackDrawType, Color, bool) 具体实现。");
    }

    private static bool Prefix(
        Tool __instance,
        SpriteBatch spriteBatch,
        Vector2 location,
        float scaleSize,
        float transparency,
        float layerDepth,
        StackDrawType drawStackNumber,
        Color color,
        bool drawShadow)
    {
        if (__instance is not FishingRod rod || !CustomToolData.IsElectricFishMachineRod(rod))
            return true;

        bool recipe = __instance.IsRecipe;
        float t = transparency;
        float s = scaleSize;
        if (recipe)
            ApplyVanillaRecipeMenuDrawAdjust(__instance, ref t, ref s);

        string textureKey = recipe
            ? ElectricFishMachineContent.ElectricFishMachineRecipeTexture
            : ElectricFishMachineContent.ElectricFishMachineTexture;
        Texture2D tex = Game1.content.Load<Texture2D>(textureKey);
        Rectangle source = recipe
            ? new Rectangle(0, 0, tex.Width, tex.Height)
            : ElectricFishMachineContent.GetSpriteSourceRect(0);
        float drawScale = s * MenuScaleCorrection;
        float drawnW = source.Width * drawScale;
        float drawnH = source.Height * drawScale;
        // 与原版物品格一致：一格边长 16×UI 缩放（道具栏/背包单格约 64px）
        float slotSide = 16f * Game1.pixelZoom;
        Vector2 drawPos = location + new Vector2((slotSide - drawnW) * 0.5f, (slotSide - drawnH) * 0.5f);

        if (drawShadow)
        {
            spriteBatch.Draw(
                tex,
                drawPos + new Vector2(4f, 4f),
                source,
                Color.Black * (0.35f * t),
                0f,
                Vector2.Zero,
                drawScale,
                SpriteEffects.None,
                layerDepth - 1E-06f);
        }

        spriteBatch.Draw(
            tex,
            drawPos,
            source,
            color * t,
            0f,
            Vector2.Zero,
            drawScale,
            SpriteEffects.None,
            layerDepth);

        // 与 Data/Objects 配方条目一致：叠原版蓝图角标/动效（威利商店、背包里未学会的配方等）。
        if (recipe)
            __instance.DrawMenuIcons(spriteBatch, location, s, t, layerDepth, drawStackNumber, color);

        return false;
    }

    private static void ApplyVanillaRecipeMenuDrawAdjust(Item item, ref float transparency, ref float scaleSize)
    {
        if (AdjustMenuDrawForRecipes != null)
        {
            object[] args = { transparency, scaleSize };
            AdjustMenuDrawForRecipes.Invoke(item, args);
            transparency = (float)args[0];
            scaleSize = (float)args[1];
            return;
        }

        // 若未来版本改名/签名变化：接近原版「未学配方」略透明观感。
        transparency *= 0.55f;
    }
}
