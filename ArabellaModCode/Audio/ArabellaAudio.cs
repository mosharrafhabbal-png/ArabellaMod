using Godot;
using ArabellaMod.Settings;

namespace ArabellaMod;

/// <summary>
/// 艾拉贝拉的全局语音服务。
/// 负责语音分组、预加载、随机播放、重复规避和短时间防抖。
/// </summary>
public static class ArabellaAudio
{
    public enum Cue
    {
        CharacterSelect,
        BackPose,
        Attack,
        Skill,
        Power,
        Hit,
        EnemyHit,
        LacerationDamage,
        Death,
        Victory,
        ReturnWhip
    }

    private sealed record AudioEntry(string ResourcePath, float VolumeDb);

    private const string AudioRoot = $"{Entry.ResPath}/audio";
    private const ulong DefaultCooldownMilliseconds = 250;
    private const ulong PreplayedEnemyHitSuppressionMilliseconds = 1_200;

    // 角色语音素材的原始响度偏低，当前统一增益 8 dB；只影响艾拉贝拉语音，不改变游戏主音量。
    private const float VoiceVolumeDb = 8f;

    /// <summary>
    /// 选人界面的语音冷却。反复点击时，10 秒内不会重复触发。
    /// </summary>
    public const ulong CharacterSelectCooldownMilliseconds = 10_000;

    private static readonly Dictionary<Cue, List<AudioEntry>> AudioPools = new();
    private static readonly Dictionary<Cue, int> LastPlayedIndex = new();
    private static readonly Dictionary<Cue, ulong> LastPlayedAt = new();
    private static readonly Dictionary<AudioStreamPlayer, float> ActivePlayerBaseVolumes = [];
    private static readonly HashSet<AudioStreamPlayer> ActiveVoicePlayers = [];
    private static readonly HashSet<AudioStreamPlayer> ActiveSoundEffectPlayers = [];
    private static readonly HashSet<AudioStreamPlayer> ActiveCardVoicePlayers = [];
    private static AudioStreamPlayer? _activeBackPoseVoicePlayer;
    private static bool _isBackPoseVoicePending;
    private static ulong _suppressNextEnemyHitUntil;

    // 持有预加载后的资源引用，避免第一次触发语音时再同步读取磁盘造成卡顿。
    private static readonly Dictionary<string, AudioStream> PreloadedStreams = new(StringComparer.Ordinal);

    public static void Initialize()
    {
        StopPlayers(ActiveVoicePlayers);
        StopPlayers(ActiveSoundEffectPlayers);
        ActiveCardVoicePlayers.Clear();

        AudioPools.Clear();
        LastPlayedIndex.Clear();
        LastPlayedAt.Clear();
        _isBackPoseVoicePending = false;
        _suppressNextEnemyHitUntil = 0;
        PreloadedStreams.Clear();

        RegisterPool(Cue.CharacterSelect,
            "alabeilaxuanrenyuyin",
            "alabeilaxuanrenyuyin02");

        // 卡牌悬停背身立绘出现时随机播放，独立 Cue 便于使用较长冷却防止扫牌叠音。
        RegisterPool(Cue.BackPose,
            "alabeiladaiji",
            "alabeiladaiji02",
            "alabeiladaiji03",
            "alabeiladaiji04");

        // 用户提供的攻击语音文件沿用 daiji 命名，但只在打出攻击牌时播放。
        RegisterPool(Cue.Attack,
            "alabeiladaiji",
            "alabeiladaiji02",
            "alabeiladaiji03",
            "alabeiladaiji04");

        RegisterPool(Cue.Skill,
            "alabeilajineng",
            "alabeilajineng02",
            "alabeilajineng03",
            "alabeilajineng04",
            "alabeilajineng05");

        RegisterPool(Cue.Power,
            "alabeilanengli",
            "alabeilanengli02",
            "alabeilanengli03");

        RegisterPool(Cue.Hit,
            "alabeilashoudaogongji",
            "alabeilashoudaogongji02",
            "alabeilashoudaogongji03",
            "alabeilashoudaogongji04");

        Register(Cue.EnemyHit, $"{AudioRoot}/teshu/teshugongji.mp3");
        Register(Cue.EnemyHit, $"{AudioRoot}/teshu/teshugongji02.mp3");
        Register(Cue.EnemyHit, $"{AudioRoot}/teshu/putonggongji01.mp3");

        Register(
            Cue.LacerationDamage,
            $"{AudioRoot}/teshu/debuffshagnhai.mp3");

        RegisterPool(Cue.Death, "alabeilasiwang");
        RegisterPool(Cue.Victory, "alabeilahuosheng");

        // B 待机返回动画第 10 帧的短鞭声。与其他角色音频使用相同增益，并在初始化阶段预载。
        Register(
            Cue.ReturnWhip,
            $"{AudioRoot}/teshu/bianzishengyin02duanzan.mp3",
            VoiceVolumeDb);

        PreloadRegisteredAudio();
        Entry.Logger.Info($"艾拉贝拉全局语音已初始化：{PreloadedStreams.Count} 个音频资源已预加载。");
    }

    public static void Register(Cue cue, string resourcePath, float volumeDb = 0f)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new ArgumentException("音频资源路径不能为空。", nameof(resourcePath));
        }

        if (!AudioPools.TryGetValue(cue, out List<AudioEntry>? pool))
        {
            pool = [];
            AudioPools[cue] = pool;
        }

        pool.Add(new(resourcePath, volumeDb));
    }

    public static void Play(Cue cue, ulong cooldownMilliseconds = DefaultCooldownMilliseconds)
    {
        if (!AudioPools.TryGetValue(cue, out List<AudioEntry>? pool) || pool.Count == 0)
        {
            return;
        }

        if (!IsCategoryEnabled(cue))
        {
            return;
        }

        // The back-pose line owns the character-voice lane while it is being queued or
        // played. Card animation voices must never overlap it; combat sound effects and
        // unrelated character lines remain unaffected.
        if (IsCardPlayVoice(cue) && IsBackPoseVoiceReservedOrPlaying())
        {
            return;
        }

        ulong now = Time.GetTicksMsec();
        if (LastPlayedAt.TryGetValue(cue, out ulong lastPlayedAt) &&
            now - lastPlayedAt < cooldownMilliseconds)
        {
            return;
        }

        int selectedIndex = SelectIndex(cue, pool.Count);
        LastPlayedAt[cue] = now;
        LastPlayedIndex[cue] = selectedIndex;
        if (cue == Cue.BackPose)
        {
            // Reserve synchronously so a card voice requested before the deferred audio
            // player is attached to the scene tree is suppressed as well.
            _isBackPoseVoicePending = true;
        }

        PlayResource(cue, pool[selectedIndex]);
    }

    /// <summary>
    /// Plays the existing attack/skill card line pool for the matching back-pose
    /// one-shot. The one-shot takes over the character-voice lane from the idle
    /// back pose, so its line is not discarded just because the hovered-card idle
    /// line is still queued or playing.
    /// </summary>
    public static void PlayBackPoseCardVoice(Cue cue)
    {
        if (cue is not (Cue.Attack or Cue.Skill))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cue),
                cue,
                "Only attack and skill voices have matching back-pose animations.");
        }

        // Cancel a deferred idle line as well as one that has already started.
        // The idle pool and its normal hover behavior otherwise remain unchanged.
        _isBackPoseVoicePending = false;
        StopActiveBackPoseVoice();
        Play(cue);
    }

    public static void PlayEnemyHitBeforeImpact()
    {
        _suppressNextEnemyHitUntil =
            Time.GetTicksMsec() + PreplayedEnemyHitSuppressionMilliseconds;
        Play(Cue.EnemyHit, cooldownMilliseconds: 0);
    }

    public static void PlayEnemyHitOnDamage()
    {
        ulong now = Time.GetTicksMsec();
        if (_suppressNextEnemyHitUntil != 0 && now <= _suppressNextEnemyHitUntil)
        {
            _suppressNextEnemyHitUntil = 0;
            return;
        }

        _suppressNextEnemyHitUntil = 0;
        Play(Cue.EnemyHit, cooldownMilliseconds: 0);
    }

    public static void PlayLacerationDamage()
    {
        Play(Cue.LacerationDamage, cooldownMilliseconds: 0);
    }

    internal static void ApplyCharacterVoiceEnabled(bool enabled)
    {
        if (!enabled)
        {
            StopPlayers(ActiveVoicePlayers);
            ActiveCardVoicePlayers.Clear();
            _activeBackPoseVoicePlayer = null;
            _isBackPoseVoicePending = false;
        }
    }

    internal static void ApplyCharacterVoiceVolumePercent(int percent)
    {
        ApplyVolumePercent(ActiveVoicePlayers, percent);
    }

    internal static void ApplySoundEffectsEnabled(bool enabled)
    {
        if (!enabled)
        {
            StopPlayers(ActiveSoundEffectPlayers);
        }
    }

    internal static void ApplySoundEffectsVolumePercent(int percent)
    {
        ApplyVolumePercent(ActiveSoundEffectPlayers, percent);
    }

    private static void RegisterPool(Cue cue, params string[] fileNamesWithoutExtension)
    {
        foreach (string fileName in fileNamesWithoutExtension)
        {
            Register(cue, $"{AudioRoot}/{fileName}.mp3", VoiceVolumeDb);
        }
    }

    private static void PreloadRegisteredAudio()
    {
        foreach (AudioEntry entry in AudioPools.Values.SelectMany(pool => pool))
        {
            AudioStream? stream = ResourceLoader.Load<AudioStream>(
                entry.ResourcePath,
                cacheMode: ResourceLoader.CacheMode.Reuse);
            if (stream == null)
            {
                Entry.Logger.Warn($"语音资源预加载失败：{entry.ResourcePath}");
                continue;
            }

            PreloadedStreams[entry.ResourcePath] = stream;
        }
    }

    private static int SelectIndex(Cue cue, int count)
    {
        if (count <= 1 || !LastPlayedIndex.TryGetValue(cue, out int previousIndex))
        {
            return Random.Shared.Next(count);
        }

        // 从上一条以外的候选中等概率抽取，避免连续两次听到完全相同的语音。
        int selected = Random.Shared.Next(count - 1);
        return selected >= previousIndex ? selected + 1 : selected;
    }

    private static bool IsCardPlayVoice(Cue cue)
    {
        return cue is Cue.Attack or Cue.Skill or Cue.Power;
    }

    private static bool IsVoice(Cue cue)
    {
        return cue is Cue.CharacterSelect or Cue.BackPose or Cue.Attack or Cue.Skill or
            Cue.Power or Cue.Hit or Cue.Death or Cue.Victory;
    }

    private static bool IsCategoryEnabled(Cue cue)
    {
        return IsVoice(cue)
            ? ArabellaSettingsPage.CharacterVoiceEnabledBinding.Read()
            : ArabellaSettingsPage.SoundEffectsEnabledBinding.Read();
    }

    private static int GetCategoryVolumePercent(Cue cue)
    {
        return IsVoice(cue)
            ? ArabellaSettingsPage.CharacterVoiceVolumeBinding.Read()
            : ArabellaSettingsPage.SoundEffectsVolumeBinding.Read();
    }

    private static float GetVolumeOffsetDb(int percent)
    {
        return Mathf.LinearToDb(Mathf.Clamp(percent, 50, 150) / 100f);
    }

    private static void ApplyVolumePercent(
        IEnumerable<AudioStreamPlayer> players,
        int percent)
    {
        float volumeOffsetDb = GetVolumeOffsetDb(percent);
        foreach (AudioStreamPlayer player in players.ToArray())
        {
            if (GodotObject.IsInstanceValid(player) &&
                ActivePlayerBaseVolumes.TryGetValue(player, out float baseVolumeDb))
            {
                player.VolumeDb = baseVolumeDb + volumeOffsetDb;
            }
        }
    }

    private static void StopPlayers(HashSet<AudioStreamPlayer> players)
    {
        foreach (AudioStreamPlayer player in players.ToArray())
        {
            if (GodotObject.IsInstanceValid(player))
            {
                player.Stop();
                player.QueueFree();
            }

            ActivePlayerBaseVolumes.Remove(player);
        }

        players.Clear();
    }

    private static bool IsBackPoseVoiceReservedOrPlaying()
    {
        if (_isBackPoseVoicePending)
        {
            return true;
        }

        if (!GodotObject.IsInstanceValid(_activeBackPoseVoicePlayer))
        {
            _activeBackPoseVoicePlayer = null;
            return false;
        }

        return _activeBackPoseVoicePlayer.Playing;
    }

    private static void StopActiveCardVoices()
    {
        foreach (AudioStreamPlayer player in ActiveCardVoicePlayers.ToArray())
        {
            ActiveVoicePlayers.Remove(player);
            ActivePlayerBaseVolumes.Remove(player);
            if (GodotObject.IsInstanceValid(player))
            {
                player.Stop();
                player.QueueFree();
            }
        }

        ActiveCardVoicePlayers.Clear();
    }

    private static void StopActiveBackPoseVoice()
    {
        AudioStreamPlayer? player = _activeBackPoseVoicePlayer;
        _activeBackPoseVoicePlayer = null;
        if (player != null)
        {
            ActiveVoicePlayers.Remove(player);
            ActivePlayerBaseVolumes.Remove(player);
        }

        if (GodotObject.IsInstanceValid(player))
        {
            player.Stop();
            player.QueueFree();
        }
    }

    private static void PlayResource(Cue cue, AudioEntry entry)
    {
        Callable.From(() =>
        {
            // A committed attack/skill can replace the hovered-card idle pose before
            // this deferred callback runs. Do not resurrect that cancelled idle line.
            if (cue == Cue.BackPose && !_isBackPoseVoicePending)
            {
                return;
            }

            // Recheck after deferral: a back-pose line may have reserved the voice lane
            // after this card voice was originally requested.
            if (IsCardPlayVoice(cue) && IsBackPoseVoiceReservedOrPlaying())
            {
                return;
            }

            if (!IsCategoryEnabled(cue))
            {
                if (cue == Cue.BackPose)
                {
                    _isBackPoseVoicePending = false;
                }

                return;
            }

            if (!PreloadedStreams.TryGetValue(entry.ResourcePath, out AudioStream? stream))
            {
                // 预加载失败时保留一次运行期回退，避免单个资源异常影响其他语音。
                stream = ResourceLoader.Load<AudioStream>(
                    entry.ResourcePath,
                    cacheMode: ResourceLoader.CacheMode.Reuse);
            }

            if (stream == null)
            {
                if (cue == Cue.BackPose)
                {
                    _isBackPoseVoicePending = false;
                }

                Entry.Logger.Warn($"语音资源加载失败：{entry.ResourcePath}");
                return;
            }

            SceneTree? tree = Engine.GetMainLoop() as SceneTree;
            if (tree?.Root == null)
            {
                if (cue == Cue.BackPose)
                {
                    _isBackPoseVoicePending = false;
                }

                return;
            }

            AudioStreamPlayer player = new()
            {
                Name = "ArabellaOneShotAudio",
                Stream = stream,
                VolumeDb = entry.VolumeDb + GetVolumeOffsetDb(GetCategoryVolumePercent(cue)),
                Bus = "Master"
            };
            ActivePlayerBaseVolumes[player] = entry.VolumeDb;

            tree.Root.AddChild(player);
            HashSet<AudioStreamPlayer> categoryPlayers = IsVoice(cue)
                ? ActiveVoicePlayers
                : ActiveSoundEffectPlayers;
            categoryPlayers.Add(player);
            if (cue == Cue.BackPose)
            {
                StopActiveCardVoices();
                StopActiveBackPoseVoice();
                _activeBackPoseVoicePlayer = player;
                _isBackPoseVoicePending = false;
            }
            else if (IsCardPlayVoice(cue))
            {
                ActiveCardVoicePlayers.Add(player);
            }

            player.Finished += () =>
            {
                categoryPlayers.Remove(player);
                ActivePlayerBaseVolumes.Remove(player);
                if (cue == Cue.BackPose && player == _activeBackPoseVoicePlayer)
                {
                    _activeBackPoseVoicePlayer = null;
                }
                else if (IsCardPlayVoice(cue))
                {
                    ActiveCardVoicePlayers.Remove(player);
                }

                player.QueueFree();
            };
            player.Play();
        }).CallDeferred();
    }
}
