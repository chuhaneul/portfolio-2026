using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

/// <summary>
/// 덱 편집 팝업(PopupLevelLinkDeckManage)의 타이틀 영역
/// - 닫기 버튼 / 튜토리얼(도움말) 버튼만 있으며, 클릭 처리는 Setting 콜백으로 부모 팝업에 넘긴다
/// - SetUI 는 오버라이드하지 않아 BaseUILayout 의 빈 SetUI 가 호출된다
/// </summary>
public class ObjLevelLinkDeck_TitleUI : BaseUILayout<ObjLevelLinkDeck_TitleUI.Setting>
{
    //닫기 버튼 → PopupLevelLinkDeckManage.OnClickEventClose
    [SerializeField] private Button BtnClose;
    //튜토리얼 버튼 → PopupLevelLinkDeckManage.OnClickEventTutorial
    [SerializeField] private Button BtnTutorial;

    /// <summary>
    /// 버튼 바인딩 (부모 InitUI 에서 1회, SetData 보다 먼저 호출됨)
    /// - 콜백은 클릭 시점의 setting 에서 꺼내므로, 바인딩 시점에 setting 이 없어도 된다
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( BtnClose != null )
        {
            //클릭 → 부모 콜백 실행 후 BUTTONDELAYTIME 동안 재클릭 방지
            BtnClose.BindToOnClick(_=>
            {
                setting.OnClickClose.Invoke();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnTutorial != null )
        {
            BtnTutorial.BindToOnClick(_=>
            {
                setting.OnClickTutorial.Invoke();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }
    }


    /// <summary>
    /// 타이틀 영역 입력 데이터 (PopupLevelLinkDeckManage.SetUIData 에서 생성)
    /// </summary>
    public class Setting
    {
        //닫기 클릭 콜백 (변경 사항 확인 후 닫기)
        public Action OnClickClose;
        //튜토리얼 클릭 콜백 (현재는 로그만 출력)
        public Action OnClickTutorial;

    }
}
