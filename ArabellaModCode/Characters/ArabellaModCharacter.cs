using Godot;
using ArabellaMod.Animation;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;

namespace ArabellaMod.Characters;

[RegisterCharacter]
public sealed class
    ArabellaModCharacter : ModCharacterTemplate<ArabellaModCardPool, ArabellaModRelicPool, ArabellaModPotionPool>
{
    public static readonly Color ThemeColor = new(0.42f, 0.65f, 0.72f);

    private const string SceneRoot = $"{Entry.ResPath}/scenes/characters";
    private const string ImageRoot = $"{Entry.ResPath}/images/characters";
    private const string CharacterScenePath = $"{SceneRoot}/ArabellaMod_character.tscn";
    private const string EnergyCounterScenePath = $"{SceneRoot}/ArabellaMod_energy_counter.tscn";
    private const string MerchantScenePath = $"{SceneRoot}/ArabellaMod_merchant.tscn";
    private const string RestSiteScenePath = $"{SceneRoot}/ArabellaMod_rest_site.tscn";
    private const string CharacterSelectBgScenePath = $"{SceneRoot}/ArabellaMod_character_select_bg.tscn";

    // 角色名称颜色。
    public override Color NameColor => ThemeColor;

    // 能量图标轮廓颜色。
    public override Color EnergyLabelOutlineColor => new(0.20f, 0.01f, 0.04f);

    // 地图绘制颜色。
    public override Color MapDrawingColor => ThemeColor;

    // 人物性别（男女中立）。
    public override CharacterGender Gender => CharacterGender.Neutral;

    // 初始血量和金币。
    public override int StartingHp => 75;
    public override int StartingGold => 99;

    // CharacterAssetProfile 按类别拆分。你只写需要替换的部分，其他字段会保留回退。
    // AssetProfile 只指定模板自带的静态占位资源；没有复制的音频、拖尾、转场等资源继续从占位角色回退。
    public override CharacterAssetProfile AssetProfile => new(
        Scenes: new CharacterSceneAssetSet(
            // 人物模型 tscn 路径。
            VisualsPath: CharacterScenePath,
            // 能量表盘 tscn 路径。
            EnergyCounterPath: EnergyCounterScenePath,
            // 商店人物场景。
            MerchantAnimPath: MerchantScenePath,
            // 篝火休息场景。
            RestSiteAnimPath: RestSiteScenePath),
        Ui: new CharacterUiAssetSet(
            // 人物头像路径。
            IconTexturePath: $"{ImageRoot}/ArabellaMod_character_icon.png",
            // 人物头像轮廓。
            IconOutlineTexturePath: $"{ImageRoot}/ArabellaMod_character_icon_outline.png",
            IconPath: $"{SceneRoot}/ArabellaMod_character_icon.tscn",
            // 人物选择背景。
            CharacterSelectBgPath: CharacterSelectBgScenePath,
            // 人物选择图标。
            CharacterSelectIconPath: $"{ImageRoot}/ArabellaMod_character_select.png",
            // 人物选择图标-锁定状态。
            CharacterSelectLockedIconPath: $"{ImageRoot}/ArabellaMod_character_select.png",
            // 地图上的角色标记图标、表情轮盘上的角色头像。
            MapMarkerPath: $"{ImageRoot}/ArabellaMod_map_marker.png"),
        Vfx: new CharacterVfxAssetSet(
            // 保留原生拖尾结构，仅将丝带、火花和粒子统一为暗红色。
            TrailStyle: new CharacterTrailStyle(
                OuterTrailModulate: new Color(0.24f, 0.01f, 0.04f, 0.95f),
                InnerTrailModulate: new Color(0.64f, 0.04f, 0.10f, 0.92f),
                BigSparksColor: new Color(0.78f, 0.08f, 0.14f, 0.88f),
                LittleSparksColor: new Color(0.46f, 0.02f, 0.07f, 0.80f),
                PrimarySpriteModulate: new Color(0.58f, 0.03f, 0.09f, 0.92f),
                SecondarySpriteModulate: new Color(0.28f, 0.01f, 0.04f, 0.88f))));

    // 某个字段没写时，RitsuLib 会从占位角色配置里补齐。
    public override string? PlaceholderCharacterId => "ironclad";

    // 如果你的人物不需要时间线小故事，加上这句。
    public override bool RequiresEpochAndTimeline => false;

    // 正常情况下由动画控制器的真实命中事件直接放行伤害。
    // 这个帧数换算值仅作为控制器不存在时的原版回退延迟。
    public override float AttackAnimDelay => ArabellaAnimationAssets.AttackDamageDelaySeconds;
    public override float CastAnimDelay => 1.2f;

    // 让 RitsuLib 把普通 Godot 场景转换成游戏需要的 NCreatureVisuals。
    // 自动转换人物场景，让你不需要手动挂脚本。复制即可。
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        return RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(
            CharacterScenePath);
    }

    // 攻击建筑师的攻击特效列表。
    public override List<string> GetArchitectAttackVfx()
    {
        return
        [
            "vfx/vfx_attack_blunt",
            "vfx/vfx_heavy_blunt",
            "vfx/vfx_attack_slash",
            "vfx/vfx_bloody_impact",
            "vfx/vfx_rock_shatter"
        ];
    }
}
