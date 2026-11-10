using UnityEngine;
using UnityEngine.UI;
using System;
using UniRx;

/// <summary>
/// 정렬 필터 패널(ObjLevelLinkDeck_MidUI_SortFilter) 하단 버튼 묶음
/// - 확인(BtnOK) / 필터 없음(BtnNofilter) / 전체 선택(BtnAll)
/// - 부모가 SetButtonUIData 로 Setting 을 다시 넘길 때마다 확인 버튼 상태(OKState)가 갱신된다
/// </summary>
public class ObjLevelLinkDeck_MidUI_SortFilterButton : BaseUILayout<ObjLevelLinkDeck_MidUI_SortFilterButton.Setting>
{
    //확인 버튼 (ButtonSetting : Disable 상태에서도 클릭 콜백은 오며, 현재 상태가 인자로 전달된다)
    [SerializeField] private ButtonSetting BtnOK;
    //필터 없음 (모두 해제)
    [SerializeField] private Button BtnNofilter;
    //전체 선택
    [SerializeField] private Button BtnAll;

    //버튼 바인딩 중복 방지
    private bool IsInit = false;

    /// <summary>
    /// 버튼 바인딩 (부모 InitUI 에서 1회)
    /// </summary>
    public override void InitUI()
    {
        if( IsInit) return;
        IsInit = true;

        base.InitUI();

        if( BtnOK != null)
        {
            //ButtonSetting 내부 버튼 바인딩 / 상태 구독 초기화. 클릭 콜백은 SetData 에서 지정
            BtnOK.Init();
        }

        if( BtnNofilter != null )
        {
            //클릭 시 부모 콜백 호출 후 BUTTONDELAYTIME 동안 재클릭 방지 (AsSingleUnitObservable : 타이머 완료 시 Unit 하나로 끝남)
            BtnNofilter.BindToOnClick(_=>
            {
                if( setting.OnClickEventNofilter != null )
                {
                    setting.OnClickEventNofilter.Invoke();
                }

                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsSingleUnitObservable();

            }).AddTo(this);
        }

        if( BtnAll != null )
        {
            BtnAll.BindToOnClick(_=>
            {
                if( setting.OnClickEventAll != null )
                {
                    setting.OnClickEventAll.Invoke();
                }
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsSingleUnitObservable();

            }).AddTo(this);
        }
    }

    /// <summary>
    /// 부모(ObjLevelLinkDeck_MidUI_SortFilter.SetButtonUIData)가 호출. 확인 버튼 클릭 콜백을 매번 새로 지정한다
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);
        
        if( BtnOK != null )
        {
            //state : 클릭 시점의 버튼 상태
            BtnOK.SetOnClickButton = (state)=>
            {
                //활성(None) 상태일 때만 확인 처리. Disable(필터 결과 0장)이면 아무 것도 하지 않는다
                if( state == Button_State.None )
                {
                    if( setting.OnClickEventOK != null )
                    {
                        setting.OnClickEventOK.Invoke();
                    }
                }
                else
                {
                    //안내 메세지가 있다면, 메세지를 보여주기로 한다
                }
            };

        }
    }

    /// <summary>
    /// 확인 버튼 상태(활성 / 비활성 표시) 반영
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();
        if( BtnOK != null )
        {
            BtnOK.SetState(setting.OKState);
        }
    }


    /// <summary>
    /// 필터 버튼 묶음 입력 데이터
    /// </summary>
    public class Setting
    {
        //확인 버튼 상태 (None = 활성, Disable = 비활성 표시)
        public Button_State OKState =  Button_State.None;

        //확인 → ObjLevelLinkDeck_MidUI_SortFilter.OnClickEventOK
        public Action OnClickEventOK;
        //필터 없음 → OnClickEventNoFilter
        public Action OnClickEventNofilter;
        //전체 선택 → OnClickEventAll
        public Action OnClickEventAll;


    }
}
