using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using ArabellaMod.Mechanics;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace ArabellaMod;

[ModInitializer(nameof(Initialize))]
public partial class Entry
{
    // ModId 必须与 ArabellaMod.json 中的 id 保持一致。
    // ResPath 是 PCK 内资源根目录，后续资源路径统一从这里拼接。
    public const string ModId = "ArabellaMod";
    public const string ResPath = $"res://{ModId}";

    public static Logger Logger { get; } = new(ModId, LogType.Generic);

    public static void Initialize()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        // 动画触发依赖 Harmony 补丁。唯一 ID 可防止与其他模组的补丁实例冲突。
        Harmony harmony = new("ArabellaMod.AnimationAndEffects");
        harmony.PatchAll(assembly);

        // Godot 脚本注册与 RitsuLib 内容扫描是两套机制，两项都必须保留。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 与蕾欧娜、卡莉佩一致：主菜单和暂停菜单均可调整背身动画开关与大小。
        Settings.ArabellaSettingsPage.Register();

        // 欲火是艾拉贝拉的次级战斗资源；在任何卡牌或单例读取它之前完成注册。
        ArabellaResources.Register();

        // 在玩家进入战斗或商店前载入人物帧，避免第一次播放动作时产生明显卡顿。
        Animation.ArabellaAnimationAssets.PreloadAllCharacterFrames();
        Animation.ArabellaBackPoseAssets.Preload();

        // 全局服务即使暂时没有音频/特效素材也先完成初始化，后续卡牌可直接调用统一接口。
        ArabellaAudio.Initialize();
        ArabellaVfx.Initialize();

        Logger.Info("ArabellaMod initialized.");
    }
}
