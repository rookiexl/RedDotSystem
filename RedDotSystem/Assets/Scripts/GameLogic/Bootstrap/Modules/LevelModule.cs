using Game.RedDot;

/// <summary>
/// 关卡 Model 与红点注册。
/// </summary>
public sealed class LevelModule : GameModuleBase
{
    public override string Name => "Level";

    public override void Initialize()
    {
        LevelModel.Singleton.Init();
    }

    public override void RegisterRedDots(IRedDotRegistrar registrar)
    {
        registrar.Register(new LevelRDInitializer());
    }

    public override void Dispose()
    {
        LevelModel.Singleton.Dispose();
    }
}
