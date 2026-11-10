using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UnityEngine.UI;
using System;

/// <summary>
/// 덱 편집 팝업 중단의 버튼 영역
/// - 부모 : ObjLevelLinkDeck_MidUI
/// - BtnOK(저장) : 모든 슬롯이 찼을 때만 활성(None). 활성일 때만 PopupLevelLinkDeckManage.OnClickEventOK 호출
/// - BtnGoTop(맨 위로) : 카드 스크롤을 첫 줄로 이동
/// </summary>
public class ObjLevelLinkDeck_MidUI_Button : BaseUILayout<ObjLevelLinkDeck_MidUI_Button.Setting>
{
    //저장 버튼 (ButtonSetting : Disable 상태여도 클릭 콜백이 오고, 현재 상태가 인자로 전달된다)
    [SerializeField] private ButtonSetting BtnOK;
    //스크롤 맨 위로 버튼
    [SerializeField] private Button BtnGoTop;

    /// <summary>
    /// 버튼 바인딩 (부모 InitUI 에서 1회)
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( BtnOK != null )
        {
            BtnOK.Init();
            //클릭 시 OnClickEventOK(현재 버튼 상태) 호출
            BtnOK.SetOnClickButton = OnClickEventOK;
        }

        if( BtnGoTop !=  null )
        {
            //클릭 → 부모 콜백 후 BUTTONDELAYTIME 동안 재클릭 방지
            BtnGoTop.BindToOnClick(_=>
            {
                if( setting.OnClickEventGoTop != null )
                {
                    setting.OnClickEventGoTop.Invoke();
                }

                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsSingleUnitObservable();

            }).AddTo(this);
        }
    }

    /// <summary>
    /// 최초 화면 구성 : 저장 버튼 상태 반영
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();
        SetStateBtnOK();
    }

    /// <summary>
    /// 슬롯 변경 후 갱신 : 저장 버튼 상태 반영
    /// </summary>
    public override void UpdateUI()
    {
        base.UpdateUI();
        SetStateBtnOK();
    }
    
    /// <summary>
    /// Setting.State(None = 활성, Disable = 비활성 표시)를 저장 버튼에 반영
    /// </summary>
    private void SetStateBtnOK()
    {

        if( BtnOK != null )
        {
            BtnOK.SetState(setting.State);
        }
    }

    /// <summary>
    /// 저장 버튼 클릭. 활성(None) 상태일 때만 부모 콜백을 호출하고, 빈 슬롯이 있는 Disable 상태에서는 무시한다
    /// </summary>
    private void OnClickEventOK(Button_State state)
    {
        if( state == Button_State.None )
        {
            if( setting.OnClickEventOK != null )
            {
                setting.OnClickEventOK.Invoke();
            }
        }
    }

    /// <summary>
    /// 버튼 영역 입력 데이터 (ObjLevelLinkDeck_MidUI.SetData 에서 생성)
    /// </summary>
    public class Setting
    {
        //저장 버튼 상태 (모든 슬롯이 차 있으면 None, 아니면 Disable)
        public Button_State State = Button_State.None;

        //저장 → PopupLevelLinkDeckManage.OnClickEventOK
        public Action OnClickEventOK;
        //맨 위로 → ObjLevelLinkDeck_MidUI.OnClickEventGoTop
        public Action OnClickEventGoTop;
        
    }
}