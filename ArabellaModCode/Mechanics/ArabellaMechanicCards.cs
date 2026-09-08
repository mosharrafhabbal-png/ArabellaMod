using MegaCrit.Sts2.Core.GameActions.Multiplayer;
namespace ArabellaMod.Mechanics;

/// <summary>
/// Marks Arabella's own cards. Excitement's dynamic Lust payment is refreshed
/// by the combat Energy-cost hook rather than being captured when the card is initialized.
/// </summary>
internal interface IArabellaExcitementCostCard
{
}

/// <summary>
/// Implement on a card that has <see cref="ArabellaKeywords.Control"/>.
/// While this card is in Exhaust, Energy/Lust spent to play other cards advances its
/// independent progress and triggers Control. The card's own play cost never contributes.
/// Each entry into Exhaust starts a new progress track for that card instance.
/// </summary>
public interface IControlCard
{
    int ControlThreshold { get; }

    Task OnControl(PlayerChoiceContext choiceContext)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Implement on a card that has <see cref="ArabellaKeywords.Indulgence"/>.
/// The singleton invokes this callback after the card is exhausted.
/// </summary>
public interface IIndulgenceCard
{
    Task OnIndulgence(PlayerChoiceContext choiceContext, bool causedByEthereal);
}

/// <summary>
/// Implement on a card that has <see cref="ArabellaKeywords.Sensing"/>.
/// This hook has no choice context because arbitrary pile movements do not provide one.
/// </summary>
public interface ISensingCard
{
    Task OnSensing();
}
