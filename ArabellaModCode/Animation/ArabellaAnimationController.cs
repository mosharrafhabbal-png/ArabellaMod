using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ArabellaMod.Animation;

/// <summary>
/// 单个战斗人物的动画状态机。
/// 它只负责播放顺序和打断规则；素材路径统一由 ArabellaAnimationAssets 管理。
/// </summary>
internal sealed class ArabellaAnimationController
{
    private enum ActionState
    {
        Idle,
        Attack,
        Skill,
        Power,
        Hit,
        Death
    }

    private readonly AnimatedSprite2D _sprite;
    private readonly NCreature _creatureNode;
    private readonly Vector2 _defaultSpritePosition;
    private ActionState _state = ActionState.Idle;

    // 新返回动画使用 1000×720 画布，旧战斗动画使用 800×720 画布。
    // 根据首尾帧透明像素重心与脚底位置校准，避免切换时人物横向或纵向跳动。
    private static readonly Vector2 ReturnTransitionSpriteOffset = new(53f, -74f);
    private const int ReturnTransitionWhipSoundFrame = 10;

    // 参考 YukiMod 的近战位移：根据目标脚底位置计算落点，而不是使用固定的屏幕坐标。
    private const float AttackStopDistance = 250f;
    private Creature? _pendingAttackTarget;
    private bool _pendingAttackTargetsAllEnemies;
    private bool _hasAttackOrigin;
    private Vector2 _attackOriginGlobalPosition;
    private Vector2 _attackDestinationGlobalPosition;
    private TaskCompletionSource<bool>? _attackImpactCompletion;

    // 第一次攻击随机选 A/B，后续避免连续抽到同一套动画。
    // 只有两套攻击时，这样既保留随机起手，也能让玩家稳定看到两种动作。
    private bool? _lastAttackWasA;

    // 攻击结束后留出短暂的命中窗口，确保伤害数字先出现，再播放返回待机动画。
    private const double AttackImpactPauseSeconds = 0.10;

    // 每次开始新动作都会递增版本号。旧异步序列醒来后发现版本不一致，就不会覆盖新动作。
    private int _sequenceVersion;

    public ArabellaAnimationController(NCreature creatureNode, AnimatedSprite2D sprite)
    {
        _creatureNode = creatureNode;
        _sprite = sprite;
        _defaultSpritePosition = sprite.Position;
        // 全部控制器共享初始化阶段已经载入的帧集，首次攻击不会再触发磁盘读取或纹理解码。
        _sprite.SpriteFrames = ArabellaAnimationAssets.GetPreloadedCombatFrames();
        ReturnToIdleWithoutVersionCheck();
    }

    /// <summary>
    /// 由出牌入口提前记录这次攻击的位移目标。
    /// 攻击 Trigger 到来时会立即取走，不会污染下一张牌。
    /// </summary>
    public void SetAttackTarget(Creature? target, bool targetsAllEnemies)
    {
        _pendingAttackTarget = target;
        _pendingAttackTargetsAllEnemies = targetsAllEnemies;
    }

    public bool PlayAttack()
    {
        if (!CanStart(ActionState.Attack))
        {
            return false;
        }

        StringName attack = ChooseAttackAnimation();
        Creature? attackTarget = _pendingAttackTarget;
        bool targetsAllEnemies = _pendingAttackTargetsAllEnemies;
        _pendingAttackTarget = null;
        _pendingAttackTargetsAllEnemies = false;

        // 伤害命令等待这个完成源，不再根据固定秒数猜测动画进度。
        _attackImpactCompletion?.TrySetResult(true);
        _attackImpactCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        int version = Begin(ActionState.Attack);
        RunAttackSequence(version, attack, attackTarget, targetsAllEnemies);
        return true;
    }

    /// <summary>
    /// 启动攻击并返回一个在真实命中帧到达时才完成的 Task。
    /// CreatureCmd 等待它之后才会结算伤害和显示数字。
    /// </summary>
    public Task PlayAttackAndWaitForImpact(out bool startedNewAttack)
    {
        if (_state == ActionState.Attack && _attackImpactCompletion != null)
        {
            if (!_attackImpactCompletion.Task.IsCompleted)
            {
                startedNewAttack = false;
                return _attackImpactCompletion.Task;
            }

            // 多段攻击的下一击可能在上一击回待机前立即到来，先收尾再重新播放。
            RestoreAttackPosition();
            _state = ActionState.Idle;
        }

        startedNewAttack = PlayAttack();
        return _attackImpactCompletion?.Task ?? Task.CompletedTask;
    }

    public bool PlaySkill()
    {
        if (!CanStart(ActionState.Skill))
        {
            return false;
        }

        int version = Begin(ActionState.Skill);
        RunSkillSequence(version);
        return true;
    }

    public bool PlayPower()
    {
        if (!CanStart(ActionState.Power))
        {
            return false;
        }

        int version = Begin(ActionState.Power);
        ArabellaVfx.PlayPowerBuff(_creatureNode, _sprite);
        RunPowerSequence(version);
        return true;
    }

    public bool PlayHit()
    {
        if (_state == ActionState.Death || _state == ActionState.Hit)
        {
            return false;
        }

        // 受击会打断攻击、技能或能力动画，防止伤害反馈被长动画吞掉。
        int version = Begin(ActionState.Hit);
        RunHitSequence(version);
        return true;
    }

    public bool PlayDeath()
    {
        if (_state == ActionState.Death)
        {
            return false;
        }

        // 死亡拥有最高优先级；版本号会让所有尚未结束的旧序列自动失效。
        int version = Begin(ActionState.Death);
        RunDeathSequence(version);
        return true;
    }

    private bool CanStart(ActionState requestedState)
    {
        if (_state == ActionState.Death)
        {
            return false;
        }

        // 多段伤害可能连续触发 Attack，同一动作进行中不从头重播。
        return _state == ActionState.Idle || _state != requestedState;
    }

    private int Begin(ActionState state)
    {
        if (state != ActionState.Attack)
        {
            // 受击、死亡或其他动作打断攻击时，必须先回到原位。
            CompleteAttackImpactWait();
            RestoreAttackPosition();
        }

        _state = state;
        _sequenceVersion++;
        _sprite.Stop();
        // 如果返回动画被受击或死亡打断，先恢复其他动画共用的默认位置。
        _sprite.Position = _defaultSpritePosition;
        return _sequenceVersion;
    }

    private async void RunAttackSequence(
        int version,
        StringName attack,
        Creature? attackTarget,
        bool targetsAllEnemies)
    {
        try
        {
            if (!await PlayAttackTransitionWithLeadSound(version)) return;
            if (!await PlayOnce(ArabellaAnimationAssets.AttackReadyName, version)) return;

            // 攻击准备完成后移到目标面前，并在攻击期间持续锁定落点。
            IReadOnlyList<NCreature> enemyNodes = targetsAllEnemies
                ? GetLivingEnemyNodes()
                : Array.Empty<NCreature>();
            Vector2 enemyCenter = _sprite.GlobalPosition;
            if (targetsAllEnemies && enemyNodes.Count > 0)
            {
                Rect2 enemyBounds = ArabellaVfx.GetCombinedHitboxBounds(enemyNodes);
                enemyCenter = enemyBounds.GetCenter();
                TryMoveToEnemyCenter(enemyCenter, enemyNodes, version);
            }
            else
            {
                TryMoveToAttackTarget(attackTarget, version);
            }

            if (!await PlayAttackAndSignalImpact(
                    attack,
                    version,
                    attackTarget,
                    targetsAllEnemies,
                    enemyNodes,
                    enemyCenter)) return;
            if (!await PlayOnce(ArabellaAnimationAssets.AttackEndName, version)) return;
            if (!await WaitSeconds(AttackImpactPauseSeconds, version)) return;

            // 先让伤害数字出现，再回到原位并正向播放专用的 B 待机返回动画。
            RestoreAttackPosition();
            if (!await PlayReturnTransition(version)) return;

            ReturnToIdle(version);
        }
        catch (Exception exception)
        {
            HandleSequenceError("攻击", version, exception);
        }
    }

    private async void RunSkillSequence(int version)
    {
        try
        {
            if (!await PlayOnce(ArabellaAnimationAssets.TransitionName, version)) return;
            if (!await PlayOnce(ArabellaAnimationAssets.SkillReadyName, version)) return;
            if (!await PlayOnce(ArabellaAnimationAssets.SkillReleaseName, version)) return;
            if (!await PlayReturnTransition(version)) return;

            // 技能释放结束后，正向播放专用返回动画，再回到普通待机。
            ReturnToIdle(version);
        }
        catch (Exception exception)
        {
            HandleSequenceError("技能", version, exception);
        }
    }

    private async void RunPowerSequence(int version)
    {
        try
        {
            if (!await PlayOnce(ArabellaAnimationAssets.TransitionName, version)) return;
            if (!await PlayOnce(ArabellaAnimationAssets.PowerReadyName, version)) return;
            if (!await PlayOnce(ArabellaAnimationAssets.PowerReleaseName, version)) return;
            if (!await PlayReturnTransition(version)) return;

            ReturnToIdle(version);
        }
        catch (Exception exception)
        {
            HandleSequenceError("能力", version, exception);
        }
    }

    private async void RunHitSequence(int version)
    {
        try
        {
            if (!await PlayOnce(ArabellaAnimationAssets.HitName, version)) return;
            ReturnToIdle(version);
        }
        catch (Exception exception)
        {
            HandleSequenceError("受击", version, exception);
        }
    }

    private async void RunDeathSequence(int version)
    {
        try
        {
            // 不调用 ReturnToIdle，让人物停留在死亡动画最后一帧。
            await PlayOnce(ArabellaAnimationAssets.DeathName, version);
        }
        catch (Exception exception)
        {
            HandleSequenceError("死亡", version, exception);
        }
    }

    private async Task<bool> PlayOnce(StringName animation, int version, bool backwards = false)
    {
        if (!IsCurrent(version))
        {
            return false;
        }

        if (backwards)
        {
            _sprite.PlayBackwards(animation);
        }
        else
        {
            _sprite.Play(animation);
        }

        await _sprite.ToSignal(_sprite, AnimatedSprite2D.SignalName.AnimationFinished);
        return IsCurrent(version);
    }

    private async Task<bool> PlayAttackTransitionWithLeadSound(int version)
    {
        if (!IsCurrent(version))
        {
            return false;
        }

        _sprite.Play(ArabellaAnimationAssets.TransitionName);
        while (IsCurrent(version) &&
               _sprite.Animation == ArabellaAnimationAssets.TransitionName &&
               _sprite.Frame < ArabellaAnimationAssets.AttackSoundTriggerFrame)
        {
            await _sprite.ToSignal(_sprite, AnimatedSprite2D.SignalName.FrameChanged);
        }

        if (!IsCurrent(version) ||
            _sprite.Animation != ArabellaAnimationAssets.TransitionName)
        {
            return false;
        }

        ArabellaAudio.PlayEnemyHitBeforeImpact();
        await _sprite.ToSignal(_sprite, AnimatedSprite2D.SignalName.AnimationFinished);
        return IsCurrent(version);
    }

    /// <summary>
    /// 正向播放独立的 B 待机返回动画，并仅在这段动画期间应用画布差异补偿。
    /// </summary>
    private async Task<bool> PlayReturnTransition(int version)
    {
        if (!IsCurrent(version))
        {
            return false;
        }

        _sprite.Position = _defaultSpritePosition + ReturnTransitionSpriteOffset;
        _sprite.Play(ArabellaAnimationAssets.ReturnTransitionName);

        // 文件 frame_30_000010.png 对应索引 10；直接监听真实帧变化，调整 FPS 后仍精确同步。
        while (IsCurrent(version) &&
               _sprite.Animation == ArabellaAnimationAssets.ReturnTransitionName &&
               _sprite.Frame < ReturnTransitionWhipSoundFrame)
        {
            await _sprite.ToSignal(_sprite, AnimatedSprite2D.SignalName.FrameChanged);
        }

        if (!IsCurrent(version) ||
            _sprite.Animation != ArabellaAnimationAssets.ReturnTransitionName)
        {
            return false;
        }

        ArabellaAudio.Play(ArabellaAudio.Cue.ReturnWhip, cooldownMilliseconds: 0);
        await _sprite.ToSignal(_sprite, AnimatedSprite2D.SignalName.AnimationFinished);

        if (GodotObject.IsInstanceValid(_sprite))
        {
            _sprite.Position = _defaultSpritePosition;
        }

        return IsCurrent(version);
    }

    private async Task<bool> PlayAttackAndSignalImpact(
        StringName attack,
        int version,
        Creature? attackTarget,
        bool targetsAllEnemies,
        IReadOnlyList<NCreature> enemyNodes,
        Vector2 enemyCenter)
    {
        if (!IsCurrent(version))
        {
            return false;
        }

        if (targetsAllEnemies)
        {
            ArabellaVfx.PlayAllAttack(
                _creatureNode,
                _sprite,
                enemyNodes,
                enemyCenter);
        }
        else
        {
            ArabellaVfx.PlaySingleAttack(
                _creatureNode,
                _sprite,
                attackTarget,
                attack == ArabellaAnimationAssets.AttackAName);
        }

        _sprite.Play(attack);
        int impactFrame = ArabellaAnimationAssets.AttackImpactFrame;

        // 不用固定秒数，直接等到指定帧，因此调整 FPS 后命中点仍然稳定。
        while (IsCurrent(version) &&
               _sprite.Animation == attack &&
               _sprite.Frame < impactFrame)
        {
            await _sprite.ToSignal(_sprite, AnimatedSprite2D.SignalName.FrameChanged);
        }

        if (!IsCurrent(version) || _sprite.Animation != attack)
        {
            return false;
        }

        // 人物已经完成冲刺；主体攻击进入第 4 帧时放行伤害命令。
        // 随后继续播完主体攻击与攻击结束动画，视觉动作不会被截断。
        CompleteAttackImpactWait();
        await _sprite.ToSignal(_sprite, AnimatedSprite2D.SignalName.AnimationFinished);
        return IsCurrent(version);
    }

    private async Task<bool> WaitSeconds(double seconds, int version)
    {
        if (!IsCurrent(version))
        {
            return false;
        }

        SceneTreeTimer timer = _sprite.GetTree().CreateTimer(seconds);
        await _sprite.ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        return IsCurrent(version);
    }

    private StringName ChooseAttackAnimation()
    {
        bool useAttackA = _lastAttackWasA.HasValue
            ? !_lastAttackWasA.Value
            : Random.Shared.Next(2) == 0;

        _lastAttackWasA = useAttackA;
        return useAttackA
            ? ArabellaAnimationAssets.AttackAName
            : ArabellaAnimationAssets.AttackBName;
    }

    private IReadOnlyList<NCreature> GetLivingEnemyNodes()
    {
        List<NCreature> result = [];
        NCombatRoom? room = NCombatRoom.Instance;
        IEnumerable<Creature> enemies =
            _creatureNode.Entity.CombatState?.HittableEnemies ?? Array.Empty<Creature>();
        if (!GodotObject.IsInstanceValid(room))
        {
            return result;
        }

        foreach (Creature enemy in enemies)
        {
            if (!enemy.IsAlive)
            {
                continue;
            }

            NCreature? enemyNode = room.GetCreatureNode(enemy);
            if (GodotObject.IsInstanceValid(enemyNode))
            {
                result.Add(enemyNode);
            }
        }

        return result;
    }

    private void TryMoveToEnemyCenter(
        Vector2 enemyCenter,
        IReadOnlyList<NCreature> enemyNodes,
        int version)
    {
        if (!IsCurrent(version) || !GodotObject.IsInstanceValid(_creatureNode) ||
            enemyNodes.Count == 0)
        {
            return;
        }

        Vector2 attackerFoot = GetHitboxFoot(_creatureNode);
        float enemyFootY = enemyNodes.Average(enemy => GetHitboxFoot(enemy).Y);
        Vector2 desiredFoot = new(enemyCenter.X, enemyFootY);
        Vector2 footOffset = attackerFoot - _creatureNode.GlobalPosition;

        _attackOriginGlobalPosition = _creatureNode.GlobalPosition;
        _attackDestinationGlobalPosition = new Vector2(
            Mathf.Round(desiredFoot.X - footOffset.X),
            Mathf.Round(desiredFoot.Y - footOffset.Y));
        _hasAttackOrigin = true;

        _creatureNode.GlobalPosition = _attackDestinationGlobalPosition;
        _creatureNode.GlobalPosition = _attackDestinationGlobalPosition;
        RunAttackPositionLock(version);
    }

    private void TryMoveToAttackTarget(Creature? target, int version)
    {
        if (target == null || !target.IsAlive || !IsCurrent(version) ||
            NCombatRoom.Instance == null || !GodotObject.IsInstanceValid(_creatureNode))
        {
            return;
        }

        NCreature? targetNode = NCombatRoom.Instance.GetCreatureNode(target);
        if (targetNode == null || !GodotObject.IsInstanceValid(targetNode))
        {
            return;
        }

        Vector2 attackerFoot = GetHitboxFoot(_creatureNode);
        Vector2 targetFoot = GetHitboxFoot(targetNode);
        Vector2 toTarget = targetFoot - attackerFoot;
        float currentDistance = toTarget.Length();
        if (currentDistance <= AttackStopDistance || currentDistance < 0.001f)
        {
            return;
        }

        Vector2 direction = toTarget / currentDistance;
        Vector2 desiredFoot = targetFoot - direction * AttackStopDistance;
        Vector2 footOffset = attackerFoot - _creatureNode.GlobalPosition;

        _attackOriginGlobalPosition = _creatureNode.GlobalPosition;
        _attackDestinationGlobalPosition = new Vector2(
            Mathf.Round(desiredFoot.X - footOffset.X),
            Mathf.Round(desiredFoot.Y - footOffset.Y));
        _hasAttackOrigin = true;

        // YukiMod 会在位移后重复设定坐标并按帧锁位，防止战斗布局在动画中重置人物。
        _creatureNode.GlobalPosition = _attackDestinationGlobalPosition;
        _creatureNode.GlobalPosition = _attackDestinationGlobalPosition;
        RunAttackPositionLock(version);
    }

    private async void RunAttackPositionLock(int version)
    {
        try
        {
            while (_hasAttackOrigin && _state == ActionState.Attack && IsCurrent(version))
            {
                _creatureNode.GlobalPosition = _attackDestinationGlobalPosition;
                await _creatureNode.ToSignal(
                    _creatureNode.GetTree(),
                    SceneTree.SignalName.ProcessFrame);
            }
        }
        catch (Exception exception)
        {
            Entry.Logger.Warn($"攻击位移锁定发生异常：{exception.Message}");
            RestoreAttackPosition();
        }
    }

    private static Vector2 GetHitboxFoot(NCreature creatureNode)
    {
        Control? hitbox = creatureNode.Hitbox;
        if (hitbox != null && GodotObject.IsInstanceValid(hitbox))
        {
            Rect2 rect = hitbox.GetGlobalRect();
            return new Vector2(
                rect.Position.X + rect.Size.X * 0.5f,
                rect.Position.Y + rect.Size.Y);
        }

        return creatureNode.GlobalPosition;
    }

    private void RestoreAttackPosition()
    {
        if (!_hasAttackOrigin)
        {
            return;
        }

        if (GodotObject.IsInstanceValid(_creatureNode))
        {
            _creatureNode.GlobalPosition = _attackOriginGlobalPosition;
            _creatureNode.GlobalPosition = _attackOriginGlobalPosition;
        }

        _hasAttackOrigin = false;
        _attackOriginGlobalPosition = Vector2.Zero;
        _attackDestinationGlobalPosition = Vector2.Zero;
    }

    private void CompleteAttackImpactWait()
    {
        _attackImpactCompletion?.TrySetResult(true);
    }

    private bool IsCurrent(int version)
    {
        return version == _sequenceVersion && GodotObject.IsInstanceValid(_sprite);
    }

    private void ReturnToIdle(int version)
    {
        if (!IsCurrent(version) || _state == ActionState.Death)
        {
            return;
        }

        _state = ActionState.Idle;
        ReturnToIdleWithoutVersionCheck();
    }

    private void ReturnToIdleWithoutVersionCheck()
    {
        RestoreAttackPosition();
        _sprite.Stop();
        _sprite.Position = _defaultSpritePosition;
        _sprite.Play(ArabellaAnimationAssets.IdleName);
    }

    private void HandleSequenceError(string actionName, int version, Exception exception)
    {
        Entry.Logger.Warn($"{actionName}动画序列发生异常：{exception.Message}");
        CompleteAttackImpactWait();
        RestoreAttackPosition();
        ReturnToIdle(version);
    }
}
