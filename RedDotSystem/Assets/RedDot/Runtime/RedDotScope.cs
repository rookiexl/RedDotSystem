using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.RedDot
{
/// <summary>
/// 一组动态红点的注册与释放作用域。
/// Note:
/// 注册操作先记录为待提交项；Commit() 时统一写入模型并只重建一次父子关系。
/// Dispose() 会批量移除本作用域成功注册的所有红点名。
/// </summary>
public sealed class RedDotScope : IDisposable
{
    private readonly string mOwner;
    private readonly List<PendingRegistration> mPendingRegistrations;
    private readonly HashSet<string> mPendingRedDotNameSet;
    private readonly List<string> mRegisteredRedDotNameList;
    private bool mIsCommitted;
    private bool mIsDisposed;
    private bool mCommitFailed;

    public string Owner
    {
        get
        {
            return mOwner;
        }
    }

    public bool IsCommitted
    {
        get
        {
            return mIsCommitted;
        }
    }

    public bool IsDisposed
    {
        get
        {
            return mIsDisposed;
        }
    }

    internal RedDotScope(string owner)
    {
        mOwner = owner ?? string.Empty;
        mPendingRegistrations = new List<PendingRegistration>();
        mPendingRedDotNameSet = new HashSet<string>(StringComparer.Ordinal);
        mRegisteredRedDotNameList = new List<string>();
    }

    /// <summary>
    /// 添加不带独立处理器的红点名。
    /// </summary>
    public bool AddRedDotInfo(string redDotName, string redDotDes)
    {
        if(!TryAddPendingRegistration(
               redDotName,
               () => RedDotModel.Singleton.AddRedDotInfo(redDotName, redDotDes).Item1))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// 添加带静态处理器的红点名。
    /// </summary>
    public bool AddRedDotInfo<T>(string redDotName, string redDotDes)
        where T : RedDotHandler, new()
    {
        return TryAddPendingRegistration(
            redDotName,
            () => RedDotModel.Singleton.AddRedDotInfo<T>(redDotName, redDotDes).Item1);
    }

    /// <summary>
    /// 添加带动态处理器参数的红点名。
    /// </summary>
    public bool AddDynamicRedDotInfo<T, W>(
        string redDotName,
        string redDotDes,
        W dynamicData)
        where T : DynamicRedDotHandler<W>, new()
    {
        return TryAddPendingRegistration(
            redDotName,
            () => RedDotModel.Singleton
                .AddDynamicRedDotInfo<T, W>(redDotName, redDotDes, dynamicData)
                .Item1);
    }

    /// <summary>
    /// 提交待注册红点，并统一重建一次父子关系。
    /// </summary>
    public bool Commit()
    {
        if(mIsDisposed)
        {
            Debug.LogError($"红点作用域:{mOwner}已释放，无法提交!");
            return false;
        }
        if(mCommitFailed)
        {
            Debug.LogError($"红点作用域:{mOwner}提交失败过一次，无法重复提交!");
            return false;
        }
        if(mIsCommitted)
        {
            return true;
        }
        if(mPendingRegistrations.Count == 0)
        {
            mIsCommitted = true;
            return true;
        }

        RedDotModel.Singleton.BeginDerivedDataBatch();
        try
        {
            foreach(var pendingRegistration in mPendingRegistrations)
            {
                var redDotName = pendingRegistration.RedDotName;
                var existedBeforeRegistration =
                    RedDotModel.Singleton.IsRedDotNameExist(redDotName);
                try
                {
                    if(!pendingRegistration.Register())
                    {
                        mCommitFailed = true;
                        RollbackRegisteredRedDotNames();
                        ClearPendingRegistrations();
                        Debug.LogError(
                            $"红点作用域:{mOwner}提交失败，已回滚本次注册");
                        return false;
                    }
                }
                catch
                {
                    // 处理器初始化中途异常时，节点可能已经写入模型。
                    if(!existedBeforeRegistration &&
                       RedDotModel.Singleton.IsRedDotNameExist(redDotName))
                    {
                        mRegisteredRedDotNameList.Add(redDotName);
                    }
                    throw;
                }
                mRegisteredRedDotNameList.Add(redDotName);
            }

            ClearPendingRegistrations();
            mIsCommitted = true;
            return true;
        }
        catch
        {
            mCommitFailed = true;
            RollbackRegisteredRedDotNames();
            ClearPendingRegistrations();
            throw;
        }
        finally
        {
            RedDotModel.Singleton.EndDerivedDataBatch();
        }
    }

    /// <summary>
    /// 批量移除本作用域注册的所有红点名。
    /// </summary>
    public void Dispose()
    {
        if(mIsDisposed)
        {
            return;
        }

        if(mIsCommitted && mRegisteredRedDotNameList.Count > 0)
        {
            RedDotModel.Singleton.BeginDerivedDataBatch();
            try
            {
                RollbackRegisteredRedDotNames();
            }
            finally
            {
                RedDotModel.Singleton.EndDerivedDataBatch();
            }
        }
        else
        {
            ClearPendingRegistrations();
        }

        mRegisteredRedDotNameList.Clear();
        mIsDisposed = true;
    }

    /// <summary>
    /// 记录待提交注册；未提交前不修改全局红点模型。
    /// </summary>
    private bool TryAddPendingRegistration(
        string redDotName,
        Func<bool> registerFunc)
    {
        if(mIsDisposed || mIsCommitted || mCommitFailed)
        {
            Debug.LogError(
                $"红点作用域:{mOwner}当前状态不允许继续添加红点名:{redDotName}");
            return false;
        }
        if(string.IsNullOrEmpty(redDotName))
        {
            Debug.LogError($"红点作用域:{mOwner}不能添加空红点名!");
            RedDotModel.Singleton.RecordRegistrationIssue(
                "RED_DOT_SCOPE_NAME_EMPTY",
                $"红点作用域:{mOwner}尝试添加空红点名");
            return false;
        }
        if(!mPendingRedDotNameSet.Add(redDotName))
        {
            Debug.LogError(
                $"红点作用域:{mOwner}重复添加红点名:{redDotName}");
            RedDotModel.Singleton.RecordRegistrationIssue(
                "RED_DOT_SCOPE_DUPLICATE_NODE",
                $"红点作用域:{mOwner}重复添加红点名:{redDotName}");
            return false;
        }

        mPendingRegistrations.Add(new PendingRegistration(redDotName, registerFunc));
        return true;
    }

    /// <summary>
    /// 按子节点优先顺序回滚当前已注册红点。
    /// </summary>
    private void RollbackRegisteredRedDotNames()
    {
        if(mRegisteredRedDotNameList.Count == 0)
        {
            return;
        }

        mRegisteredRedDotNameList.Sort(CompareRedDotNameDepthDescending);
        foreach(var redDotName in mRegisteredRedDotNameList)
        {
            if(RedDotModel.Singleton.IsRedDotNameExist(redDotName))
            {
                RedDotModel.Singleton.RemoveRedDotInfo(redDotName);
            }
        }
        mRegisteredRedDotNameList.Clear();
    }

    /// <summary>
    /// 清理尚未提交的注册记录。
    /// </summary>
    private void ClearPendingRegistrations()
    {
        mPendingRegistrations.Clear();
        mPendingRedDotNameSet.Clear();
    }

    /// <summary>
    /// 红点名深度降序，保证子节点先于父节点移除。
    /// </summary>
    private int CompareRedDotNameDepthDescending(
        string left,
        string right)
    {
        var depthCompare = RedDotUtilities
            .GetRedDotNameDepth(right)
            .CompareTo(RedDotUtilities.GetRedDotNameDepth(left));
        if(depthCompare != 0)
        {
            return depthCompare;
        }
        return string.CompareOrdinal(right, left);
    }

    /// <summary>
    /// 待提交的红点注册项。
    /// </summary>
    private readonly struct PendingRegistration
    {
        public string RedDotName { get; }
        public Func<bool> Register { get; }

        public PendingRegistration(string redDotName, Func<bool> register)
        {
            RedDotName = redDotName;
            Register = register;
        }
    }
}
}
