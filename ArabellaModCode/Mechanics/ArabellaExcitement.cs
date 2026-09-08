using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace ArabellaMod.Mechanics;

/// <summary>
/// 兴奋的统一读写入口，并负责首次或再次进入 10 层时抽牌。
/// </summary>
public static class ArabellaExcitement
{
    public const int RestlessThreshold = 3;
    public const int IndulgedThreshold = 6;
    public const int ExcitedThreshold = 10;

    public static int Get(Player player)
    {
        return SecondaryResourceCmd.Get(player, ArabellaResources.ExcitementId);
    }

    public static async Task Gain(
        Player player,
        int amount,
        AbstractModel source,
        PlayerChoiceContext? choiceContext = null)
    {
        if (amount <= 0)
            return;

        int oldAmount = Get(player);
        int newAmount = await SecondaryResourceCmd.Gain(
            player,
            ArabellaResources.ExcitementId,
            amount,
            source);

        if (oldAmount < ExcitedThreshold && newAmount >= ExcitedThreshold)
        {
            await CardPileCmd.Draw(
                choiceContext ?? new ThrowingPlayerChoiceContext(),
                2,
                player);
        }
    }

    public static Task Lose(Player player, int amount, AbstractModel source)
    {
        return amount <= 0
            ? Task.CompletedTask
            : SecondaryResourceCmd.Lose(
                player,
                ArabellaResources.ExcitementId,
                amount,
                source);
    }
}
