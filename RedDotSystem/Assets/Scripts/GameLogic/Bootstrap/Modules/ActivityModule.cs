/// <summary>
/// 活动 Model 的生命周期。动态活动红点由 ActivityData
/// 在活动创建时注册。
/// </summary>
public sealed class ActivityModule : GameModuleBase
{
    public override string Name => "Activity";

    public override void Initialize()
    {
        ActivityModel.Singleton.Init();
    }

    public override void Dispose()
    {
        ActivityModel.Singleton.Dispose();
    }
}
