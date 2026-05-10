using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley;
using StardewValley.Tools;

namespace ElectricFishMachine;

/// <summary>
/// 在 <see cref="FishingRod.drawAttachments"/> 流程里、单槽电池图标下绘制耐久条。
/// </summary>
internal static class ElectricFishBatteryBarRenderer
{
    internal static void DrawIfBatteryInSlotZero(FishingRod rod, SpriteBatch b, int iconLeft, int iconTop)
    {
        if (!CustomToolData.IsElectricFishMachineRod(rod))
            return;

        NetObjectArray<StardewValley.Object>? attachments = rod.attachments;
        if (attachments == null || attachments.Count == 0)
            return;

        StardewValley.Object? obj = attachments[0];
        if (obj == null || !FishingRodBatteryOnlyPatch.IsBatteryPack(obj))
            return;

        DrawTackleStyleBar(b, iconLeft, iconTop, ElectricFishBatteryHud.GetBarFillRatio(rod));
    }

    private static void DrawTackleStyleBar(SpriteBatch b, int iconLeft, int iconTop, float fillRatio)
    {
        Texture2D pixel = Game1.fadeToBlackRect;
        const int barW = 56;
        const int barH = 4;
        int bx = iconLeft + 4;
        int by = iconTop + 56;

        Rectangle background = new(bx, by, barW, barH);
        b.Draw(pixel, background, Color.Black * 0.42f);

        int filledW = Math.Max(0, (int)Math.Floor(barW * fillRatio + 0.0001f));
        if (filledW > 0)
        {
            Color fillColor = fillRatio > 0.25f
                ? new Color(60, 220, 90)
                : new Color(220, 180, 60);

            Rectangle foreground = new(bx, by, filledW, barH);
            b.Draw(pixel, foreground, fillColor);
        }
    }
}
