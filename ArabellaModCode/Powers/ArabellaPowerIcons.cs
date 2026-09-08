using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Powers;

internal static class ArabellaPowerIcons
{
    private const string Root = $"{Entry.ResPath}/images/powers";

    public static PowerAssetProfile Create(string fileName) => new(
        IconPath: $"{Root}/{fileName}",
        BigIconPath: $"{Root}/{fileName}");
}
