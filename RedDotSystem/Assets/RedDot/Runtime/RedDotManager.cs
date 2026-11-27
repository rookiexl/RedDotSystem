/*
 * Description:             RedDotManager.cs
 * Author:                  TONYTANG
 * Create Date:             2022/08/14
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 红点系统主要设计：
// 1. 采用前缀树数据结构，从红点命名上解决红点父子定义关联问题
// 2. 红点运算单元采用最小单元化定义，采用组合定义方式组装红点影响因素，从而实现高度自由的红点运算逻辑组装
// 3. 红点运算单元作为红点影响显示的最小单元，每一个都会对应逻辑层面的一个计算代码，从而实现和逻辑层关联实现自定义解锁和计算方式
// 4. 红点运算结果按红点运算单元为单位，采用标脏加延迟计算的方式避免重复运算和结果缓存
// 5. 红点运算单元支持多种显示类型定义(e.g. 1. 纯红点 2. 纯数字红点 3. 新红点 4. 混合红点等)，红点最终显示类型由所有影响他的红点运算单元计算结果组合而成(e.g. 红点运算单元1(新红点类型)+红点运算单元2(数字红点类型)=新红点类型)

// 静态红点支持设计:
// 1. Static red dot IDs are supplied by the application layer.

// 动态红点支持设计：
// 1. 动态红点通过自定义取名的方式定义动态红点名和动态红点运算单元名注册到RedDotModel里去实现动态红点逻辑支持

/// <summary>
/// RedDotManager.cs
/// 红点管理单例类
/// </summary>
namespace Game.RedDot
{
public class RedDotManager : IRedDotService
{
    private static RedDotManager sSingleton;

    public static RedDotManager Singleton
    {
        get
        {
            if (sSingleton == null)
            {
                sSingleton = new RedDotManager();
            }
            return sSingleton;
        }
    }

    /// <summary>
    /// 每次 CheckDirtyRedDotUnit() 最多处理的运算单元数量。
    /// Note:
    /// 名称沿用历史命名，实际不是每个 Unity 帧都会检查，
    /// 而是 Update() 每累计 DIRTY_UPDATE_INTERVAL_FRAME 帧才处理一批。
    /// </summary>
    private const int CALCULATE_RED_DOT_UNIT_NUM_PER_FRAME = 30;

    /// <summary>
    /// 待计算的运算单元集合<红点运算单元>。
    /// Note:
    /// 业务事件只负责把单元加入这里，不会立即执行计算。
    /// </summary>
    private HashSet<string> mDirtyRedDotUnitSet;

    /// <summary>
    /// 强制刷新红点名集合<红点名>。
    /// Note:
    /// 用于动态节点增删、UI 主动请求刷新等场景：
    /// 即使运算单元结果没有变化，也必须通知该红点的订阅者。
    /// </summary>
    private HashSet<string> mForceRefreshRedDotNameSet;

    /// <summary>
    /// 当前批次等待计算的运算单元集合。
    /// Note:
    /// 每批最多取 CALCULATE_RED_DOT_UNIT_NUM_PER_FRAME 个单元。
    /// 先复制到临时集合并从原脏集合移除，再执行计算，
    /// 避免计算期间重新标脏同一个单元时修改原集合。
    /// </summary>
    private HashSet<string> mWaitCalculateRedDotUnitSet;

    /// <summary>
    /// 当前批次需要通知的红点名集合<红点名>。
    /// Note:
    /// 多个变化单元可能影响同一个红点名，使用 HashSet 负责去重。
    /// </summary>
    private HashSet<string> mNeedRefreshRedDotNameSet;

    /// <summary>
    /// 按红点名索引的视图订阅者。
    /// </summary>
    private Dictionary<string, Action<RedDotSnapshot>[]> mRedDotNameSubscriberMap;

    /// <summary>
    /// 当前批次的运算单元结果变化标记Map<红点运算单元，结果是否变化>。
    /// Note:
    /// 这里缓存的是“是否变化”，不是红点数值本身。
    /// 每次 CheckDirtyRedDotUnit() 开始时都会清空。
    /// </summary>
    private Dictionary<string, bool> mCaculatedRedDotUnitResultChangeMap;

    /// <summary>
    /// 标脏检测的调度间隔。
    /// Note:
    /// 每累计 10 帧才执行一次 CheckDirtyRedDotUnit()。
    /// 因此一个批次最多处理 30 个单元，而不是每个 Unity 帧处理 30 个。
    /// </summary>
    private const int DIRTY_UPDATE_INTERVAL_FRAME = 10;

    /// <summary>
    /// 经历的帧数
    /// </summary>
    private int mFramePassed;

    public RedDotManager()
    {
        mDirtyRedDotUnitSet = new HashSet<string>();
        mForceRefreshRedDotNameSet = new HashSet<string>();
        mWaitCalculateRedDotUnitSet = new HashSet<string>();
        mCaculatedRedDotUnitResultChangeMap = new Dictionary<string, bool>();
        mNeedRefreshRedDotNameSet = new HashSet<string>();
        mRedDotNameSubscriberMap =
            new Dictionary<string, Action<RedDotSnapshot>[]>();
        mFramePassed = 0;
    }

    /// <summary>
    /// 初始化
    /// </summary>
    public void Init()
    {

    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        mDirtyRedDotUnitSet.Clear();
        mForceRefreshRedDotNameSet.Clear();
        mWaitCalculateRedDotUnitSet.Clear();
        mNeedRefreshRedDotNameSet.Clear();
        mCaculatedRedDotUnitResultChangeMap.Clear();
        mRedDotNameSubscriberMap.Clear();
        mFramePassed = 0;
    }

    /// <summary>
    /// 标脏所有红点名运算单元运算
    /// Note:
    /// 进入游戏后标脏一次所有,确保触发所有红点运算单元运算
    /// </summary>
    public void MarkAllRedDotUnitDirty()
    {
        var redDotUnitInfoMap = RedDotModel.Singleton.GetRedDotUnitInfoMap();
        foreach(var redDotUnitInfo in redDotUnitInfoMap)
        {
            MarkRedDotUnitDirty(redDotUnitInfo.Key);
        }
    }

    /// <summary>
    /// 立即汇总指定红点的当前缓存结果，并通知其订阅者。
    /// Note:
    /// 此函数不会重新计算运算单元，只负责生成快照和推送。
    /// 它由 CheckDirtyRedDotUnit() 在单元结果变化或强制刷新时调用。
    /// </summary>
    /// <param name="redDotName"></param>
    public bool TriggerRedDotNameUpdate(string redDotName)
    {
        if(!RedDotModel.Singleton.IsRedDotNameExist(redDotName))
        {
            Debug.LogError($"触发红点名:{redDotName}刷新回调失败!");
            return false;
        }
        // 快照 = 汇总该节点所有运算单元的已有缓存结果。
        // 不在这一步重新调用各单元的计算函数。
        NotifyRedDotNameSubscribers(GetSnapshot(redDotName));
        return true;
    }

    /// <summary>
    /// 汇总指定红点名下所有运算单元的当前缓存结果。
    /// Note:
    /// 父节点在绑定运算单元时已经递归接收了子节点的单元，
    /// 所以这里直接求和即可，父节点不需要单独维护一套计算公式。
    /// 只有结果大于 0 的单元才参与显示类型的组合。
    /// </summary>
    /// <param name="redDotName"></param>
    /// <returns></returns>
    public (int result, RedDotType redDotType) GetRedDotNameResult(string redDotName)
    {
        (int result, RedDotType redDotType) redDotNameResult = (0, RedDotType.NONE);
        var redDotUnitList = RedDotModel.Singleton.GetRedDotUnitsByName(redDotName);
        if (redDotUnitList != null)
        {
            var result = 0;
            var redDotType = RedDotType.NONE;
            foreach (var redDotUnit in redDotUnitList)
            {
                var redDotUnitResult = RedDotModel.Singleton.GetRedDotUnitResult(redDotUnit);
                if (redDotUnitResult > 0)
                {
                    var redDotType2 = RedDotModel.Singleton.GetRedDotUnitRedType(redDotUnit);
                    redDotType = redDotType | redDotType2;
                }
                result += redDotUnitResult;
            }
            redDotNameResult.result = result;
            redDotNameResult.redDotType = redDotType;
        }
        return redDotNameResult;
    }

    /// <summary>
    /// 红点初始化是否已完成。
    /// </summary>
    public bool IsReady => RedDotModel.Singleton.IsInitCompelte;

    /// <summary>
    /// 获取最新的不可变红点快照。
    /// </summary>
    public RedDotSnapshot GetSnapshot(string redDotName)
    {
        if (string.IsNullOrEmpty(redDotName) ||
            !RedDotModel.Singleton.IsRedDotNameExist(redDotName))
        {
            return RedDotSnapshot.Empty;
        }

        var redDotNameResult = GetRedDotNameResult(redDotName);
        return new RedDotSnapshot(redDotName, redDotNameResult.result, redDotNameResult.redDotType);
    }

    /// <summary>
    /// 订阅红点，视图无需感知具体 Manager 实现。
    /// </summary>
    public IDisposable Subscribe(string redDotName, Action<RedDotSnapshot> onChanged)
    {
        if (string.IsNullOrEmpty(redDotName))
        {
            return new RedDotSubscription(null);
        }
        if (onChanged == null)
        {
            throw new ArgumentNullException(nameof(onChanged));
        }

        mRedDotNameSubscriberMap.TryGetValue(redDotName, out var subscribers);
        var subscriberCount = subscribers != null ? subscribers.Length : 0;
        var newSubscribers = new Action<RedDotSnapshot>[subscriberCount + 1];
        if(subscriberCount > 0)
        {
            Array.Copy(subscribers, newSubscribers, subscriberCount);
        }
        newSubscribers[subscriberCount] = onChanged;
        mRedDotNameSubscriberMap[redDotName] = newSubscribers;
        return new RedDotSubscription(() => Unsubscribe(redDotName, onChanged));
    }

    /// <summary>
    /// 视图显示时请求刷新红点状态。
    /// </summary>
    public bool RequestRefresh(string redDotName)
    {
        if (string.IsNullOrEmpty(redDotName) ||
            !RedDotModel.Singleton.IsRedDotNameExist(redDotName))
        {
            return false;
        }

        ForceRefreshRedDotName(redDotName);
        return true;
    }

    private void Unsubscribe(string redDotName, Action<RedDotSnapshot> onChanged)
    {
        if (!mRedDotNameSubscriberMap.TryGetValue(redDotName, out var subscribers))
        {
            return;
        }

        var callbackIndex = Array.IndexOf(subscribers, onChanged);
        if(callbackIndex < 0)
        {
            return;
        }

        if (subscribers.Length == 1)
        {
            mRedDotNameSubscriberMap.Remove(redDotName);
            return;
        }

        var newSubscribers = new Action<RedDotSnapshot>[subscribers.Length - 1];
        if(callbackIndex > 0)
        {
            Array.Copy(subscribers, 0, newSubscribers, 0, callbackIndex);
        }
        var trailingCount = subscribers.Length - callbackIndex - 1;
        if(trailingCount > 0)
        {
            Array.Copy(
                subscribers,
                callbackIndex + 1,
                newSubscribers,
                callbackIndex,
                trailingCount);
        }
        mRedDotNameSubscriberMap[redDotName] = newSubscribers;
    }

    private void NotifyRedDotNameSubscribers(RedDotSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.RedDotName) ||
            !mRedDotNameSubscriberMap.TryGetValue(snapshot.RedDotName, out var subscribers))
        {
            return;
        }

        if(subscribers == null || subscribers.Length == 0)
        {
            return;
        }

        // 订阅数组只在绑定变化时分配，通知热路径不再调用 GetInvocationList。
        foreach(var callback in subscribers)
        {
            try
            {
                callback.Invoke(snapshot);
            }
            catch(Exception exception)
            {
                var callbackMethod = callback.Method;
                Debug.LogException(new InvalidOperationException(
                    $"红点名:{snapshot.RedDotName}订阅回调:" +
                    $"{callbackMethod.DeclaringType?.Name}.{callbackMethod.Name}执行失败",
                    exception));
            }
        }
    }

    /// <summary>
    /// 获取指定红点名结果
    /// </summary>
    /// <param name="redDotName"></param>
    /// <returns></returns>
    public int GetRedDotResult(string redDotName)
    {
        var redDotUnitList = RedDotModel.Singleton.GetRedDotUnitsByName(redDotName);
        if(redDotUnitList == null)
        {
            return 0;
        }
        var result = 0;
        foreach(var redDotUnit in redDotUnitList)
        {
            var redDotUnitResult = RedDotModel.Singleton.GetRedDotUnitResult(redDotUnit);
            result += redDotUnitResult;
        }
        return result;
    }

    /// <summary>
    /// 红点系统每帧更新入口。
    /// Note:
    /// 不是每帧都检查脏数据，而是累计到固定帧数后处理一个批次，
    /// 避免业务数据频繁变化时每帧重复扫描和计算。
    /// </summary>
    public void Update()
    {
        mFramePassed++;
        if(mFramePassed >= DIRTY_UPDATE_INTERVAL_FRAME)
        {
            // 每次只处理一批脏单元，剩余单元继续留在脏集合中等待下一轮。
            CheckDirtyRedDotUnit();
            mFramePassed = 0;
        }
    }

    /// <summary>
    /// 立即计算全部待处理红点单元。用于启动阶段，
    /// 确保 UI 首帧拿到完整快照而不是旧数据。
    /// </summary>
    public void EvaluateAllDirtyNow()
    {
        if(!RedDotModel.Singleton.IsInitCompelte)
        {
            return;
        }

        const int maxIterations = 10000;
        var iteration = 0;
        // CheckDirtyRedDotUnit() 每轮只处理有限数量的单元，
        // 因此启动阶段循环调用，直到所有初始脏数据和强制刷新都处理完。
        while((mDirtyRedDotUnitSet.Count > 0 || mForceRefreshRedDotNameSet.Count > 0) &&
              iteration < maxIterations)
        {
            CheckDirtyRedDotUnit();
            iteration++;
        }

        if(mDirtyRedDotUnitSet.Count > 0 || mForceRefreshRedDotNameSet.Count > 0)
        {
            Debug.LogWarning("EvaluateAllDirtyNow reached the iteration limit; some red dots remain dirty.");
        }
    }

    /// <summary>
    /// 标记指定红点名脏
    /// </summary>
    /// <param name="redDotName"></param>
    public void MarkRedDotNameDirty(string redDotName)
    {
        var redDotUnitList = RedDotModel.Singleton.GetRedDotUnitsByName(redDotName);
        if (redDotUnitList != null)
        {
            // 红点节点本身不保存可执行公式，标脏最终会落到它绑定的所有运算单元。
            foreach (var redDotUnit in redDotUnitList)
            {
                MarkRedDotUnitDirty(redDotUnit);
            }
        }
    }

    /// <summary>
    /// 标记指定红点运算类型脏
    /// </summary>
    /// <param name="redDotUnit"></param>
    public void MarkRedDotUnitDirty(string redDotUnit)
    {
        // 只入队，不在这里执行计算；真正计算由 CheckDirtyRedDotUnit() 统一调度。
        mDirtyRedDotUnitSet.Add(redDotUnit);
    }

    /// <summary>
    /// 强制刷新指定红点名
    /// </summary>
    /// <param name="redDotName"></param>
    public void ForceRefreshRedDotName(string redDotName)
    {
        // 绕过“结果是否变化”的判断，下一轮检查时无条件通知该红点名。
        mForceRefreshRedDotNameSet.Add(redDotName);
    }

    /// <summary>
    /// 处理一批标脏的红点运算单元，并把变化通知给相关红点名。
    ///
    /// 完整流程：
    /// 1. 从待处理集合中取最多 30 个单元，作为当前批次；
    /// 2. 先把这些单元从待处理集合移除，防止计算期间修改原集合；
    /// 3. 重新执行单元的规则函数，并比较新旧结果；
    /// 4. 只有结果变化时，才反查该单元影响的所有红点名；
    /// 5. 合并必须强制刷新的红点名；
    /// 6. 对需要刷新的红点名重新生成快照，并通知订阅者。
    ///
    /// Note:
    /// 本函数计算的是“运算单元”，不是直接计算整棵红点树。
    /// 红点名的结果由 GetRedDotNameResult() 根据单元缓存结果汇总。
    /// </summary>
    private void CheckDirtyRedDotUnit()
    {
        if(!RedDotModel.Singleton.IsInitCompelte)
        {
            return;
        }
        if(mDirtyRedDotUnitSet.Count == 0 && mForceRefreshRedDotNameSet.Count == 0)
        {
            return;
        }
        // 限制每次最大计算红点单元数量，避免单帧计算过多导致过卡问题
        // 只计算标脏的红点单元，其他单元不重复计算
        // 通过从缓存结果直接获取结果避免过多的不必要红点单元运算
        // 红点单元的结果变化才通知相关红点名刷新
        //
        // 这里先收集本批次，而不是直接遍历 mDirtyRedDotUnitSet 并计算，
        // 是为了在计算过程中安全地处理“再次标脏”的情况。
        var dirtyRedDotUnitNum = 0;
        mWaitCalculateRedDotUnitSet.Clear();
        foreach(var dirtyRedDotUnit in mDirtyRedDotUnitSet)
        {
            if(dirtyRedDotUnitNum >= CALCULATE_RED_DOT_UNIT_NUM_PER_FRAME)
            {
                continue;
            }
            mWaitCalculateRedDotUnitSet.Add(dirtyRedDotUnit);
            dirtyRedDotUnitNum++;
        }

        // 先移出原始待处理队列，再执行计算。
        // 如果某个规则函数在计算过程中再次标脏同一个单元，
        // 该单元可以重新进入 mDirtyRedDotUnitSet，等待下一批次处理。
        foreach(var redDotUnit in mWaitCalculateRedDotUnitSet)
        {
            mDirtyRedDotUnitSet.Remove(redDotUnit);
        }

        // 这两个集合只描述当前批次，先清空以避免混入上一批次的数据。
        mCaculatedRedDotUnitResultChangeMap.Clear();
        mNeedRefreshRedDotNameSet.Clear();
        foreach(var redDotUnit in mWaitCalculateRedDotUnitSet)
        {
            // 逻辑层可能出现还未初始化但在标脏的红点单元的情况
            // 这种情况不需要触发红点单元运算，直接跳过
            // 动态红点被移除后，也可能残留旧的脏记录，同样在这里跳过。
            if(!RedDotModel.Singleton.ExistsRedDotUnitInfo(redDotUnit))
            {
                continue;
            }
            bool resultChange = false;
            // 当前批次内记录单元的计算结果是否变化，避免同一批次重复计算。
            // mWaitCalculateRedDotUnitSet 本身是 HashSet，通常不会重复；
            // 这层 Map 主要用于明确表达“本批次已经算过”的语义。
            if(!mCaculatedRedDotUnitResultChangeMap.TryGetValue(redDotUnit, out resultChange))
            {
                resultChange = DoRedDotUnitCaculate(redDotUnit);
                mCaculatedRedDotUnitResultChangeMap.Add(redDotUnit, resultChange);
            }
            if(!resultChange)
            {
                // 例如旧值是 2，重新计算后仍然是 2。
                // 单元缓存已更新，但没有必要通知 UI，避免无意义刷新。
                continue;
            }
            // 反向依赖：一个运算单元会影响哪些红点名。
            // 由于子节点单元会递归挂到父节点，这里通常包含当前节点及所有父节点。
            var redDotNameList = RedDotModel.Singleton.GetRedDotNamesByUnit(redDotUnit);
            if(redDotNameList == null || redDotNameList.Count == 0)
            {
                continue;
            }
            foreach(var redDotName in redDotNameList)
            {
                // 多个变化单元可能影响同一个红点名，用 HashSet 去重。
                mNeedRefreshRedDotNameSet.Add(redDotName);
            }
        }

        // 动态节点创建/删除等场景可能没有单元结果变化，
        // 但仍需要让 UI 收到最新快照，因此把强制刷新节点合并进来。
        foreach(var redDotName in mForceRefreshRedDotNameSet)
        {
            mNeedRefreshRedDotNameSet.Add(redDotName);
        }
        mForceRefreshRedDotNameSet.Clear();
        
        // 通知上层相关红点名结果变化，触发刷新回调。
        // 无论通知过程是否出现异常，都必须清理当前批次状态。
        try
        {
            foreach (var resultChangedRedDotName in mNeedRefreshRedDotNameSet)
            {
                // 在动态红点名移除的情况下是可能出现红点名需要刷新但不存在的情况
                if(!RedDotModel.Singleton.IsRedDotNameExist(resultChangedRedDotName))
                {
                    continue;
                }

                try
                {
                    TriggerRedDotNameUpdate(resultChangedRedDotName);
                }
                catch(Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        $"红点名:{resultChangedRedDotName}刷新失败",
                        exception));
                }
            }
        }
        finally
        {
            mNeedRefreshRedDotNameSet.Clear();
        }
    }

    /// <summary>
    /// 重新计算一个红点运算单元，并更新其缓存结果。
    /// Note:
    /// 本函数只返回“新旧结果是否变化”，不负责通知红点名。
    /// 通知流程由调用方 CheckDirtyRedDotUnit() 统一处理。
    /// </summary>
    /// <param name="redDotUnit"></param>
    /// <returns></returns>
    private bool DoRedDotUnitCaculate(string redDotUnit)
    {
        // 保存旧结果，计算结束后用于判断是否需要通知 UI。
        var preResult = RedDotModel.Singleton.GetRedDotUnitResult(redDotUnit);
        var redDotUnitFunc = RedDotModel.Singleton.GetRedDotUnitFunc(redDotUnit);
        var result = preResult;
        if(redDotUnitFunc != null)
        {
            // 规则函数只读取当前业务数据并返回一个整数，例如：
            // return MailModel.Singleton.NewPublicMailNum;
            try
            {
                result = redDotUnitFunc();
            }
            catch(Exception exception)
            {
                // 保留最后一次有效结果，避免异常直接把已有红点错误熄灭。
                Debug.LogException(new InvalidOperationException(
                    $"红点运算单元:{redDotUnit}计算失败，保留上次结果:{preResult}",
                    exception));
                return false;
            }
        }
        else
        {
            result = 0;
            //Debug.LogError($"红点运算单元:{redDotUnit.ToString()}未绑定有效计算方法!");
        }
        // 用新结果覆盖单元缓存，并返回是否发生变化。
        RedDotModel.Singleton.SetRedDotUnitResult(redDotUnit, result);
        return preResult != result;
    }
}
}
