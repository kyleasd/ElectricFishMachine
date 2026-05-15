using Microsoft.Xna.Framework;

namespace ElectricFishMachine;

/// <summary>模组注入资源路径（UniqueID 须与 manifest.json 一致）。</summary>
internal static class ElectricFishMachineContent
{
    internal const string UniqueId = "Kyle.ElectricFishMachine";

    /// <summary>SMAPI 注入的电鱼机贴图资产名，对应 assets/ElectricFishMachine.png。</summary>
    internal const string ElectricFishMachineTexture = "Mods/" + UniqueId + "/ElectricFishMachine";

    /// <summary>单帧像素宽高；贴图为横向条带：<c>宽 = SpriteFrameWidth * SpriteFrameCount</c>，高 <see cref="SpriteFrameHeight"/>。</summary>
    internal const int SpriteFrameWidth = 16;

    internal const int SpriteFrameHeight = 16;

    internal const int SpriteFrameCount = 5;

    /// <summary>电鱼进行中时头顶循环的条带列下标（0-based）：<b>1～4</b>，对应贴图里第 2～5 格。</summary>
    internal const int OverheadElectricFishAnimStartIndex = 1;

    internal const int OverheadElectricFishAnimEndIndexInclusive = 4;

    /// <summary>头顶电鱼动画每帧持续毫秒数。</summary>
    internal const int OverheadElectricFishAnimFrameDurationMs = 150;

    /// <summary>道具栏/背包与举过头顶共用：16×16 源图相对原版 scaleSize 的乘数；头顶再按游戏的 pixelZoom 相对基准 4 换算。</summary>
    internal const float UiSpriteScaleMultiplier = 4f;

    internal static Rectangle GetSpriteSourceRect(int frameIndex)
    {
        int i = Math.Clamp(frameIndex, 0, SpriteFrameCount - 1);
        return new Rectangle(i * SpriteFrameWidth, 0, SpriteFrameWidth, SpriteFrameHeight);
    }
}
