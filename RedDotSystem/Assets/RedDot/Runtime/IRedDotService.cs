namespace Game.RedDot
{
using System;

/// <summary>
/// 供视图和其他表现层代码使用的红点公共接口。
/// </summary>
public interface IRedDotService
{
    bool IsReady { get; }
    RedDotSnapshot GetSnapshot(string redDotName);
    IDisposable Subscribe(string redDotName, Action<RedDotSnapshot> onChanged);
    bool RequestRefresh(string redDotName);
}
}
