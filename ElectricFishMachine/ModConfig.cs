namespace ElectricFishMachine;

/// <summary>SMAPI 从模组目录下的 <c>config.json</c> 读取；缺省时使用此处默认值。</summary>
public class ModConfig
{
    /// <summary>
    /// 为 <c>true</c> 时，电鱼机刷出的「鱼」类物品固定为铱星品质（<c>Quality = 4</c>），不再按钓鱼等级随机银/金/铱。
    /// </summary>
    public bool OnlyIridiumQuality { get; set; } = false;

    /// <summary>
    /// 电鱼刷鱼半径（格），以人物站立点为中心四向延伸，有效区间 [3, 10]，默认 3。
    /// </summary>
    public int ElectricFishRange { get; set; } = 3;

    /// <summary>将 <see cref="ElectricFishRange"/> 限制在 [3, 10]。</summary>
    public int ClampedElectricFishRange => Math.Clamp(ElectricFishRange, 3, 10);

    /// <summary>
    /// 为 <c>true</c> 时跳过钓鱼垃圾（<c>trash_item</c> 标签：垃圾、浮木、破眼镜等），重新掷骰直至非垃圾或达到重试上限。
    /// </summary>
    public bool FilterTrash { get; set; } = false;
}
