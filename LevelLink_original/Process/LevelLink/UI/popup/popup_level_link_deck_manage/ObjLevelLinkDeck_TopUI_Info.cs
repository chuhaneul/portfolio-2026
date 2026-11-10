using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UniRx;


/// <summary>
/// 덱 편집 팝업 상단(ObjLevelLinkDeck_TopUI)의 정보 영역
/// - LevelLink 레벨 표시 : 왼쪽 = 기존(서버) 레벨, 오른쪽 = 현재 편집 중 배치 기준 레벨
/// - 자동 배치 버튼 / 카드 관리(카드 인벤토리 이동) 버튼 → Setting 콜백으로 부모 팝업에 전달
/// </summary>
public class ObjLevelLinkDeck_TopUI_Info : BaseUILayout<ObjLevelLinkDeck_TopUI_Info.Setting>
{
    //자동 배치 버튼
    [SerializeField] private Button BtnAuto;
    //카드 관리(카드 인벤토리) 버튼
    [SerializeField] private Button BtnCardInven;
    //기존 LevelLink 레벨 텍스트
    [SerializeField] private Text TxtLevelLeft;
    //현재 배치 기준 LevelLink 레벨 텍스트
    [SerializeField] private Text TextLevelRight;

    /// <summary>
    /// 버튼 바인딩 (ObjLevelLinkDeck_TopUI.InitUI 에서 1회)
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( BtnAuto != null )
        {
            //클릭 → 부모 콜백 후 BUTTONDELAYTIME 동안 재클릭 방지
            BtnAuto.BindToOnClick(_=>
            {
                if( setting.OnClickAutoEvent != null )
                {
                    setting.OnClickAutoEvent.Invoke();
                }
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsSingleUnitObservable();

            }).AddTo(this);
        }

        if( BtnCardInven != null )
        {
            BtnCardInven.BindToOnClick(_=>
            {
                if( setting.OnClickCardInvenEvent != null )
                {
                    setting.OnClickCardInvenEvent.Invoke();
                }
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsSingleUnitObservable();

            }).AddTo(this);
        }

    }

    /// <summary>
    /// 레벨 텍스트 갱신 (ObjLevelLinkDeck_TopUI 의 SetUI / UpdateUI 에서 호출)
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();
        SetOriginalLevelLinkLevel();
        SetCurrentLevelLinkLevel();

    }

    //왼쪽 : 기존 LevelLink 레벨
    private void SetOriginalLevelLinkLevel()
    {
        if( TxtLevelLeft != null )
        {
            TxtLevelLeft.text = setting.OriginLevel.ToString();
        }
    }

    //오른쪽 : 현재 편집 중 배치 기준 레벨
    private void SetCurrentLevelLinkLevel()
    {
        if( TextLevelRight != null )
        {
            TextLevelRight.text = setting.CurrentLevel.ToString();
        }
    }


    /// <summary>
    /// 정보 영역 입력 데이터 (ObjLevelLinkDeck_TopUI.SetData 에서 생성)
    /// </summary>
    public class Setting
    {
        //기존(서버) LevelLink 레벨
        public int OriginLevel;
        //현재 배치 기준 레벨 (ObjLevelLinkDeck_TopUI.Setting.GetCurrentLevelLinkLevel)
        public int CurrentLevel;

        //자동 배치 클릭 콜백
        public Action OnClickAutoEvent;
        //카드 관리 클릭 콜백
        public Action OnClickCardInvenEvent;
        

    }
}
