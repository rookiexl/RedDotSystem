using Game.RedDot;

/// <summary>
/// 注册主界面父红点，并在启动完成后打开主界面。
/// </summary>
public sealed class MainUIModule : GameModuleBase
{
    private readonly MainUI mMainUI;

    public override string Name => "MainUI";
    public override int StartOrder => 1000;

    public MainUIModule(MainUI mainUI)
    {
        mMainUI = mainUI;
    }

    public override void RegisterRedDots(IRedDotRegistrar registrar)
    {
        registrar.Register(new MainUIRDInitializer());
    }

    public override void Start()
    {
        mMainUI.Open();
    }

    public override void Dispose()
    {
        if(mMainUI != null && mMainUI.IsOpen)
        {
            mMainUI.Close();
        }
    }
}
