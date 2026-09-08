using Godot;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace ArabellaMod.Characters;

public sealed class ArabellaModCardPool : TypeListCardPoolModel
{
    private static readonly Material? PoolFrameTintMaterial =
        // 采用蕾欧娜当前工程使用的新接口；最后一个参数是颜色强度/透明度。
        MaterialUtils.CreateReplaceHueShaderMaterial(0.42f, 0.65f, 0.72f, 1.0f);

    // Title 和 EnergyColorName 是池子的稳定标识，不是玩家看到的角色名。
    // 自定义角色卡、遗物、药水池保持同一个 EnergyColorName，方便实验室和文本统一读取能量图标。
    public override string Title => "ArabellaMod";
    public override string EnergyColorName => "ArabellaMod";

    // 这里指定卡牌文本和大图使用的能量图标路径。
    // res://ArabellaMod/... 里的 ArabellaMod 是 PCK 资源目录，不是 C# namespace。
    public override string? BigEnergyIconPath =>
        $"{Entry.ResPath}/images/characters/ArabellaMod_energy_icon_card.png";
    public override string? TextEnergyIconPath => $"{Entry.ResPath}/images/characters/energy_text.png";

    public override Color DeckEntryCardColor => ArabellaModCharacter.ThemeColor;
    public override Color EnergyOutlineColor => new(0.20f, 0.01f, 0.04f);
    public override Material? PoolFrameMaterial => PoolFrameTintMaterial;

    // false 表示这是角色专属卡池，不是事件/状态那类无色卡池。
    public override bool IsColorless => false;
}
