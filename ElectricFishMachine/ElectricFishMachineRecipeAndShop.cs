using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData.Shops;

namespace ElectricFishMachine;

/// <summary>
/// 合成配方（硬木×99、铜锭×99、铁锭×99、金锭×99、五彩碎片×1）与威利鱼店以 66666g 购买配方图纸。
/// </summary>
internal static class ElectricFishMachineRecipeAndShop
{
    private const string WillyShopRecipeSaleId = "Kyle.ElectricFishMachine.WillyRecipe";

    internal static void Register(IModHelper helper)
    {
        helper.Events.Content.AssetRequested += (_, e) => OnAssetRequested(e);
    }

    private static void OnAssetRequested(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
        {
            e.Edit(
                asset =>
                {
                    IDictionary<string, string> recipes = asset.AsDictionary<string, string>().Data;
                    if (recipes.ContainsKey(CustomToolData.ToolId))
                        return;

                    // 709 硬木, 334 铜锭, 335 铁锭, 336 金锭, 74 五彩碎片；产出为自定义鱼竿工具（顺序即制作界面显示顺序）
                    recipes[CustomToolData.ToolId] =
                        "709 99 334 99 335 99 336 99 74 1/Home/(T)"
                        + CustomToolData.ToolId
                        + "/false/null/[LocalizedText Strings\\Tools:ElectricFishMachineRod_Name]";
                },
                AssetEditPriority.Early
            );
            return;
        }

        if (!e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            return;

        e.Edit(
            asset =>
            {
                IDictionary<string, ShopData> shops = asset.AsDictionary<string, ShopData>().Data;
                if (!shops.TryGetValue("FishShop", out ShopData? fishShop) || fishShop.Items is null)
                    return;

                if (fishShop.Items.Any(i => i.Id == WillyShopRecipeSaleId))
                    return;

                fishShop.Items.Add(
                    new ShopItemData
                    {
                        Id = WillyShopRecipeSaleId,
                        ItemId = "(T)" + CustomToolData.ToolId,
                        IsRecipe = true,
                        Price = 66666,
                        ApplyProfitMargins = false
                    }
                );
            },
            AssetEditPriority.Early
        );
    }
}
