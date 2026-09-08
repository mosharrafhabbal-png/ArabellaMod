using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace ArabellaMod.Mechanics;

[RegisterOwnedCardKeyword(nameof(Control), IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(nameof(Indulgence), IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(nameof(Sensing), IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(nameof(Restless), IncludeInCardHoverTip = true)]
public class ArabellaKeywords
{
    public static readonly CardKeyword Control = Get(nameof(Control));
    public static readonly CardKeyword Indulgence = Get(nameof(Indulgence));
    public static readonly CardKeyword Sensing = Get(nameof(Sensing));
    public static readonly CardKeyword Restless = Get(nameof(Restless));

    private static CardKeyword Get(string localStem)
    {
        return ModContentRegistry
            .GetQualifiedKeywordId(Entry.ModId, localStem)
            .GetModCardKeyword();
    }
}
