using MegaCrit.Sts2.Core.Entities.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace ArabellaMod.Powers;

/// <summary>
/// 裂创增幅：每层使裂创在结算并减半后补回2层。
/// </summary>
[RegisterPower]
public sealed class ArabellaLacerationAmplificationPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => ArabellaPowerIcons.Create(
        "icon_1501711_chain_scale.png");
}
