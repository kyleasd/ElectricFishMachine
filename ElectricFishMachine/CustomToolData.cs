using System.Reflection;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Tools;
using StardewValley.Tools;

namespace ElectricFishMachine;

/// <summary>
/// 注册自定义鱼竿条目：<see cref="StardewValley.Tools.FishingRod"/> + Harmony 限制挂件；数据由铱金鱼竿克隆。
/// </summary>
internal static class CustomToolData
{
    internal const string ToolId = "ElectricFishMachineRod";

    internal static bool IsElectricFishMachineRod(FishingRod? rod)
    {
        return rod != null
            && string.Equals(rod.QualifiedItemId, "(T)" + ToolId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 快捷栏当前选中电鱼机，且任一挂件槽已装入电池组时可触发「电鱼」逻辑。
    /// </summary>
    internal static bool PlayerCanElectricFish(Farmer who)
    {
        if (who.CurrentTool is not FishingRod rod)
            return false;

        if (!IsElectricFishMachineRod(rod))
            return false;

        return RodHasBatteryPack(rod);
    }

    /// <summary>
    /// 旧版曾把电池放在第二槽；读档后若仍存在则移到 <c>[0]</c>，与单槽数据一致。
    /// </summary>
    internal static void MigrateElectricRodBatteryFromLegacySecondSlot(FishingRod rod)
    {
        if (!IsElectricFishMachineRod(rod))
            return;

        NetObjectArray<StardewValley.Object>? att = rod.attachments;
        if (att == null || att.Count < 2)
            return;

        if (att[0] != null)
            return;

        StardewValley.Object? legacy = att[1];
        if (legacy == null || !FishingRodBatteryOnlyPatch.IsBatteryPack(legacy))
            return;

        att[0] = legacy;
        att[1] = null!;
    }

    /// <summary>
    /// 从挂件槽扣除 1 个电池组（优先序号小的槽）。用于在水边持续使用电鱼机时消耗电量。
    /// </summary>
    internal static bool TryConsumeOneBatteryCharge(FishingRod rod)
    {
        if (!IsElectricFishMachineRod(rod))
            return false;

        NetObjectArray<StardewValley.Object>? attachments = rod.attachments;
        if (attachments == null || attachments.Count == 0)
            return false;

        for (int i = 0; i < attachments.Count; i++)
        {
            StardewValley.Object? o = attachments[i];
            if (o == null || !FishingRodBatteryOnlyPatch.IsBatteryPack(o))
                continue;

            if (o.Stack <= 1)
                attachments[i] = null!;
            else
                o.Stack--;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 手持电鱼机且未真正举着 <see cref="Farmer.ActiveObject"/> 时，沿用原版「举过头顶」的姿势与绘制时机。
    /// </summary>
    internal static bool ShouldHoldElectricRodOverhead(Farmer who)
    {
        if (who.CurrentTool is not FishingRod rod || !IsElectricFishMachineRod(rod))
            return false;

        if (who.UsingTool)
            return false;

        if (who.ActiveObject != null)
            return false;

        if (who.FarmerSprite.PauseForSingleAnimation)
            return false;

        if (who.mount != null || who.isAnimatingMount)
            return false;

        if (who.IsSitting())
            return false;

        if (who.onBridge.Value)
            return false;

        if (Game1.eventUp || Game1.killScreen)
            return false;

        if (who.bathingClothes.Value)
            return false;

        return true;
    }

    internal static bool RodHasBatteryPack(FishingRod rod)
    {
        NetObjectArray<StardewValley.Object>? attachments = rod.attachments;
        if (attachments == null || attachments.Count == 0)
            return false;

        for (int i = 0; i < attachments.Count; i++)
        {
            StardewValley.Object? o = attachments[i];
            if (o != null && FishingRodBatteryOnlyPatch.IsBatteryPack(o))
                return true;
        }

        return false;
    }

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += (_, e) => OnAssetRequested(e);
    }

    private static void OnAssetRequested(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Strings/Tools"))
        {
            e.Edit(
                asset =>
                {
                    IDictionary<string, string> strings = asset.AsDictionary<string, string>().Data;
                    strings["ElectricFishMachineRod_Name"] = "电鱼机";
                    strings["ElectricFishMachineRod_Description"] = "据说把电池装进这个机器里面会有神奇的事情发生";
                },
                AssetEditPriority.Early
            );
            return;
        }

        if (!e.NameWithoutLocale.IsEquivalentTo("Data/Tools"))
            return;

        e.Edit(
            asset =>
            {
                IDictionary<string, ToolData> tools = asset.AsDictionary<string, ToolData>().Data;
                ToolData? iridium = FindIridiumRodTemplate(tools);
                if (iridium == null)
                    return;

                ToolData electric = ShallowCopyToolData(iridium);
                // 必须为游戏本体里的类型；模组程序集中的自定义 ClassName 无法被实例化 → 错误物品。
                electric.ClassName = nameof(StardewValley.Tools.FishingRod);
                electric.Name = ToolId;
                electric.DisplayName = "[LocalizedText Strings\\Tools:ElectricFishMachineRod_Name]";
                electric.Description = "[LocalizedText Strings\\Tools:ElectricFishMachineRod_Description]";

                // 原版铱金鱼竿的 Texture 常为 null（走内置默认）；自定义工具 ID 不会套用该默认，必须写明朝图资源名。
                electric.Texture = string.IsNullOrWhiteSpace(iridium.Texture)
                    ? "TileSheets\\Tools"
                    : iridium.Texture.Trim();
                electric.SpriteIndex = iridium.SpriteIndex;
                electric.MenuSpriteIndex = iridium.MenuSpriteIndex;

                // 单槽即可（电池在 attachments[0]）。双槽会让 tooltip 按两行预留高度却只画第二行，中间出现大块空白。
                electric.AttachmentSlots = 1;

                tools[ToolId] = electric;
            },
            AssetEditPriority.Late
        );
    }

    private static ToolData ShallowCopyToolData(ToolData source)
    {
        ToolData copy = new();
        foreach (PropertyInfo prop in typeof(ToolData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length != 0)
                continue;
            if (!prop.CanRead || !prop.CanWrite)
                continue;

            prop.SetValue(copy, prop.GetValue(source));
        }

        return copy;
    }

    private static ToolData? FindIridiumRodTemplate(IDictionary<string, ToolData> tools)
    {
        foreach ((string key, ToolData data) in tools)
        {
            if (key.Equals("IridiumRod", StringComparison.OrdinalIgnoreCase)
                || key.Equals("(T)IridiumRod", StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }
        }

        foreach ((_, ToolData data) in tools)
        {
            if (data is { Name: string name }
                && name.Equals("IridiumRod", StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }
        }

        return null;
    }
}
