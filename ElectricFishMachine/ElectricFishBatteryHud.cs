using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace ElectricFishMachine;

/// <summary>
/// 工具栏电池「耐久条」：显示当前放电周期剩余（CooldownRemaining / Interval）。
/// 离开水边时倒计时暂停且<strong>不重置</strong>，条保持在上次的比例；只有卸下电池/换工具时才清零倒计时。
/// </summary>
internal static class ElectricFishBatteryHud
{
    internal static int CooldownRemaining;

    internal static int IntervalFrames = 180;

    internal static float GetBarFillRatio(FishingRod? rod = null)
    {
        if (IntervalFrames <= 0)
            return 1f;

        if (!Context.IsWorldReady)
            return 1f;

        FishingRod? r = rod ?? (Game1.player.CurrentTool as FishingRod);
        if (r == null || !CustomToolData.IsElectricFishMachineRod(r))
            return 1f;

        if (!CustomToolData.RodHasBatteryPack(r))
            return 1f;

        return Math.Clamp(CooldownRemaining / (float)IntervalFrames, 0f, 1f);
    }
}
