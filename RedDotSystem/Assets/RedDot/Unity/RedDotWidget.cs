using Game.RedDot;

namespace Game.RedDot.Unity
{
/*
 * Description:             RedDotWidget.cs
 * Author:                  TONYTANG
 * Create Date:             2022/08/14
 */

using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 红点视图。自行管理订阅，只负责渲染当前快照；
/// 业务计算和聚合仍由红点系统负责。
/// </summary>
[DisallowMultipleComponent]
public class RedDotWidget : MonoBehaviour
{
    [Header("Red dot image")]
    public Image ImgRedDot;

    [Header("Red dot text")]
    public Text TxtRedDotDes;

    [SerializeField, Header("Red dot name")]
    private string mRedDotName;

    private IDisposable mSubscription;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    /// <summary>
    /// 设置要显示的红点节点。传空值时保持隐藏。
    /// </summary>
    public void SetRedDotName(string redDotName)
    {
        if (mRedDotName == redDotName)
        {
            TrySubscribe();
            return;
        }

        Unsubscribe();
        mRedDotName = redDotName;
        TrySubscribe();
    }

    /// <summary>
    /// 清除目标红点节点，和临时隐藏不同。
    /// </summary>
    public void ClearRedDotName()
    {
        Unsubscribe();
        mRedDotName = null;
        SetVisualActive(false);
    }

    private void TrySubscribe()
    {
        if (!isActiveAndEnabled || mSubscription != null)
        {
            return;
        }
        if (string.IsNullOrEmpty(mRedDotName))
        {
            SetVisualActive(false);
            return;
        }

        mSubscription = RedDotService.Provider.Subscribe(mRedDotName, OnRedDotRefresh);
        Render(RedDotService.Provider.GetSnapshot(mRedDotName));
        RedDotService.Provider.RequestRefresh(mRedDotName);
    }

    private void Unsubscribe()
    {
        if (mSubscription == null)
        {
            return;
        }

        mSubscription.Dispose();
        mSubscription = null;
    }

    private void OnRedDotRefresh(RedDotSnapshot snapshot)
    {
        Render(snapshot);
    }

    private void Render(RedDotSnapshot snapshot)
    {
        var result = snapshot.IsValid ? snapshot.Result : 0;
        var displayType = snapshot.IsValid ? snapshot.DisplayType : RedDotType.NONE;
        SetVisualActive(result > 0);
        SetRedDotText(RedDotUtilities.GetRedDotResultText(result, displayType));
    }

    /// <summary>
    /// 只隐藏视觉子节点。宿主对象必须保持激活，
    /// 才能持续接收状态变化。
    /// </summary>
    private void SetVisualActive(bool active)
    {
        if (ImgRedDot != null)
        {
            ImgRedDot.gameObject.SetActive(active);
        }
        if (TxtRedDotDes != null)
        {
            TxtRedDotDes.gameObject.SetActive(active);
        }
    }

    private void SetRedDotText(string redDotText)
    {
        if (TxtRedDotDes != null)
        {
            TxtRedDotDes.text = redDotText;
        }
    }
}
}
