using Godot;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;
using ArabellaMod.Animation;

namespace ArabellaMod.Settings;

public sealed class ArabellaSettings
{
    public bool IsBackPoseEnabled { get; set; } = true;
    public int BackPoseScalePercent { get; set; } = 150;
    public bool AreSequenceFrameEffectsEnabled { get; set; } = true;
    public bool IsCharacterVoiceEnabled { get; set; } = true;
    public int CharacterVoiceVolumePercent { get; set; } = 100;
    public bool IsSoundEffectsEnabled { get; set; } = true;
    public int SoundEffectsVolumePercent { get; set; } = 100;
}

public static class ArabellaSettingsPage
{
    private const string DataKey = "arabella_settings";

    public static readonly ModSettingsValueBinding<ArabellaSettings, bool> BackPoseEnabledBinding = new(
        Entry.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.IsBackPoseEnabled,
        static (settings, enabled) =>
        {
            settings.IsBackPoseEnabled = enabled;
            ArabellaBackPoseVfx.ApplyEnabled(enabled);
        });

    public static readonly ModSettingsValueBinding<ArabellaSettings, int> BackPoseScaleBinding = new(
        Entry.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.BackPoseScalePercent,
        static (settings, percent) =>
        {
            settings.BackPoseScalePercent = Mathf.Clamp(percent, 80, 150);
            ArabellaBackPoseVfx.ApplyScalePercent(settings.BackPoseScalePercent);
        });

    public static readonly ModSettingsValueBinding<ArabellaSettings, bool> SequenceFrameEffectsEnabledBinding = new(
        Entry.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.AreSequenceFrameEffectsEnabled,
        static (settings, enabled) =>
        {
            settings.AreSequenceFrameEffectsEnabled = enabled;
            ArabellaVfx.ApplySequenceFrameEffectsEnabled(enabled);
        });

    public static readonly ModSettingsValueBinding<ArabellaSettings, bool> CharacterVoiceEnabledBinding = new(
        Entry.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.IsCharacterVoiceEnabled,
        static (settings, enabled) =>
        {
            settings.IsCharacterVoiceEnabled = enabled;
            ArabellaAudio.ApplyCharacterVoiceEnabled(enabled);
        });

    public static readonly ModSettingsValueBinding<ArabellaSettings, int> CharacterVoiceVolumeBinding = new(
        Entry.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.CharacterVoiceVolumePercent,
        static (settings, percent) =>
        {
            settings.CharacterVoiceVolumePercent = Mathf.Clamp(percent, 50, 150);
            ArabellaAudio.ApplyCharacterVoiceVolumePercent(settings.CharacterVoiceVolumePercent);
        });

    public static readonly ModSettingsValueBinding<ArabellaSettings, bool> SoundEffectsEnabledBinding = new(
        Entry.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.IsSoundEffectsEnabled,
        static (settings, enabled) =>
        {
            settings.IsSoundEffectsEnabled = enabled;
            ArabellaAudio.ApplySoundEffectsEnabled(enabled);
        });

    public static readonly ModSettingsValueBinding<ArabellaSettings, int> SoundEffectsVolumeBinding = new(
        Entry.ModId,
        DataKey,
        SaveScope.Global,
        static settings => settings.SoundEffectsVolumePercent,
        static (settings, percent) =>
        {
            settings.SoundEffectsVolumePercent = Mathf.Clamp(percent, 50, 150);
            ArabellaAudio.ApplySoundEffectsVolumePercent(settings.SoundEffectsVolumePercent);
        });

    public static void Register()
    {
        ModDataStore.For(Entry.ModId).Register<ArabellaSettings>(
            key: DataKey,
            fileName: "arabella_settings.json",
            scope: SaveScope.Global,
            defaultFactory: static () => new ArabellaSettings(),
            autoCreateIfMissing: true);

        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            .WithTitle(ModSettingsText.Literal(T("艾拉贝拉设置", "Arabella Settings")))
            .WithModDisplayName(ModSettingsText.Literal("Arabella Mod"))
            .WithVisibleOnHostSurfaces(
                ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause)
            .AddSection("visual_effects", section => section
                .WithTitle(ModSettingsText.Literal(T("视觉特效", "Visual Effects")))
                .AddToggle(
                    "back_pose_enabled",
                    ModSettingsText.Literal(T("启用背身动画", "Enable Back-Pose Animation")),
                    BackPoseEnabledBinding)
                .AddIntSlider(
                    "back_pose_scale",
                    ModSettingsText.Literal(T("背身动画大小", "Back-Pose Size")),
                    BackPoseScaleBinding,
                    minValue: 80,
                    maxValue: 150,
                    step: 5,
                    valueFormatter: static value => $"{value}%")
                .AddToggle(
                    "sequence_frame_effects_enabled",
                    ModSettingsText.Literal(T("序列帧特效", "Frame-Sequence Effects")),
                    SequenceFrameEffectsEnabledBinding)
                .AddButton(
                    "reset_visual_effects",
                    ModSettingsText.Literal(T("恢复默认", "Restore Defaults")),
                    ModSettingsText.Literal(T("重置", "Reset")),
                    host =>
                    {
                        BackPoseEnabledBinding.Write(true);
                        BackPoseScaleBinding.Write(150);
                        SequenceFrameEffectsEnabledBinding.Write(true);
                        host.MarkDirty(BackPoseEnabledBinding);
                        host.MarkDirty(BackPoseScaleBinding);
                        host.MarkDirty(SequenceFrameEffectsEnabledBinding);
                        host.RequestRefresh();
                    },
                    ModSettingsButtonTone.Normal))
            .AddSection("audio", section => section
                .WithTitle(ModSettingsText.Literal(T("音效", "Audio")))
                .AddToggle(
                    "character_voice_enabled",
                    ModSettingsText.Literal(T("人物语音", "Character Voice")),
                    CharacterVoiceEnabledBinding)
                .AddIntSlider(
                    "character_voice_volume",
                    ModSettingsText.Literal(T("人物语音音量", "Character Voice Volume")),
                    CharacterVoiceVolumeBinding,
                    minValue: 50,
                    maxValue: 150,
                    step: 5,
                    valueFormatter: static value => $"{value}%")
                .AddToggle(
                    "sound_effects_enabled",
                    ModSettingsText.Literal(T("特效声音", "Sound Effects")),
                    SoundEffectsEnabledBinding)
                .AddIntSlider(
                    "sound_effects_volume",
                    ModSettingsText.Literal(T("特效声音音量", "Sound Effects Volume")),
                    SoundEffectsVolumeBinding,
                    minValue: 50,
                    maxValue: 150,
                    step: 5,
                    valueFormatter: static value => $"{value}%")
                .AddButton(
                    "reset_audio",
                    ModSettingsText.Literal(T("恢复默认", "Restore Defaults")),
                    ModSettingsText.Literal(T("重置", "Reset")),
                    host =>
                    {
                        CharacterVoiceEnabledBinding.Write(true);
                        CharacterVoiceVolumeBinding.Write(100);
                        SoundEffectsEnabledBinding.Write(true);
                        SoundEffectsVolumeBinding.Write(100);
                        host.MarkDirty(CharacterVoiceEnabledBinding);
                        host.MarkDirty(CharacterVoiceVolumeBinding);
                        host.MarkDirty(SoundEffectsEnabledBinding);
                        host.MarkDirty(SoundEffectsVolumeBinding);
                        host.RequestRefresh();
                    },
                    ModSettingsButtonTone.Normal)));
    }

    private static string T(string chinese, string english)
    {
        string locale = TranslationServer.GetLocale();
        return !string.IsNullOrEmpty(locale) && locale.StartsWith("zh")
            ? chinese
            : english;
    }
}
