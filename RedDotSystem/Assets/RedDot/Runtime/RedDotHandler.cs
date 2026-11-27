namespace Game.RedDot
{
/*
 * Description:             RedDotHandler.cs
 * Author:                  TONYTANG
 * Create Date:             2026/04/03
 */

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 框架侧红点处理器。只负责红点单元，不感知应用的业务事件系统。
/// </summary>
public abstract class RedDotHandler
{
    public RedDotInfo BindRedDotInfo { get; private set; }

    protected Dictionary<string, string> mBindRedDotUnitMap =
        new Dictionary<string, string>();

    public void Init(RedDotInfo bindRedDotInfo)
    {
        Debug.Assert(bindRedDotInfo != null, "RedDotInfo cannot be null.");
        BindRedDotInfo = bindRedDotInfo;
        InitRedDotDatas();
        OnBindEvents();
    }

    protected abstract void InitRedDotDatas();

    /// <summary>
    /// 供业务侧处理器基类绑定业务事件。
    /// </summary>
    protected virtual void OnBindEvents()
    {
    }

    /// <summary>
    /// 供业务侧处理器基类释放业务事件。
    /// </summary>
    protected virtual void OnUnbindEvents()
    {
    }

    public virtual void Clear()
    {
        OnUnbindEvents();
        ClearRedDotDatas();
        ClearBindRedDotInfo();
    }

    protected virtual void ResetDatas()
    {
        BindRedDotInfo = null;
        mBindRedDotUnitMap.Clear();
    }

    protected void ClearRedDotDatas()
    {
        var redDotUnits = mBindRedDotUnitMap.Keys.ToList();
        foreach (var redDotUnit in redDotUnits)
        {
            RemoveBindRedDotUnit(redDotUnit);
        }
    }

    protected void ClearBindRedDotInfo()
    {
        BindRedDotInfo = null;
    }

    protected bool AddBindRedDotUnit(
        string redDotUnit,
        string redDotUnitDes,
        Func<int> calculateFunc,
        RedDotType redDotType = RedDotType.NUMBER,
        bool isRecursive = true)
    {
        var (result, _) = RedDotModel.Singleton.AddRedDotUnitInfo(
            redDotUnit,
            redDotUnitDes,
            calculateFunc,
            redDotType);
        if (!result)
        {
            Debug.LogError($"Red dot unit:{redDotUnit} already exists.");
            return false;
        }

        mBindRedDotUnitMap.Add(redDotUnit, redDotUnit);
        BindRedDotInfo.AddRedDotUnit(redDotUnit, isRecursive);
        return true;
    }

    protected bool RemoveBindRedDotUnit(string redDotUnit)
    {
        if (!mBindRedDotUnitMap.TryGetValue(redDotUnit, out var redDotUnitValue))
        {
            Debug.LogError($"Red dot unit:{redDotUnit} is not bound by this handler.");
            return false;
        }

        mBindRedDotUnitMap.Remove(redDotUnit);
        BindRedDotInfo.RemoveRedDotUnit(redDotUnitValue);
        RedDotModel.Singleton.RemoveRedDotUnitInfo(redDotUnitValue);
        return true;
    }

    protected bool HasBindRedDotUnit(string redDotUnit)
    {
        return mBindRedDotUnitMap.ContainsKey(redDotUnit);
    }
}
}
