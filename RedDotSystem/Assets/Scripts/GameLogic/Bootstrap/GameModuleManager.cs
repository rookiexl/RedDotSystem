using System;
using System.Collections.Generic;
using Game.RedDot;
using UnityEngine;

/// <summary>
/// 按生命周期阶段执行业务模块，并保持明确的注册顺序。
/// </summary>
public sealed class GameModuleManager
{
    private readonly List<IGameModule> mModules = new List<IGameModule>();

    public IReadOnlyList<IGameModule> Modules
    {
        get
        {
            return mModules;
        }
    }

    public GameModuleManager(IEnumerable<IGameModule> modules)
    {
        if(modules == null)
        {
            throw new ArgumentNullException(nameof(modules));
        }

        var moduleNames = new HashSet<string>();
        foreach(var module in modules)
        {
            if(module == null)
            {
                throw new ArgumentException("Game module cannot be null.", nameof(modules));
            }
            if(!moduleNames.Add(module.Name))
            {
                throw new ArgumentException($"Duplicate game module name:{module.Name}", nameof(modules));
            }
            mModules.Add(module);
        }
    }

    public void InitializeAll()
    {
        foreach(var module in mModules)
        {
            Debug.Log($"Game module initialize:{module.Name}");
            module.Initialize();
        }
    }

    public void RegisterRedDots(IRedDotRegistrar registrar)
    {
        foreach(var module in mModules)
        {
            module.RegisterRedDots(registrar);
        }
    }

    public void StartAll()
    {
        var startModules = new List<IGameModule>(mModules);
        startModules.Sort((left, right) => left.StartOrder.CompareTo(right.StartOrder));
        foreach(var module in startModules)
        {
            module.Start();
        }
    }

    public void DisposeAll()
    {
        for(var index = mModules.Count - 1; index >= 0; index--)
        {
            mModules[index].Dispose();
        }
    }
}
