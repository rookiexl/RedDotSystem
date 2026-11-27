namespace Game.RedDot
{
/// <summary>
/// 红点消费者的统一入口。测试可以替换 Provider，
/// 使视图代码不依赖具体单例实现。
/// </summary>
public static class RedDotService
{
    private static IRedDotService sProvider = RedDotManager.Singleton;

    public static IRedDotService Provider => sProvider;

    public static void SetProvider(IRedDotService provider)
    {
        sProvider = provider ?? RedDotManager.Singleton;
    }

    public static void ResetProvider()
    {
        sProvider = RedDotManager.Singleton;
    }
}
}
