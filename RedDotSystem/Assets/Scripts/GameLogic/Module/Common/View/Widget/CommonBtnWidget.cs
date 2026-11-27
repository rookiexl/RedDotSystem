using Game.RedDot;
using Game.RedDot.Unity;

/*
 * Description:             CommonBtnWidget.cs
 * Author:                  TONYTANG
 * Create Date:             2026/05/22
 */

using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 组合按钮视图。文本、点击回调和红点目标分别配置，
/// 红点订阅由红点视图自身负责。
/// </summary>
public class CommonBtnWidget : MonoBehaviour
{
    [Header("Button")]
    public Button Btn;

    [Header("Text")]
    public Text Txt;

    [Header("Red dot view")]
    public RedDotWidget RDWidget;

    private string mTxt;
    private Action mClickCb;

    private void Awake()
    {
        AddAllListeners();
    }

    private void OnDestroy()
    {
        if (Btn != null)
        {
            Btn.onClick.RemoveListener(OnBtnClick);
        }
        ClearRedDotName();
    }

    private void AddAllListeners()
    {
        if (Btn != null)
        {
            Btn.onClick.AddListener(OnBtnClick);
        }
    }

    public void Init(string txt, Action clickCb = null)
    {
        SetText(txt);
        SetClickCallback(clickCb);
    }

    public void SetText(string txt)
    {
        mTxt = txt;
        RefreshView();
    }

    public void SetClickCallback(Action clickCb)
    {
        mClickCb = clickCb;
    }

    public void SetRedDotName(string redDotName)
    {
        if (RDWidget == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(redDotName))
        {
            RDWidget.ClearRedDotName();
            return;
        }

        RDWidget.SetRedDotName(redDotName);
    }

    public void ClearRedDotName()
    {
        if (RDWidget != null)
        {
            RDWidget.ClearRedDotName();
        }
    }

    private void RefreshView()
    {
        if (Txt != null)
        {
            Txt.text = mTxt;
        }
    }

    private void OnBtnClick()
    {
        mClickCb?.Invoke();
    }
}
