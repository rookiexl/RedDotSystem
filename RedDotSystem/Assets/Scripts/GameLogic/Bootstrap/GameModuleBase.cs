using Game.RedDot;

/// <summary>
/// 供不需要实现全部生命周期阶段的模块继承。
/// </summary>
public abstract class GameModuleBase : IGameModule
{
    public abstract string Name { get; }
    public virtual int StartOrder => 0;

    public virtual void Initialize()
    {
    }

    public virtual void RegisterRedDots(IRedDotRegistrar registrar)
    {
    }

    public virtual void Start()
    {
    }

    public virtual void Dispose()
    {
    }
}
