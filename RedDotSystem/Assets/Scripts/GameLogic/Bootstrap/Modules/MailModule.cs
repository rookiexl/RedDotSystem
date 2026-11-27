using Game.RedDot;

/// <summary>
/// 邮件 Model 与红点注册。
/// </summary>
public sealed class MailModule : GameModuleBase
{
    public override string Name => "Mail";

    public override void Initialize()
    {
        MailModel.Singleton.Init();
    }

    public override void RegisterRedDots(IRedDotRegistrar registrar)
    {
        registrar.Register(new MailRDInitializer());
    }

    public override void Dispose()
    {
        MailModel.Singleton.Dispose();
    }
}
