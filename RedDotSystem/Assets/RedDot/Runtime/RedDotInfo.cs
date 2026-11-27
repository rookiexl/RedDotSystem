/*
 * Description:             RedDotInfo.cs
 * Author:                  TONYTANG
 * Create Date:             2022/08/14
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Note:
// 红点单元绑定推荐用红点处理器的方式

/// <summary>
/// RedDotInfo.cs
/// 红点信息类
/// </summary>
namespace Game.RedDot
{
public class RedDotInfo
{
    /// <summary>
    /// 红点名
    /// </summary>
    public string RedDotName
    {
        get;
        private set;
    }

    /// <summary>
    /// 当前红点最终生效的红点单元列表(本地绑定 + 子节点递归汇总)
    /// </summary>
    public List<string> RedDotUnitList
    {
        get;
        private set;
    }

    /// <summary>
    /// 红点描述
    /// </summary>
    public string RedDotDes
    {
        get;
        private set;
    }

    /// <summary>
    /// 红点处理器
    /// </summary>
    public RedDotHandler RedDotHandler
    {
        get;
        private set;
    }

    /// <summary>
    /// 红点名深度
    /// </summary>
    public int RedDotNameDepth
    {
        get;
        private set;
    }

    /// <summary>
    /// 当前红点直接绑定的红点单元信息Map[红点单元名字, 红点单元绑定信息]
    /// </summary>
    protected Dictionary<string, RedDotUnitBindData> mRedDotUnitBindDataMap;

    /// <summary>
    /// 当前红点直接绑定的红点单元信息，供模型统一派生父子汇总关系。
    /// </summary>
    internal Dictionary<string, RedDotUnitBindData> LocalRedDotUnitBindDataMap
    {
        get
        {
            return mRedDotUnitBindDataMap;
        }
    }

    /// <summary>
    /// 临时红点名列表
    /// </summary>
    private List<string> mTempRedDotNameList;

    public RedDotInfo()
    {
        RedDotUnitList = new List<string>();
        mRedDotUnitBindDataMap = new Dictionary<string, RedDotUnitBindData>();
        mTempRedDotNameList = new List<string>();
    }

    /// <summary>
    /// 重置数据
    /// </summary>
    protected virtual void ResetDatas()
    {
        RedDotName = null;
        RedDotDes = null;
        RedDotHandler = null;
        RedDotUnitList.Clear();
        mRedDotUnitBindDataMap.Clear();
        mTempRedDotNameList.Clear();
        RedDotNameDepth = 0;
    }

    /// <summary>
    /// 初始化
    /// </summary>
    /// <param name="redDotName"></param>
    /// <param name="redDotDes"></param>
    public void Init(string redDotName, string redDotDes, RedDotHandler redDotHandler = null)
    {
        RedDotName = redDotName;
        RedDotDes = redDotDes;
        RedDotNameDepth = RedDotUtilities.GetRedDotNameDepth(RedDotName);
        BindRedDotHandler(redDotHandler);
    }

    /// <summary>
    /// 清除所有数据
    /// </summary>
    public void Clear()
    {
        ClearAllRedDotUnits();
        RedDotName = null;
        RedDotDes = null;
        RedDotNameDepth = 0;
    }

    /// <summary>
    /// 绑定红点处理器
    /// </summary>
    /// <param name="redDotHandler"></param>
    /// <returns></returns>
    public bool BindRedDotHandler(RedDotHandler redDotHandler)
    {
        if(redDotHandler == null)
        {
            return false;
        }
        if(RedDotHandler != null)
        {
            Debug.LogError($"红点名:{RedDotName}已绑定红点处理器,请先清理原有红点处理器,绑定失败!");
            return false;
        }
        RedDotHandler = redDotHandler;
        return true;
    }

    /// <summary>
    /// 添加当前红点直接绑定的红点运算单元
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <param name="recusive">是否将本地单元递归汇总到父红点</param>
    /// <param name="useBind">保留的兼容参数；父子关系现在统一由模型派生</param>
    /// <returns></returns>
    public bool AddRedDotUnit(string redDotUnit, bool recusive = true, bool useBind = true)
    {
        if (string.IsNullOrEmpty(redDotUnit))
        {
            Debug.LogError($"红点名:{RedDotName}不能绑定空红点运算单元!");
            RedDotModel.Singleton.RecordRegistrationIssue(
                "RED_DOT_UNIT_BIND_EMPTY",
                $"红点名:{RedDotName}尝试绑定空红点运算单元");
            return false;
        }
        if (mRedDotUnitBindDataMap.ContainsKey(redDotUnit))
        {
            //Debug.LogError($"红点名:{RedDotName}重复添加影响红点运算单元:{redDotUnit.ToString()}，添加失败！");
            RedDotModel.Singleton.RecordRegistrationIssue(
                "RED_DOT_DUPLICATE_BINDING",
                $"红点名:{RedDotName}重复绑定红点运算单元:{redDotUnit}");
            return false;
        }

        var redDotBindData = new RedDotUnitBindData();
        redDotBindData.Init(redDotUnit, recusive);
        mRedDotUnitBindDataMap.Add(redDotUnit, redDotBindData);

        // useBind仅保留API兼容。父子汇总不再在绑定时递归修改父节点，
        // 而是由模型基于所有本地绑定统一重建，因此不再依赖父节点注册顺序。
        RedDotModel.Singleton.RequestRebuildDerivedRedDotData();

        // 新绑定的单元即使已有缓存值，也需要让受影响节点重新生成快照。
        RedDotManager.Singleton.MarkRedDotUnitDirty(redDotUnit);
        ForceRefreshRedDotNameAndParents(recusive);
        return true;
    }

    /// <summary>
    /// 移除影响的红点运算单元
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    public bool RemoveRedDotUnit(string redDotUnit)
    {
        if (!mRedDotUnitBindDataMap.TryGetValue(redDotUnit, out var redDotUnitBindData))
        {
            Debug.LogError($"红点名:{RedDotName}未直接绑定红点运算单元:{redDotUnit}，移除失败!");
            return false;
        }

        var isRecursive = redDotUnitBindData.IsRecursive;
        mRedDotUnitBindDataMap.Remove(redDotUnit);
        RedDotModel.Singleton.RequestRebuildDerivedRedDotData();

        // 删除绑定可能不改变单元缓存值，但会改变当前节点及父节点的有效单元集合。
        ForceRefreshRedDotNameAndParents(isRecursive);
        return true;
    }

    /// <summary>
    /// 清理绑定的红点处理器
    /// </summary>
    private void ClearRedDotHandler()
    {
        if(RedDotHandler != null)
        {
            RedDotHandler.Clear();
            RedDotHandler = null;
        }
    }

    /// <summary>
    /// 清空当前红点直接绑定的运算单元和派生结果。
    /// </summary>
    private void ClearAllRedDotUnits()
    {
        // 因为可能存在通过红点处理器方式绑定和直接通过RedDotInfo绑定红点单元两种方式
        // 优先清除红点处理器，避免重复清除红点运算单元数据
        ClearRedDotHandler();
        mRedDotUnitBindDataMap.Clear();
        RedDotUnitList.Clear();
    }

    /// <summary>
    /// 清空派生生效的红点单元列表。
    /// </summary>
    internal void ClearEffectiveRedDotUnits()
    {
        RedDotUnitList.Clear();
    }

    /// <summary>
    /// 添加一个派生生效的红点单元。
    /// </summary>
    internal bool AddEffectiveRedDotUnit(string redDotUnit)
    {
        if (RedDotUnitList.Contains(redDotUnit))
        {
            return false;
        }
        RedDotUnitList.Add(redDotUnit);
        return true;
    }

    /// <summary>
    /// 强制刷新当前红点及递归绑定时受影响的父红点。
    /// </summary>
    private void ForceRefreshRedDotNameAndParents(bool includeParents)
    {
        RedDotManager.Singleton.ForceRefreshRedDotName(RedDotName);
        if (!includeParents)
        {
            return;
        }

        mTempRedDotNameList.Clear();
        RedDotModel.Singleton.GetParentRedDotNameList(RedDotName, ref mTempRedDotNameList);
        foreach (var parentRedDotName in mTempRedDotNameList)
        {
            RedDotManager.Singleton.ForceRefreshRedDotName(parentRedDotName);
        }
    }
}
}
