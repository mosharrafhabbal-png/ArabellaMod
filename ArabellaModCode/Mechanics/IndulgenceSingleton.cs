using ArabellaMod.Characters;
using ArabellaMod.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace ArabellaMod.Mechanics;

/// <summary>
/// Dispatches each exhausted card's card-specific Indulgence (放纵) effect.
/// </summary>
[RegisterSingleton]
public sealed class IndulgenceSingleton : HookedSingletonModel
{
    public IndulgenceSingleton() : base(HookType.Combat)
    {
    }

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext,
        CardModel card,
        bool causedByEthereal)
    {
        if (card.Owner.Character is not ArabellaModCharacter ||
            !card.Keywords.Contains(ArabellaKeywords.Indulgence) ||
            card is not IIndulgenceCard indulgenceCard)
        {
            return;
        }

        // Snapshot before resolving: nested exhausts get their own repetitions,
        // while powers gained during this effect only affect future exhausts.
        ArabellaIndulgentNaturePower? indulgentNature =
            card.Owner.Creature.GetPower<ArabellaIndulgentNaturePower>();
        int extraResolutions = Math.Max(0, indulgentNature?.Amount ?? 0);

        await ArabellaExcitement.Gain(card.Owner, 1, this, choiceContext);
        await indulgenceCard.OnIndulgence(choiceContext, causedByEthereal);

        if (extraResolutions > 0)
        {
            indulgentNature!.Flash();
            for (int i = 0; i < extraResolutions; i++)
                await indulgenceCard.OnIndulgence(choiceContext, causedByEthereal);
        }

        if (card.Owner.Creature.GetPower<ArabellaIndulgenceQueenPower>() is { } queen)
            await queen.OnIndulgence(choiceContext);
    }
}
