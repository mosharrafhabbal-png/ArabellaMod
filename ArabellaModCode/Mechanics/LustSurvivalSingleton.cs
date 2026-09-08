using ArabellaMod.Characters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace ArabellaMod.Mechanics;

/// <summary>
/// Once per combat, consumes all of Arabella's current Lust to prevent lethal damage.
/// Every point actually removed restores three HP.
/// Each successful rescue permanently reduces this player's Lust maximum by one for the run.
/// </summary>
[RegisterSingleton]
public sealed class LustSurvivalSingleton : HookedSingletonModel, ISecondaryResourceHookListener
{
    public const int HealingPerLust = 3;

    private readonly HashSet<Creature> _lethalDamageTargets = [];
    private readonly HashSet<Creature> _deathsPreventedByLust = [];
    private readonly HashSet<Creature> _usedTargets = [];

    public LustSurvivalSingleton() : base(HookType.Combat)
    {
    }

    public override Task BeforeCombatStart()
    {
        _lethalDamageTargets.Clear();
        _deathsPreventedByLust.Clear();
        _usedTargets.Clear();
        return Task.CompletedTask;
    }

    public decimal ModifyMaxSecondaryResource(SecondaryResourceMaxContext context, decimal amount)
    {
        if (context.Player.Character is not ArabellaModCharacter ||
            context.Definition.Id != ArabellaResources.LustId)
            return amount;

        return Math.Min(amount, LustSurvivalRunData.GetMaximum(context.Player));
    }

    public override decimal ModifyHpLostAfterOstyLate(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target.Player is { Character: ArabellaModCharacter } &&
            amount > 0 &&
            amount >= target.CurrentHp)
        {
            _lethalDamageTargets.Add(target);
        }
        else
        {
            _lethalDamageTargets.Remove(target);
        }

        return amount;
    }

    public override bool ShouldDie(Creature creature)
    {
        if (!_lethalDamageTargets.Contains(creature) ||
            _usedTargets.Contains(creature) ||
            creature.Player is not { Character: ArabellaModCharacter } player ||
            LustSurvivalRunData.GetMaximum(player) <= 0 ||
            SecondaryResourceCmd.Get(player, ArabellaResources.LustId) <= 0)
        {
            return true;
        }

        _deathsPreventedByLust.Add(creature);
        return false;
    }

    public override Task AfterDeath(
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength)
    {
        // If another relic, potion or power prevented this death, discard our pending
        // damage marker so a later non-damage death cannot accidentally use it.
        if (!wasRemovalPrevented || !_deathsPreventedByLust.Contains(creature))
            _lethalDamageTargets.Remove(creature);

        return Task.CompletedTask;
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        // Only the death prevented by this hook may consume the once-per-combat use
        // or mutate persistent data. Repeated callbacks must not reduce the cap twice.
        if (!_deathsPreventedByLust.Remove(creature) || !_usedTargets.Add(creature))
            return;

        _lethalDamageTargets.Remove(creature);

        if (creature.Player is not { Character: ArabellaModCharacter } player)
            return;

        int lustBefore = SecondaryResourceCmd.Get(player, ArabellaResources.LustId);
        if (lustBefore <= 0)
            return;

        await SecondaryResourceCmd.Lose(
            player,
            ArabellaResources.LustId,
            lustBefore,
            this);

        int lustRemoved = lustBefore -
                          SecondaryResourceCmd.Get(player, ArabellaResources.LustId);
        // Drain at the old maximum first: a full 9 Lust rescue still restores 27 HP.
        LustSurvivalRunData.RecordSurvival(player);
        await SecondaryResourceCmd.ClampToMax(player, ArabellaResources.LustId, this);
        if (lustRemoved > 0)
            await CreatureCmd.Heal(creature, lustRemoved * HealingPerLust);
    }
}
