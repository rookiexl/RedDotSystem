using Game.RedDot;
using Game.RedDot.Unity;

using System;

/// <summary>
/// 业务侧动态红点处理器基类，支持游戏事件。
/// </summary>
public abstract class GameDynamicRedDotHandler<T> : DynamicRedDotHandler<T>
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
