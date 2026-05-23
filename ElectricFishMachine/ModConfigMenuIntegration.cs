using GenericModConfigMenu;
using StardewModdingAPI;

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

        api.Register(
            mod.ModManifest,
            mod.ResetConfigToDefault,
            mod.SaveConfigToFile,
            titleScreenOnly: false);

        api.AddSectionTitle(
            mod.ModManifest,
            () => "电鱼机",
            () => "调整电鱼机刷鱼行为。");

        api.AddBoolOption(
            mod.ModManifest,
            () => mod.Config.OnlyIridiumQuality,
            val => mod.Config.OnlyIridiumQuality = val,
            () => "仅铱星品质",
            () => "开启后，电鱼机刷出的鱼类固定为铱星品质（不再按钓鱼等级随机银/金/铱）。");

        api.AddNumberOption(
            mod.ModManifest,
            () => mod.Config.ElectricFishRange,
            val => mod.Config.ElectricFishRange = val,
            () => "电鱼范围",
            () => "以人物为中心的水域扫描半径（格），3–10，默认 3。",
            min: 3,
            max: 10);

        api.AddBoolOption(
            mod.ModManifest,
            () => mod.Config.FilterTrash,
            val => mod.Config.FilterTrash = val,
            () => "过滤垃圾",
            () => "开启后，电鱼不会刷出垃圾、浮木、破眼镜等（带 trash_item 标签的钓获）。");
    }
}
