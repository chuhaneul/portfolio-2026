using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 등록 / 해제 확인 팝업(PopupLevelLinkCardChange)의 제목 영역
/// - 해제 모드 : "등록 해제" 제목, 등록 모드 : "안내" 제목
/// - 부모 : PopupLevelLinkCardChange.SetUI (SetData → SetUI)
/// </summary>
public class PopupLevelLinkCardChange_TopUI : BaseUILayout<PopupLevelLinkCardChange_TopUI.Setting>
{
    //제목 텍스트
    [SerializeField] private Text TxtTitle;

    /// <summary>
    /// 제목 갱신
    /// </summary>
    public override void SetUI()
    {
        SetTitle();
    }

    /// <summary>
    /// 모드에 따라 문자열 키를 골라 제목 표시
    /// </summary>
    private void SetTitle()
    {
        if( TxtTitle == null) return;
        if( setting.IsLevelLinkUnregister )
        {
            TxtTitle.text = CStringDataManager.Instance.GetStringData("levellink_popup_title_02"); //등록 해제
        }
        else
        {
            TxtTitle.text = CStringDataManager.Instance.GetStringData("UI_common_alert"); //안내
        }
    }


    /// <summary>
    /// 제목 영역 입력 데이터
    /// </summary>
    public class Setting
    {
        //true = 등록 해제 모드
        public bool IsLevelLinkUnregister = false;
    }

}
