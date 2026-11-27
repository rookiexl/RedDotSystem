using Game.RedDot;

/// <summary>
/// 背包 Model 与红点注册。
/// </summary>
public sealed class BackpackModule : GameModuleBase
{
    public override string Name => "Backpack";

    public override void Initialize()
    {
        BackpackModel.Singleton.Init();
    }

    public override void RegisterRedDots(IRedDotRegistrar registrar)
    {
        registrar.Register(new BackpackRDInitializer());
    }

    public override void Dispose()
    {
        BackpackModel.Singleton.Dispose();
    }
}
