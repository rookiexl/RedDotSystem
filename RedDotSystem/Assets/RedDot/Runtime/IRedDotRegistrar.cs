namespace Game.RedDot
{
    /// <summary>
    /// 业务模块通过该契约注册自身的红点定义。
    /// </summary>
    public interface IRedDotRegistrar
    {
        bool Register(FuncRDInitializer initializer);
    }
}
