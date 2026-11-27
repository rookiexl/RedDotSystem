/*
 * Description:             RedDotModel.cs
 * Author:                  TONYTANG
 * Create Date:             2022/08/14
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RedDotModel.cs
/// 红点数据单例类
/// </summary>
namespace Game.RedDot
{
public class RedDotModel
{
    private static RedDotModel sSingleton;

    public static RedDotModel Singleton
    {
        get
        {
            if (sSingleton == null)
            {
                sSingleton = new RedDotModel();
            }
            return sSingleton;
        }
    }

    public bool IsInitCompelte { get; private set; }

    /// <summary>
    /// 红点名分隔符
    /// </summary>
    public const string Separator = "|";

    /// <summary>
    /// 红点运算单元信息Map<红点运算单元名, 红点运算单元信息>
    /// </summary>
    private Dictionary<string, RedDotUnitInfo> mRedDotUnitInfoMap;

    /// <summary>
    /// 红点运算单元结果值Map<红点运算单元, 红点运算单元结果值>
    /// </summary>
    private Dictionary<string, int> mRedDotUnitResultMap;

    /// <summary>
    /// 红点名和红点信息Map<红点名, 红点信息>
    /// </summary>
    private Dictionary<string, RedDotInfo> mRedDotInfoMap;

    /// <summary>
    /// 红点单元和红点名列表Map
    /// </summary>
    private Dictionary<string, List<string>> mRedDotUnitNameMap;

    /// <summary>
    /// 重建派生父子关系时使用的临时父红点名列表。
    /// </summary>
    private List<string> mTempParentRedDotNameList;

    /// <summary>
    /// 重建反向索引时复用的待移除空单元列表。
    /// </summary>
    private List<string> mTempEmptyRedDotUnitNameList;

    /// <summary>
    /// 当前是否处于批量派生关系更新阶段。
    /// </summary>
    private int mDerivedDataBatchDepth;

    /// <summary>
    /// 派生关系是否存在待重建数据。
    /// </summary>
    private bool mDerivedDataDirty;

    /// <summary>
    /// 注册过程中已发现的配置问题，供统一校验报告汇总。
    /// </summary>
    private readonly List<RedDotValidationIssue> mRegistrationIssues;

    /// <summary>
    /// 红点前缀树
    /// </summary>
    public Trie RedDotTrie
    {
        get;
        private set;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public RedDotModel()
    {
        mRedDotUnitInfoMap = new Dictionary<string, RedDotUnitInfo>();
        mRedDotUnitResultMap = new Dictionary<string, int>();
        mRedDotInfoMap = new Dictionary<string, RedDotInfo>();
        mRedDotUnitNameMap = new Dictionary<string, List<string>>();
        mTempParentRedDotNameList = new List<string>();
        mTempEmptyRedDotUnitNameList = new List<string>();
        mDerivedDataBatchDepth = 0;
        mDerivedDataDirty = false;
        mRegistrationIssues = new List<RedDotValidationIssue>();
        RedDotTrie = new Trie(Separator);
    }

    /// <summary>
    /// 响应初始化
    /// </summary>
    public void Init()
    {
        if(IsInitCompelte)
        {
            Debug.LogError("红点数据模型已完成初始化，请勿重复初始化!");
            return;
        }
        IsInitCompelte = true;
        mRegistrationIssues.Clear();
    }

    /// <summary>
    /// 响应释放
    /// </summary>
    public void Dispose()
    {
        mRedDotUnitInfoMap.Clear();
        mRedDotUnitResultMap.Clear();
        mRedDotInfoMap.Clear();
        mRedDotUnitNameMap.Clear();
        mTempParentRedDotNameList.Clear();
        mTempEmptyRedDotUnitNameList.Clear();
        mDerivedDataBatchDepth = 0;
        mDerivedDataDirty = false;
        mRegistrationIssues.Clear();
        RedDotTrie.Dispose();
        IsInitCompelte = false;
    }

    /// <summary>
    /// 添加指定红点信息(不带红点处理器)
    /// 没有独立红点单元信息的红点无需构造红点处理器
    /// </summary>
    /// <param name="redDotName"></param>
    /// <param name="redDotDes"></param>
    /// <returns></returns>
    public (bool, RedDotInfo) AddRedDotInfo(string redDotName, string redDotDes)
    {
        if (mRedDotInfoMap.TryGetValue(redDotName, out var existRedDotInfo))
        {
            Debug.LogError($"已添加红点名:{redDotName}的红点信息,请勿重复添加,添加失败!");
            RecordRegistrationIssue(
                "RED_DOT_DUPLICATE_NODE",
                $"重复注册红点名:{redDotName}");
            return (false, existRedDotInfo);
        }
        var redDotInfo = new RedDotInfo();
        redDotInfo.Init(redDotName, redDotDes);
        mRedDotInfoMap.Add(redDotName, redDotInfo);
        RedDotTrie.AddWord(redDotName);
        RequestRebuildDerivedRedDotData();
        RedDotManager.Singleton.ForceRefreshRedDotName(redDotName);
        return (true, redDotInfo);
    }

    /// <summary>
    /// 添加指定红点信息(带红点处理器)
    /// 有独立红点单元信息的红点需要构造红点处理器
    /// </summary>
    /// <param name="redDotName"></param>
    /// <param name="redDotDes"></param>
    /// <returns></returns>
    public (bool, RedDotInfo) AddRedDotInfo<T>(string redDotName, string redDotDes) where T : RedDotHandler, new()
    {
        if (mRedDotInfoMap.TryGetValue(redDotName, out var existRedDotInfo))
        {
            Debug.LogError($"已添加红点名:{redDotName}的红点信息,请勿重复添加,添加失败!");
            RecordRegistrationIssue(
                "RED_DOT_DUPLICATE_NODE",
                $"重复注册红点名:{redDotName}");
            return (false, existRedDotInfo);
        }
        var redDotHandler = new T();
        var redDotInfo = new RedDotInfo();
        redDotInfo.Init(redDotName, redDotDes, redDotHandler);
        // 在红点处理器初始化之前必须先将红点信息添加进去
        // 不然后续可能找不到当前红点信息
        mRedDotInfoMap.Add(redDotName, redDotInfo);
        RedDotTrie.AddWord(redDotName);
        RequestRebuildDerivedRedDotData();
        RedDotManager.Singleton.ForceRefreshRedDotName(redDotName);
        redDotHandler.Init(redDotInfo);
        return (true, redDotInfo);
    }

    /// <summary>
    /// 添加指定动态红点信息(带红点处理器，带动态参数)
    /// 有独立红点单元信息的红点需要构造红点处理器
    /// </summary>
    /// <param name="redDotName"></param>
    /// <param name="redDotDes"></param>
    /// <param name="dynamicData"></param>
    /// <returns></returns>
    public (bool, RedDotInfo) AddDynamicRedDotInfo<T, W>(string redDotName, string redDotDes, W dynamicData) where T : DynamicRedDotHandler<W>, new()
    {
        if (mRedDotInfoMap.TryGetValue(redDotName, out var existRedDotInfo))
        {
            Debug.LogError($"已添加红点名:{redDotName}的红点信息,请勿重复添加,添加失败!");
            RecordRegistrationIssue(
                "RED_DOT_DUPLICATE_NODE",
                $"重复注册红点名:{redDotName}");
            return (false, existRedDotInfo);
        }
        var redDotHandler = new T();
        var redDotInfo = new RedDotInfo();
        redDotInfo.Init(redDotName, redDotDes, redDotHandler);
        redDotHandler.Data = dynamicData;
        // 在红点处理器初始化之前必须先将红点信息添加进去
        // 不然后续可能找不到当前红点信息
        mRedDotInfoMap.Add(redDotName, redDotInfo);
        RedDotTrie.AddWord(redDotName);
        RequestRebuildDerivedRedDotData();
        RedDotManager.Singleton.ForceRefreshRedDotName(redDotName);
        redDotHandler.Init(redDotInfo);

        return (true, redDotInfo);
    }

    /// <summary>
    /// 移除指定红点名信息
    /// </summary>
    /// <param name="redDotName"></param>
    /// <returns></returns>
    public bool RemoveRedDotInfo(string redDotName)
    {
        if(!mRedDotInfoMap.TryGetValue(redDotName, out var existRedDotInfo))
        {
            Debug.LogError($"找不到红点名:{redDotName}的红点名信息，移除失败！");
            return false;
        }
        ForceRefreshRedDotNameAndParents(redDotName);
        existRedDotInfo.Clear();
        mRedDotInfoMap.Remove(redDotName);
        RedDotTrie.RemoveWord(redDotName);
        RequestRebuildDerivedRedDotData();
        return true;
    }

    /// <summary>
    /// 进入批量派生关系更新阶段。
    /// </summary>
    internal void BeginDerivedDataBatch()
    {
        mDerivedDataBatchDepth++;
    }

    /// <summary>
    /// 结束批量派生关系更新阶段，并在最外层结束时统一重建。
    /// </summary>
    internal void EndDerivedDataBatch()
    {
        if(mDerivedDataBatchDepth <= 0)
        {
            return;
        }

        mDerivedDataBatchDepth--;
        if(mDerivedDataBatchDepth == 0 && mDerivedDataDirty)
        {
            RebuildDerivedRedDotData();
        }
    }

    /// <summary>
    /// 标记派生关系需要重建；批量更新阶段只记录脏标记。
    /// </summary>
    internal void RequestRebuildDerivedRedDotData()
    {
        mDerivedDataDirty = true;
        if(mDerivedDataBatchDepth > 0)
        {
            return;
        }
        RebuildDerivedRedDotData();
    }

    /// <summary>
    /// 清空注册阶段累计的配置问题。
    /// </summary>
    internal void ClearValidationIssues()
    {
        mRegistrationIssues.Clear();
    }

    /// <summary>
    /// 记录注册阶段已发现的配置问题。
    /// </summary>
    internal void RecordRegistrationIssue(
        string code,
        string message,
        RedDotValidationSeverity severity = RedDotValidationSeverity.Error)
    {
        mRegistrationIssues.Add(new RedDotValidationIssue(severity, code, message));
    }

    /// <summary>
    /// 校验当前红点定义图，并返回完整报告。
    /// Note:
    /// 注册流程应在 CompleteRegistration() 完成父子关系派生后调用本方法。
    /// </summary>
    public RedDotValidationReport Validate()
    {
        var report = new RedDotValidationReport();
        report.AddRange(mRegistrationIssues);
        ValidateRedDotNames(report);
        ValidateRedDotUnits(report);
        return report;
    }

    /// <summary>
    /// 校验红点名层级、父节点和空叶子节点。
    /// </summary>
    private void ValidateRedDotNames(RedDotValidationReport report)
    {
        var missingParentNames = new HashSet<string>(StringComparer.Ordinal);
        foreach(var redDotInfoEntry in mRedDotInfoMap)
        {
            var redDotName = redDotInfoEntry.Key;
            if(!ValidateRedDotNameSegments(redDotName, report))
            {
                continue;
            }

            var segments = redDotName.Split(
                new[] { Separator },
                StringSplitOptions.None);
            var prefix = string.Empty;
            for(var index = 0; index < segments.Length - 1; index++)
            {
                prefix = index == 0
                    ? segments[index]
                    : $"{prefix}{Separator}{segments[index]}";
                if(!mRedDotInfoMap.ContainsKey(prefix) &&
                   missingParentNames.Add(prefix))
                {
                    report.Add(
                        RedDotValidationSeverity.Error,
                        "RED_DOT_PARENT_MISSING",
                        $"红点名:{redDotName}缺少父节点:{prefix}");
                }
            }

            var trieNode = RedDotTrie.GetWordNode(redDotName);
            var hasChild = trieNode != null && trieNode.ChildCount > 0;

            if(!hasChild && redDotInfoEntry.Value.RedDotUnitList.Count == 0)
            {
                report.Add(
                    RedDotValidationSeverity.Warning,
                    "RED_DOT_EMPTY_LEAF",
                    $"红点名:{redDotName}没有绑定运算单元，也没有子节点");
            }
        }
    }

    /// <summary>
    /// 校验单个红点名的分段格式。
    /// </summary>
    private bool ValidateRedDotNameSegments(
        string redDotName,
        RedDotValidationReport report)
    {
        if(string.IsNullOrWhiteSpace(redDotName))
        {
            report.Add(
                RedDotValidationSeverity.Error,
                "RED_DOT_NAME_EMPTY",
                "红点名不能为空");
            return false;
        }

        var hasInvalidSegment = false;
        var segments = redDotName.Split(
            new[] { Separator },
            StringSplitOptions.None);
        foreach(var segment in segments)
        {
            if(!string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }
            hasInvalidSegment = true;
            report.Add(
                RedDotValidationSeverity.Error,
                "RED_DOT_NAME_EMPTY_SEGMENT",
                $"红点名:{redDotName}包含空层级");
        }
        return !hasInvalidSegment;
    }

    /// <summary>
    /// 校验运算单元定义、引用和未使用项。
    /// </summary>
    private void ValidateRedDotUnits(RedDotValidationReport report)
    {
        foreach(var redDotUnitInfoEntry in mRedDotUnitInfoMap)
        {
            var redDotUnit = redDotUnitInfoEntry.Key;
            if(string.IsNullOrWhiteSpace(redDotUnit))
            {
                report.Add(
                    RedDotValidationSeverity.Error,
                    "RED_DOT_UNIT_NAME_EMPTY",
                    "红点运算单元名不能为空");
            }

            if(redDotUnitInfoEntry.Value.RedDotUnitCalculateFunc == null)
            {
                report.Add(
                    RedDotValidationSeverity.Error,
                    "RED_DOT_UNIT_FUNC_MISSING",
                    $"红点运算单元:{redDotUnit}未绑定计算函数");
            }

            if(!mRedDotUnitNameMap.TryGetValue(redDotUnit, out var redDotNames) ||
               redDotNames.Count == 0)
            {
                report.Add(
                    RedDotValidationSeverity.Warning,
                    "RED_DOT_UNIT_UNUSED",
                    $"红点运算单元:{redDotUnit}未被任何红点名使用");
            }
        }

        foreach(var redDotInfoEntry in mRedDotInfoMap)
        {
            foreach(var redDotUnit in redDotInfoEntry.Value.RedDotUnitList)
            {
                if(mRedDotUnitInfoMap.ContainsKey(redDotUnit))
                {
                    continue;
                }
                report.Add(
                    RedDotValidationSeverity.Error,
                    "RED_DOT_UNIT_REFERENCE_MISSING",
                    $"红点名:{redDotInfoEntry.Key}引用了未定义的红点运算单元:{redDotUnit}");
            }
        }
    }

    /// <summary>
    /// 根据所有红点名的本地单元绑定，重新构建父级有效单元集合和反向索引。
    /// Note:
    /// 子节点可以先于父节点注册。父节点后续注册时，再次执行本方法即可补齐汇总关系。
    /// </summary>
    internal void RebuildDerivedRedDotData()
    {
        mDerivedDataDirty = false;
        foreach(var redDotInfo in mRedDotInfoMap.Values)
        {
            redDotInfo.ClearEffectiveRedDotUnits();
        }

        foreach(var redDotInfo in mRedDotInfoMap.Values)
        {
            var parentNamesLoaded = false;
            foreach(var redDotUnitBindData in redDotInfo.LocalRedDotUnitBindDataMap.Values)
            {
                var redDotUnit = redDotUnitBindData.RedDotUnit;
                redDotInfo.AddEffectiveRedDotUnit(redDotUnit);
                if(!redDotUnitBindData.IsRecursive)
                {
                    continue;
                }

                if(!parentNamesLoaded)
                {
                    mTempParentRedDotNameList.Clear();
                    GetParentRedDotNameList(redDotName: redDotInfo.RedDotName,
                        parentRedDotNameList: ref mTempParentRedDotNameList);
                    parentNamesLoaded = true;
                }
                foreach(var parentRedDotName in mTempParentRedDotNameList)
                {
                    var parentRedDotInfo = GetRedDotInfoByName(parentRedDotName);
                    parentRedDotInfo?.AddEffectiveRedDotUnit(redDotUnit);
                }
            }
        }

        foreach(var redDotNames in mRedDotUnitNameMap.Values)
        {
            redDotNames.Clear();
        }
        foreach(var redDotInfo in mRedDotInfoMap.Values)
        {
            foreach(var redDotUnit in redDotInfo.RedDotUnitList)
            {
                AddRedDotUnitAndName(redDotUnit, redDotInfo.RedDotName);
            }
        }

        mTempEmptyRedDotUnitNameList.Clear();
        foreach(var redDotUnitNameEntry in mRedDotUnitNameMap)
        {
            if(redDotUnitNameEntry.Value.Count == 0)
            {
                mTempEmptyRedDotUnitNameList.Add(redDotUnitNameEntry.Key);
            }
        }
        foreach(var emptyRedDotUnitName in mTempEmptyRedDotUnitNameList)
        {
            mRedDotUnitNameMap.Remove(emptyRedDotUnitName);
        }
    }

    /// <summary>
    /// 强制刷新指定红点名及其所有已注册父红点。
    /// </summary>
    private void ForceRefreshRedDotNameAndParents(string redDotName)
    {
        RedDotManager.Singleton.ForceRefreshRedDotName(redDotName);
        mTempParentRedDotNameList.Clear();
        GetParentRedDotNameList(redDotName, ref mTempParentRedDotNameList);
        foreach(var parentRedDotName in mTempParentRedDotNameList)
        {
            RedDotManager.Singleton.ForceRefreshRedDotName(parentRedDotName);
        }
    }

    /// <summary>
    /// 添加红点运算单元信息
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <param name="redDotUnitDes"></param>
    /// <param name="caculateFunc"></param>
    /// <param name="redDotType"></param>
    /// <returns></returns>
    public (bool, RedDotUnitInfo) AddRedDotUnitInfo(string redDotUnit, string redDotUnitDes, Func<int> caculateFunc, RedDotType redDotType = RedDotType.NUMBER)
    {
        if(mRedDotUnitInfoMap.TryGetValue(redDotUnit, out var redDotUnitInfo))
        {
            Debug.LogError($"已添加红点运算单元:{redDotUnit}的红点运算单元信息,请勿重复添加,添加失败!");
            RecordRegistrationIssue(
                "RED_DOT_DUPLICATE_UNIT",
                $"重复注册红点运算单元:{redDotUnit}");
            return (false, redDotUnitInfo);
        }
        redDotUnitInfo = new RedDotUnitInfo();
        redDotUnitInfo.Init(redDotUnit, redDotUnitDes, caculateFunc, redDotType);
        mRedDotUnitInfoMap.Add(redDotUnit, redDotUnitInfo);
        return (true, redDotUnitInfo);
    }

    /// <summary>
    /// 清除指定红点运算单元信息
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    public bool RemoveRedDotUnitInfo(string redDotUnit)
    {
        if(!mRedDotUnitInfoMap.TryGetValue(redDotUnit, out var redDotUnitInfo))
        {
            Debug.LogError($"找不到红点运算单元:{redDotUnit}信息,删除失败!");
            return false;
        }
        redDotUnitInfo.Clear();
        mRedDotUnitInfoMap.Remove(redDotUnit);
        mRedDotUnitResultMap.Remove(redDotUnit);
        return true;
    }

    /// <summary>
    /// 是否包含指定红点运算单元信息
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    public bool ExistsRedDotUnitInfo(string redDotUnit)
    {
        return mRedDotUnitInfoMap.ContainsKey(redDotUnit);
    }

    /// <summary>
    /// 获取红点名和红点信息Map<红点名, 红点信息>
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, RedDotInfo> GetRedDotInfoMap()
    {
        return mRedDotInfoMap;
    }

    /// <summary>
    /// 获取红点运算单元信息Map<红点运算单元门, 红点运算单元信息>
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, RedDotUnitInfo> GetRedDotUnitInfoMap()
    {
        return mRedDotUnitInfoMap;
    }

    /// <summary>
    /// 获取指定红点名的红点信息
    /// </summary>
    /// <param name="redDotName"></param>
    /// <returns></returns>
    public RedDotInfo GetRedDotInfoByName(string redDotName)
    {
        RedDotInfo redDotInfo;
        if(!mRedDotInfoMap.TryGetValue(redDotName, out redDotInfo))
        {
            //Debug.LogError($"找不到红点名:{redDotName}的红点信息!");
        }
        return redDotInfo;
    }

    /// <summary>
    /// 获取指定红点运算单元的红点运算单元信息
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    public RedDotUnitInfo GetRedDotUnitInfo(string redDotUnit)
    {
        RedDotUnitInfo redDotUnitInfo;
        if(!mRedDotUnitInfoMap.TryGetValue(redDotUnit, out redDotUnitInfo))
        {
            Debug.LogError($"找不到红点运算单元:{redDotUnit.ToString()}的信息!");
        }
        return redDotUnitInfo;
    }

    /// <summary>
    /// 获取指定红点运算单元的计算委托
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    public Func<int> GetRedDotUnitFunc(string redDotUnit)
    {
        RedDotUnitInfo redDotUnitInfo = GetRedDotUnitInfo(redDotUnit);
        return redDotUnitInfo?.RedDotUnitCalculateFunc;
    }

    /// <summary>
    /// 获取指定红点运算单元的红点类型
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    public RedDotType GetRedDotUnitRedType(string redDotUnit)
    {
        RedDotUnitInfo redDotUnitInfo = GetRedDotUnitInfo(redDotUnit);
        if(redDotUnitInfo != null)
        {
            return redDotUnitInfo.RedDotType;
        }
        Debug.LogError($"获取红点运算单元:{redDotUnit}的红点类型失败!");
        return RedDotType.NONE;
    }

    /// <summary>
    /// 获取指定红点名的所有红点运算单元列表
    /// </summary>
    /// <param name="redDotName"></param>
    /// <returns></returns>
    public List<string> GetRedDotUnitsByName(string redDotName)
    {
        var redDotInfo = GetRedDotInfoByName(redDotName);
        return redDotInfo != null ? redDotInfo.RedDotUnitList : null;
    }

    /// <summary>
    /// 获取指定红点运算单元影响的所有红点名列表
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    public List<string> GetRedDotNamesByUnit(string redDotUnit)
    {
        List<string> redDotNames;
        if(!mRedDotUnitNameMap.TryGetValue(redDotUnit, out redDotNames))
        {
            Debug.LogError($"找不到红点运算单元:{redDotUnit}的影响红点名信息，获取失败！");
        }
        return redDotNames;
    }

    /// <summary>
    /// 添加指定红点运算单元影响的指定红点名
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <param name="redDotName"></param>
    /// <returns></returns>
    public bool AddRedDotUnitAndName(string redDotUnit, string redDotName)
    {
        if(!mRedDotUnitNameMap.TryGetValue(redDotUnit, out var redDotNames))
        {
            redDotNames = new List<string>();
            mRedDotUnitNameMap.Add(redDotUnit, redDotNames);
        }
        if(redDotNames.Contains(redDotName))
        {
           return false; 
        }
        redDotNames.Add(redDotName);
        return true;
    }

    /// <summary>
    /// 移除指定红点运算单元影响的指定红点名
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <param name="redDotName"></param>
    /// <returns></returns>
    public bool RemoveRedDotUnitAndName(string redDotUnit, string redDotName)
    {
        if(!mRedDotUnitNameMap.TryGetValue(redDotUnit, out var redDotNames))
        {
            Debug.LogError($"找不到红点运算单元:{redDotUnit}的影响的红点名数据，移除红点名:{redDotName}失败！");
            return false;
        }
        var result = redDotNames.Remove(redDotName);
        if(!result)
        {
            Debug.LogError($"找不到红点运算单元:{redDotUnit}影响的红点名:{redDotName}，移除失败！");
            return false;            
        }
        return result;
    }

    /// <summary>
    /// 获取指定红点运算单元的运算结果
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    public int GetRedDotUnitResult(string redDotUnit)
    {
        int result = 0;
        if(!mRedDotUnitResultMap.TryGetValue(redDotUnit, out result))
        {
        }
        return result;
    }

    /// <summary>
    /// 设置指定红点运算单元的运算结果
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    public bool SetRedDotUnitResult(string redDotUnit, int result)
    {
        if(!mRedDotUnitResultMap.ContainsKey(redDotUnit))
        {
            mRedDotUnitResultMap.Add(redDotUnit, result);
            return true;
        }
        mRedDotUnitResultMap[redDotUnit] = result;
        return true;
    }

    /// <summary>
    /// 判断指定红点名是否存在
    /// </summary>
    /// <param name="redDotName"></param>
    /// <returns></returns>
    public bool IsRedDotNameExist(string redDotName)
    {
        return mRedDotInfoMap.ContainsKey(redDotName);
    }

    /// <summary>
    /// 判断指定红点运算单元是否存在
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    public bool IsRedDotUnitExist(string redDotUnit)
    {
        return mRedDotUnitInfoMap.ContainsKey(redDotUnit);
    }

    #region 前缀树相关
    /// <summary>
    /// 获取指定红点名的父红点名列表(不含自身)
    /// </summary>
    /// <param name="redDotName"></param>
    /// <param name="parentRedDotNameList"></param>
    public void GetParentRedDotNameList(string redDotName, ref List<string> parentRedDotNameList)
    {
        RedDotTrie.GetParentWordList(redDotName, ref parentRedDotNameList);
    }
    #endregion
}
}
