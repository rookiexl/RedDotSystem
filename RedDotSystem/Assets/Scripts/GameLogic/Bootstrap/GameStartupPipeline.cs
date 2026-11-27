using Game.RedDot;

/// <summary>
/// 负责分阶段游戏启动：红点 Runtime 创建、模块规则注册、
/// 首次计算和 UI 启动。
/// </summary>
public sealed class GameStartupPipeline
{
    private readonly GameModuleManager mModuleManager;
    private bool mIsStarted;

    public GameStartupPipeline(GameModuleManager moduleManager)
    {
        mModuleManager = moduleManager;
    }

    public void Run()
    {
        if(mIsStarted)
        {
            return;
        }

        InitializeRedDotRuntime();
        mModuleManager.InitializeAll();
        RegisterRedDots();
        EvaluateInitialRedDotState();
        mModuleManager.StartAll();
        mIsStarted = true;
    }

    public void Update()
    {
        if(mIsStarted)
        {
            RedDotManager.Singleton.Update();
        }
    }

    public void Dispose()
    {
        RedDotInitializer.Singleton.Dispose();
        mModuleManager.DisposeAll();
        RedDotManager.Singleton.Dispose();
        RedDotModel.Singleton.Dispose();
        mIsStarted = false;
    }

    private void InitializeRedDotRuntime()
    {
        RedDotManager.Singleton.Init();
        RedDotModel.Singleton.Init();
    }

    private void RegisterRedDots()
    {
        var registrar = RedDotInitializer.Singleton;
        registrar.BeginRegistration();
        mModuleManager.RegisterRedDots(registrar);
        registrar.CompleteRegistration();
    }

    private void EvaluateInitialRedDotState()
    {
        RedDotManager.Singleton.MarkAllRedDotUnitDirty();
        RedDotManager.Singleton.EvaluateAllDirtyNow();
    }
}
