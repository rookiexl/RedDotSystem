using Game.RedDot;

/// <summary>
/// 装备 Model 与红点注册。
/// </summary>
public sealed class EquipModule : GameModuleBase
{
    public override string Name => "Equip";

    public override void Initialize()
    {
        EquipModel.Singleton.Init();
    }

    public override void RegisterRedDots(IRedDotRegistrar registrar)
    {
        registrar.Register(new EquipRDInitializer());
    }

    public override void Dispose()
    {
        EquipModel.Singleton.Dispose();
    }
}
