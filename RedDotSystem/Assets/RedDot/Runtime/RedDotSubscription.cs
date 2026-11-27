namespace Game.RedDot
{
using System;

/// <summary>
/// 由视图持有、可重复释放的订阅句柄。
/// </summary>
internal sealed class RedDotSubscription : IDisposable
{
    private Action mDisposeAction;

    public RedDotSubscription(Action disposeAction)
    {
        mDisposeAction = disposeAction;
    }

    public void Dispose()
    {
        if (mDisposeAction == null)
        {
            return;
        }

        var disposeAction = mDisposeAction;
        mDisposeAction = null;
        disposeAction.Invoke();
    }
}
}
