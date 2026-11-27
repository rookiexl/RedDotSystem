/// <summary>
/// 负责系统解锁数据。必须在红点规则激活前完成初始化。
/// </summary>
public sealed class SystemUnlockModule : GameModuleBase
{
    public override string Name => "SystemUnlock";

    public override void Initialize()
    {
        SystemUnlockModel.Singleton.Init();
    }

    public override void Dispose()
    {
        SystemUnlockModel.Singleton.Dispose();
    }
}
