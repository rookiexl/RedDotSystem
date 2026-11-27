using Game.RedDot;

/// <summary>
/// 分阶段游戏启动使用的业务模块生命周期。
/// </summary>
public interface IGameModule
{
    string Name { get; }
    int StartOrder { get; }
    void Initialize();
    void RegisterRedDots(IRedDotRegistrar registrar);
    void Start();
    void Dispose();
}
