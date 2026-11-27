/*
 * Description:             RedDotInitializer.cs
 * Author:                  TONYTANG
 * Create Date:             2026/04/02
 */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
/// <summary>
/// RedDotInitializer.cs
/// 红点初始化器单例类
/// </summary>
namespace Game.RedDot
{
public class RedDotInitializer : IRedDotRegistrar
{
    private static RedDotInitializer sSingleton;
    private readonly List<FuncRDInitializer> mRegisteredInitializers;

    public static RedDotInitializer Singleton
    {
        get
        {
            if (sSingleton == null)
            {
                sSingleton = new RedDotInitializer();
            }
            return sSingleton;
        }
    }

    /// <summary>
    /// 是否初始化完成
    /// </summary>
    public bool IsInitComplete
    {
        get;
        private set;
    }

    /// <summary>
    /// 当前是否允许业务模块注册红点初始化器。
    /// </summary>
    public bool IsRegistrationOpen { get; private set; }

    /// <summary>
    /// 所有的功能红点初始化器Map[红点初始化名字, 功能红点初始化器实例]
    /// </summary>
    public Dictionary<string, FuncRDInitializer> AllFuncRedDotInitializerMap
    {
        get;
        private set;
    }

    /// <summary>
    /// 业务模块注册的顶层红点初始化器。
    /// </summary>
    public IReadOnlyList<FuncRDInitializer> RegisteredInitializers
    {
        get
        {
            return mRegisteredInitializers;
        }
    }

    public RedDotInitializer()
    {
        IsInitComplete = false;
        IsRegistrationOpen = false;
        AllFuncRedDotInitializerMap = new Dictionary<string, FuncRDInitializer>();
        mRegisteredInitializers = new List<FuncRDInitializer>();
    }

    /// <summary>
    /// 进入红点注册阶段。
    /// </summary>
    public void BeginRegistration()
    {
        if(IsRegistrationOpen)
        {
            Debug.LogError("红点初始化器已处于注册阶段，请勿重复开始注册!");
            return; 
        }
        if(IsInitComplete)
        {
            Debug.LogError("红点初始化器已完成注册，请先释放后再重新开始!");
            return;
        }

        mRegisteredInitializers.Clear();
        AllFuncRedDotInitializerMap.Clear();
        IsRegistrationOpen = true;
        RedDotModel.Singleton.BeginDerivedDataBatch();
    }

    /// <summary>
    /// 注册一个顶层业务红点初始化器。
    /// </summary>
    public bool Register(FuncRDInitializer initializer)
    {
        if(!IsRegistrationOpen)
        {
            Debug.LogError("红点初始化器尚未进入注册阶段，无法注册业务红点!");
            return false;
        }
        if(initializer == null)
        {
            Debug.LogError("业务红点初始化器不能为空!");
            return false;
        }

        var initializerName = FuncRDInitializer.GetInitializerName(initializer);
        if(AllFuncRedDotInitializerMap.ContainsKey(initializerName))
        {
            Debug.LogError($"功能红点初始化器名:{initializerName}的实例对象已存在，无法重复注册!");
            return false;
        }

        AllFuncRedDotInitializerMap.Add(initializerName, initializer);
        mRegisteredInitializers.Add(initializer);
        try
        {
            initializer.Init();
        }
        catch
        {
            mRegisteredInitializers.Remove(initializer);
            AllFuncRedDotInitializerMap.Remove(initializerName);
            throw;
        }
        return true;
    }

    /// <summary>
    /// 结束注册并标记红点定义图已就绪。
    /// </summary>
    public void CompleteRegistration()
    {
        if(!IsRegistrationOpen)
        {
            Debug.LogError("红点初始化器尚未进入注册阶段，无法完成注册!");
            return;
        }

        IsRegistrationOpen = false;
        RedDotModel.Singleton.EndDerivedDataBatch();
        IsInitComplete = true;
    }

    /// <summary>
    /// 释放
    /// </summary>
    public void Dispose()
    {
        var wasRegistrationOpen = IsRegistrationOpen;
        DestroyAllRegisteredInitializers();
        if(wasRegistrationOpen)
        {
            RedDotModel.Singleton.EndDerivedDataBatch();
        }
        mRegisteredInitializers.Clear();
        AllFuncRedDotInitializerMap.Clear();
        IsRegistrationOpen = false;
        IsInitComplete = false;
    }

    /// <summary>
    /// 创建指定功能红点初始化器
    /// Note:
    /// 业务模块统一通过 Register 注册顶层初始化器
    /// <param name="dependencyInitializer">依赖的功能红点初始化器</param>
    /// </summary>
    public (bool, T) CreateInitializer<T>(FuncRDInitializer dependencyInitializer = null) where T : FuncRDInitializer, new()
    {
        var initializerName = FuncRDInitializer.GetInitializerName<T>();
        var initializer = GetInitializer<T>(initializerName);
        if(initializer != null)
        {
            Debug.LogError($"功能红点初始化器名:{initializerName}的实例对象已存在，无法重复创建!");
            return (false, initializer);
        }
        initializer = new T();
        AllFuncRedDotInitializerMap.Add(initializerName, initializer);
        initializer.Init(dependencyInitializer);
        return (true, initializer);
    }

    /// <summary>
    /// 销毁指定初始化器名的红点初始化器
    /// </summary>
    /// <param name="initializerName"></param>
    /// <returns></returns>
    public bool DestroyInitializer(string initializerName)
    {
        if(!AllFuncRedDotInitializerMap.TryGetValue(initializerName, out var funcRedDotInitializer))
        {
            Debug.LogError($"功能红点初始化器名:{initializerName}的未注册，无法销毁!");
            return false;            
        }
        AllFuncRedDotInitializerMap.Remove(initializerName);
        funcRedDotInitializer.Dispose();
        return true;
    }

    /// <summary>
    /// 销毁指定功能红点初始化器实例对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="initializer"></param>
    /// <returns></returns>
    public bool DestroyInitializer<T>(T initializer) where T : FuncRDInitializer
    {
        if(initializer == null)
        {
            Debug.LogError($"功能红点初始化器实例对象为null，无法销毁!");
            return false;            
        }
        var initializerName = FuncRDInitializer.GetInitializerName(initializer);
        var existingInitializer = GetInitializer<T>(initializerName);
        if(existingInitializer == null)
        {
            Debug.LogError($"功能红点初始化器实例对象:{initializer}的未注册，无法销毁!");
            return false;
        }
        if(existingInitializer != initializer)
        {
            Debug.LogError($"功能红点初始化器实例对象:{initializer}与已注册的实例对象:{existingInitializer}不匹配，无法销毁!");
            return false;
        }
        return DestroyInitializer(initializerName);
    }

    /// <summary>
    /// 激活指定类型的红点初始化器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    protected bool ActiveInitializer<T>() where T : FuncRDInitializer
    {
        var initializerName = FuncRDInitializer.GetInitializerName<T>();
        return ActiveInitializer(initializerName);
    }

    /// <summary>
    /// 激活指定功能红点初始化器
    /// </summary>
    /// <param name="initializerName"></param>
    /// <returns></returns>
    protected bool ActiveInitializer(string initializerName)
    {
        if(!AllFuncRedDotInitializerMap.TryGetValue(initializerName, out var funcRedDotInitializer))
        {
            Debug.LogError($"功能红点初始化器名:{initializerName}的未注册，无法激活!");
            return false;
        
        }
        if(funcRedDotInitializer.IsActive)
        {
            Debug.LogWarning($"功能红点初始化器名:{initializerName}的实例对象已激活，请勿重复激活!");
            return false;
        }
        funcRedDotInitializer.Active();
        return true;
    }

    /// <summary>
    /// 取消激活指定类型的红点初始化器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    protected bool InactiveInitializer<T>() where T : FuncRDInitializer
    {
        var initializerName = FuncRDInitializer.GetInitializerName<T>();
        return InactiveInitializer(initializerName);
    }
    
    /// <summary>
    /// 取消激活指定功能红点初始化器
    /// </summary>
    /// <param name="initializerName"></param>
    /// <returns></returns>
    protected bool InactiveInitializer(string initializerName)
    {
        if(!AllFuncRedDotInitializerMap.TryGetValue(initializerName, out var funcRedDotInitializer))
        {
            Debug.LogError($"功能红点初始化器名:{initializerName}的未注册，无法反激活!");
            return false;            
        
        }
        if(!funcRedDotInitializer.IsActive)
        {
            return false;
        }
        funcRedDotInitializer.Inactive();
        return true;
    }

    /// <summary>
    /// 获取指定功能红点初始化器名的实力对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="initializerName"></param>
    /// <returns></returns>
    protected T GetInitializer<T>(string initializerName) where T : FuncRDInitializer
    {
        if(!AllFuncRedDotInitializerMap.TryGetValue(initializerName, out var funcRedDotInitializer))
        {
            return null;            
        }
        return funcRedDotInitializer as T;
    }

    /// <summary>
    /// 按注册的逆序销毁所有顶层业务红点初始化器。
    /// </summary>
    private void DestroyAllRegisteredInitializers()
    {
        for(int index = mRegisteredInitializers.Count - 1; index >= 0; index--)
        {
            var initializer = mRegisteredInitializers[index];
            var initializerName = FuncRDInitializer.GetInitializerName(initializer);
            if(AllFuncRedDotInitializerMap.ContainsKey(initializerName))
            {
                DestroyInitializer(initializerName);
            }
            else
            {
                initializer.Dispose();
            }
        }
    }

    /// <summary>
    /// 是否已经初始化指定功能红点初始化器
    /// </summary>
    /// <param name="initializerName"></param>
    /// <returns></returns>
    public bool HasInitializer(string initializerName)
    {
        return AllFuncRedDotInitializerMap.ContainsKey(initializerName);
    }
    
}
}
