using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace ElectricFishMachine;

/// <summary>向 Generic Mod Config Menu 注册本模组配置界面。</summary>
internal static class ModConfigMenuIntegration
{
    private const string GmcmUniqueId = "spacechase0.GenericModConfigMenu";

    internal static void Register(ModEntry mod)
    {
        IGenericModConfigMenuApi? api = mod.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(GmcmUniqueId);
        if (api == null)
        {
            mod.Monitor.Log(
                "未安装 Generic Mod Config Menu，可在 Mods/ElectricFishMachine/config.json 中编辑配置。",
                LogLevel.Info);
            return;
        }

        api.RegisterModConfig(
            mod.ModManifest,
            revertToDefault: () => mod.ResetConfigToDefault(),
            saveToFile: mod.SaveConfigToFile);

        api.SetDefaultIngameOptinValue(mod.ModManifest, optedIn: true);

        api.RegisterLabel(mod.ModManifest, "电鱼机", "调整电鱼机刷鱼行为。");

        api.RegisterSimpleOption(
            mod.ModManifest,
            "仅铱星品质",
            "开启后，电鱼机刷出的鱼类固定为铱星品质（不再按钓鱼等级随机银/金/铱）。",
            () => mod.Config.OnlyIridiumQuality,
            val => mod.Config.OnlyIridiumQuality = val);

        api.RegisterClampedOption(
            mod.ModManifest,
            "电鱼范围",
            "以人物为中心的水域扫描半径（格），3–10，默认 3。",
            () => mod.Config.ElectricFishRange,
            val => mod.Config.ElectricFishRange = val,
            min: 3,
            max: 10);

        api.RegisterSimpleOption(
            mod.ModManifest,
            "过滤垃圾",
            "开启后，电鱼不会刷出垃圾、浮木、破眼镜等（带 trash_item 标签的钓获）。",
            () => mod.Config.FilterTrash,
            val => mod.Config.FilterTrash = val);
    }
}
