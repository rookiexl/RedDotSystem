using Game.RedDot;
using Game.RedDot.Unity;

using System;

/// <summary>
/// 业务侧红点处理器基类。事件订阅属于游戏业务层，
/// 不属于可复用红点框架。
/// </summary>
public abstract class GameRedDotHandler : RedDotHandler
{
    protected EventComponent mEventComponent = new EventComponent();

    protected override void OnBindEvents()
    {
        AddAllEvents();
    }

    protected override void OnUnbindEvents()
    {
        RemoveAllEvents();
    }

    protected virtual void AddAllEvents()
    {
    }

    protected virtual void RemoveAllEvents()
    {
        mEventComponent.RemoveAllEvents();
    }

    public bool AddEvent(EventId eventID, Action<BaseEvent> eventCB)
    {
        return mEventComponent.AddEvent(eventID, eventCB);
    }

    public bool RemoveEvent(EventId eventID, Action<BaseEvent> eventCB)
    {
        return mEventComponent.RemoveEvent(eventID, eventCB);
    }

    public bool HasEvent(EventId eventID)
    {
        return mEventComponent.HasEvent(eventID);
    }
}
