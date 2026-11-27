using Game.RedDot;
using Game.RedDot.Unity;

/*
 * Description:             BaseUI.cs
 * Author:                  TONYTANG
 * Create Date:             2026/07/09
 */

using System;
using UnityEngine;

/// <summary>
/// 通用 UI 生命周期。红点订阅由各视图自身持有，不属于 BaseUI。
/// </summary>
public abstract class BaseUI : MonoBehaviour
{
    public bool IsOpen { get; private set; }

    protected EventComponent mEventComponent = new EventComponent();

    public virtual void Open(params object[] args)
    {
        if (IsOpen)
        {
            Debug.LogError($"UI:{gameObject.name} is already open.");
            return;
        }

        IsOpen = true;
        gameObject.SetActive(true);
        InitDatas(args);
        RemoveAllListeners();
        AddAllListeners();
        AddAllEvents();
        OnOpen();
    }

    protected virtual void InitDatas(params object[] args)
    {
    }

    protected virtual void AddAllListeners()
    {
    }

    protected virtual void AddAllEvents()
    {
    }

    protected virtual void OnOpen()
    {
    }

    public virtual void Close()
    {
        if (!IsOpen)
        {
            Debug.LogError($"UI:{gameObject.name} is already closed.");
            return;
        }

        IsOpen = false;
        gameObject.SetActive(false);
        RemoveAllListeners();
        RemoveAllEvents();
        OnClose();
    }

    protected virtual void RemoveAllListeners()
    {
    }

    protected virtual void RemoveAllEvents()
    {
        mEventComponent.RemoveAllEvents();
    }

    protected virtual void OnClose()
    {
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
