using Game.RedDot;

/// <summary>
/// PVE Model 与红点注册。
/// </summary>
public sealed class PVEModule : GameModuleBase
{
    public override string Name => "PVE";

    public override void Initialize()
    {
        PVEModel.Singleton.Init();
    }

    public override void RegisterRedDots(IRedDotRegistrar registrar)
    {
        registrar.Register(new PVERDInitializer());
    }

    public override void Dispose()
    {
        PVEModel.Singleton.Dispose();
    }
}
