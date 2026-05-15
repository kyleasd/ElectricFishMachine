using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

    /// <summary>载入 PNG 后的缩放系数；贴图为原生 16×16 格条带时使用 1 保持像素清晰。</summary>
    internal const float ElectricFishTextureLoadScale = 1f;

    private static IModHelper? _modHelper;

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
    /// 与 <c>ModEntry</c> 电鱼刷鱼/扣电条件一致：具备电鱼资格且当前位置邻接可钓鱼水域。
    /// </summary>
    internal static bool IsPlayerActivelyElectricFishing(Farmer who)
    {
        if (!PlayerCanElectricFish(who))
            return false;

        if (who.currentLocation == null)
            return false;

        int tileX = (int)(who.Position.X / 64);
        int tileY = (int)(who.Position.Y / 64);
        GameLocation loc = who.currentLocation;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (loc.isWaterTile(tileX + dx, tileY + dy))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 举过头顶贴图用条带帧：未在电鱼时用 0；电鱼时在 <see cref="ElectricFishMachineContent.OverheadElectricFishAnimStartIndex"/>～<see cref="ElectricFishMachineContent.OverheadElectricFishAnimEndIndexInclusive"/> 间循环。
    /// </summary>
    internal static int GetOverheadElectricFishSpriteFrameIndex(Farmer who)
    {
        if (!IsPlayerActivelyElectricFishing(who))
            return 0;

        int start = ElectricFishMachineContent.OverheadElectricFishAnimStartIndex;
        int end = ElectricFishMachineContent.OverheadElectricFishAnimEndIndexInclusive;
        int len = end - start + 1;

        int ms = Game1.currentGameTime != null
            ? (int)Game1.currentGameTime.TotalGameTime.TotalMilliseconds
            : 0;

        int offset = (ms / ElectricFishMachineContent.OverheadElectricFishAnimFrameDurationMs) % len;
        if (offset < 0)
            offset = 0;

        return start + offset;
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

        // 不按 UsingTool 拦截：电鱼机禁用抛竿后原版仍可能把 UsingTool 置为 true，会误判导致永远不举过头顶。

        if (who.ActiveObject != null)
            return false;

        // 不按 PauseForSingleAnimation 拦截：原版在移动/动画衔接里常会单帧为 true；
        // 若此处 return false，IsCarrying 补丁不生效 → 手会垂下一帧再恢复举物姿势。
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
        _modHelper = helper;
        helper.Events.Content.AssetRequested += (_, e) => OnAssetRequested(e);
    }

    private static void OnAssetRequested(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(ElectricFishMachineContent.ElectricFishMachineTexture))
        {
            e.LoadFrom(LoadElectricFishMachineTexture, AssetLoadPriority.Exclusive);
            return;
        }

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

                // 使用模组内 assets/ElectricFishMachine.png（经 ElectricFishMachineContent 注入），不再沿用铱金鱼竿图块。
                electric.Texture = ElectricFishMachineContent.ElectricFishMachineTexture;
                electric.SpriteIndex = 0;
                electric.MenuSpriteIndex = 0;

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

    private static Texture2D LoadElectricFishMachineTexture()
    {
        if (_modHelper == null)
            throw new InvalidOperationException("ElectricFishMachine: Register must run before texture load.");

        string path = Path.Combine(_modHelper.DirectoryPath, "assets", "ElectricFishMachine.png");
        using FileStream stream = File.OpenRead(path);
        GraphicsDevice graphicsDevice = Game1.graphics.GraphicsDevice;
        Texture2D raw = Texture2D.FromStream(graphicsDevice, stream);

        float s = ElectricFishTextureLoadScale;
        if (s >= 0.999f || raw.Width <= 1 || raw.Height <= 1)
            return raw;

        int nw = Math.Max(1, (int)Math.Round(raw.Width * s));
        int nh = Math.Max(1, (int)Math.Round(raw.Height * s));
        if (nw == raw.Width && nh == raw.Height)
            return raw;

        Color[] src = new Color[raw.Width * raw.Height];
        raw.GetData(src);
        Color[] dst = new Color[nw * nh];
        for (int ty = 0; ty < nh; ty++)
        {
            int sy = Math.Min(raw.Height - 1, (int)((ty + 0.5f) / nh * raw.Height));
            int row = sy * raw.Width;
            for (int tx = 0; tx < nw; tx++)
            {
                int sx = Math.Min(raw.Width - 1, (int)((tx + 0.5f) / nw * raw.Width));
                dst[ty * nw + tx] = src[row + sx];
            }
        }

        var scaled = new Texture2D(graphicsDevice, nw, nh, false, SurfaceFormat.Color);
        scaled.SetData(dst);
        raw.Dispose();
        return scaled;
    }
}
