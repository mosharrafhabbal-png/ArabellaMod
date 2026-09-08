using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace ArabellaMod.Mechanics;

/// <summary>One persistent counter per player and run; combat resets never erase it.</summary>
public static class LustSurvivalRunData
{
    public sealed class State
    {
        public int MaxLustLost { get; set; }
    }

    private static PlayerRunSavedData<State> _saved = null!;

    public static void Register()
    {
        _saved = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId)
            .RegisterPerPlayer("lust_survival", () => new State(), new RunSavedDataOptions
            {
                SchemaVersion = 1,
                WritePolicy = RunSavedDataWritePolicy.WhenSet
            });
    }

    public static int GetMaximum(Player player) =>
        ArabellaResources.MaxLust - Math.Clamp(_saved.Get(player).MaxLustLost, 0, ArabellaResources.MaxLust);

    public static void RecordSurvival(Player player) =>
        _saved.Modify(player, state => state.MaxLustLost =
            Math.Clamp(state.MaxLustLost, 0, ArabellaResources.MaxLust - 1) + 1);
}
