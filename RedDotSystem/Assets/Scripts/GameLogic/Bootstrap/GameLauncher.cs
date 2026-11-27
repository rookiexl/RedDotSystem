using Game.RedDot;
using Game.RedDot.Unity;

/*
 * Description:             GameLauncher.cs
 * Author:                  TONYTANG
 * Create Date:             2022/08/10
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameLauncher.cs
/// 游戏启动器
/// </summary>
public class GameLauncher : MonoBehaviour
{
    /// <summary>
    /// 单例对象
    /// </summary>
    public static GameLauncher Singleton
    {
        get
        {
            return mSingleton;
        }
    }
    private static GameLauncher mSingleton;

    /// <summary>
    /// 主界面
    /// </summary>
    [Header("主界面")]
    public MainUI MainUI;

    /// <summary>
    /// 背包界面
    /// </summary>
    [Header("背包界面")]
    public BackpackUI BackpackUI;

    /// <summary>
    /// 装备界面
    /// </summary>
    [Header("装备界面")]
    public EquipUI EquipUI;

    /// <summary>
    /// 邮件界面
    /// </summary>
    [Header("邮件界面")]
    public MailUI MailUI;

    /// <summary>
    /// 关卡入口界面
    /// </summary>
    [Header("关卡入口界面")]
    public LevelEntryUI LevelEntryUI;

    /// <summary>
    /// 关卡界面
    /// </summary>
    [Header("关卡界面")]
    public LevelUI LevelUI;

    /// <summary>
    /// 活动界面
    /// </summary>
    [Header("活动界面")]
    public ActivityUI ActivityUI;

    /// <summary>
    /// PVE界面
    /// </summary>
    [Header("PVE界面")]
    public PVEUI PVEUI;

    private GameStartupPipeline mStartupPipeline;

    private void Awake()
    {
        mSingleton = this;
        var moduleManager = new GameModuleManager(new IGameModule[]
        {
            new GameDataModule(),
            new SystemUnlockModule(),
            new MainUIModule(MainUI),
            new BackpackModule(),
            new MailModule(),
            new EquipModule(),
            new LevelModule(),
            new PVEModule(),
            new ActivityModule()
        });
        mStartupPipeline = new GameStartupPipeline(moduleManager);
        mStartupPipeline.Run();
    }

    public void Update()
    {
        if(mStartupPipeline != null)
        {
            mStartupPipeline.Update();
        }
    }

    void OnDestroy()
    {
        if(mStartupPipeline != null)
        {
            mStartupPipeline.Dispose();
            mStartupPipeline = null;
        }
        mSingleton = null;
    }
}
