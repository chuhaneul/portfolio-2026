using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using Game.RestAPI;

/// <summary>
/// 쿨타임 단축 팝업 하단 스크롤의 옵션 아이템 1개 (스크롤 프리팹의 루트 컴포넌트).
/// - 부모: ObjLevelLinkInstantcomplete_Bottom (OnGetItemByIndex에서 InitUI 1회 → SetData → SetUI)
/// - 자식: 옵션 타입별 하위 UI 3종을 모두 가지고 있고, 타입에 맞는 하나만 켜고 나머지는 끈다.
///   NORMAL → ObjLevelLinkReslotoption_Button (보석으로 즉시 완료, 남은 시간에 따라 비용이 실시간 변함)
///   AD     → ObjLevelLinkReslotoption_AD     (광고 시청으로 시간 단축)
///   TICKET → ObjLevelLinkReslotoption_Ticket (보유 단축 티켓 사용)
/// - 하위 UI에서 실행 이벤트가 오면 Setting.OnClickEventButton(= Bottom → 팝업)으로 옵션 데이터를 올려 보낸다.
/// </summary>
public class ObjLevelLinkReslotoption : BaseUILayout<ObjLevelLinkReslotoption.Setting>
{

    // 옵션 제목/설명 텍스트. NORMAL(즉시 완료) 타입일 때만 켜진다 (광고/티켓은 각 하위 UI가 자체 텍스트를 가짐)
    [SerializeField] private Text TxtTitle;
    [SerializeField] private Text TxtDesc;

    // 타입별 하위 UI
    [SerializeField] private ObjLevelLinkReslotoption_Button ButtonUI;
    [SerializeField] private ObjLevelLinkReslotoption_AD ADUI;
    [SerializeField] private ObjLevelLinkReslotoption_Ticket TicketUI;

    /// <summary>
    /// 스크롤 아이템이 처음 생성될 때 1회 호출(Bottom이 IsInitHandlerCalled로 보장). 하위 UI 3종을 모두 초기화한다.
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( ButtonUI != null )
        {
            ButtonUI.InitUI();
        }

        if( ADUI != null )
        {
            ADUI.InitUI();
        }

        if( TicketUI != null )
        {
            TicketUI.InitUI();
        }
    }

    /// <summary>
    /// 옵션 데이터를 저장하고, 옵션 타입에 해당하는 하위 UI에만 Setting을 만들어 넘긴다.
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);
        // setting.UIType은 setting.Data.UIType을 그대로 읽는 프로퍼티
        switch(setting.UIType)
        {
            case SHORT_TIME_UI_TYPE.NORMAL: SetUIDataNormal();  break;
            case SHORT_TIME_UI_TYPE.AD:     SetUIDataAD();      break;
            case SHORT_TIME_UI_TYPE.TICKET: SetUIDataTicket();  break;
        }
    }

    /// <summary>
    /// 보석 즉시 완료 옵션 세팅.
    /// - CostToTime: 게임 설정값 "levellink_cash_shorten_payment"의 정수 값 = 보석 1개당 단축되는 초
    /// - OnClickEvent: int 인자 버전(클릭 시점의 보석 비용을 받음)
    /// - STR_TIMER_NAME: 슬롯 쿨타임 TimeStream 키 → 남은 시간을 구독해 비용을 실시간 계산
    /// </summary>
    private void SetUIDataNormal()
    {
        if( ButtonUI == null ) return;

        ButtonUI.SetData(new ObjLevelLinkReslotoption_Button.Setting()
        {
            CostToTime = setting.ConfigData.ValueToInt,
            OnClickEvent = OnClickEvent,
            STR_TIMER_NAME = setting.Data.STR_TIMER_NAME,
        });
    }

    /// <summary>
    /// 광고 옵션 세팅. 광고 데이터, 광고 타입(AD_TYPE_LEVELLINK_SLOT), 광고 시청 완료 시 호출할 콜백(인자 없는 OnClickEvent)을 넘긴다.
    /// 광고가 끝까지 재생되어야 OnClickEvent가 불리고 그때 단축 API로 이어진다.
    /// </summary>
    private void SetUIDataAD()
    {
        if( ADUI == null ) return;

        ADUI.SetData(new ObjLevelLinkReslotoption_AD.Setting(setting.AdData, AD_TYPE.AD_TYPE_LEVELLINK_SLOT, OnClickEvent));

    }

    /// <summary>
    /// 티켓 옵션 세팅. 보유 티켓 아이템 데이터와 클릭 시 호출할 콜백(인자 없는 OnClickEvent)을 넘긴다.
    /// </summary>
    private void SetUIDataTicket()
    {
        if( TicketUI == null ) return;
        
        TicketUI.SetData(new ObjLevelLinkReslotoption_Ticket.Setting(setting.TicketData, OnClickEvent));

    }


    /// <summary>
    /// 옵션 타입에 맞는 하위 UI만 SetUI + 켜고, 나머지 하위 UI는 Dispose(타이머 구독 해제) 후 끈다.
    /// 스크롤 아이템이 재활용되어 타입이 바뀌어도 이전 타입의 타이머가 남지 않도록 하기 위함.
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();

        // 제목/설명은 일단 끄고, NORMAL 타입일 때만 다시 켠다
        SetEnableTextTitle(false);
        SetEnableTextDesc(false);


        if( ButtonUI != null )
        {
            // 보석 즉시 완료 타입
            if( setting.UIType == SHORT_TIME_UI_TYPE.NORMAL )
            {
                // 남은 시간 TimeStream 구독 시작 → 보석 비용 텍스트 실시간 갱신
                ButtonUI.SetUI();
                ButtonUI.SetEnable(true);

                // "즉시 완료" 제목/설명 표시
                SetTextTitle();
                SetTextDesc();
                SetEnableTextTitle(true);
                SetEnableTextDesc(true);
            }
            else
            {
                // 다른 타입이면 비용 타이머 구독 해제 후 숨김
                ButtonUI.Dispose();
                ButtonUI.SetEnable(false);
            }
        }

        if( ADUI != null )
        {
            // 광고 타입
            if( setting.UIType == SHORT_TIME_UI_TYPE.AD )
            {
                // 광고 재시청 대기시간이 남아 있으면 타이머 표시 + 버튼 Disable 모양, 아니면 남은 횟수 표시
                ADUI.SetUI();
                ADUI.SetEnable(true);

            }
            else
            {
                ADUI.Dispose();
                ADUI.SetEnable(false);
            }
        }

        if( TicketUI != null )
        {
            // 티켓 타입
            if( setting.UIType == SHORT_TIME_UI_TYPE.TICKET )
            {
                // 티켓 이름/아이콘/보유 개수 표시
                TicketUI.SetUI();
                TicketUI.SetEnable(true);
            }
            else
            {
                // Ticket은 Dispose를 재정의하지 않아 base(빈 함수)만 호출된다
                TicketUI.Dispose();
                TicketUI.SetEnable(false);
            }
        }
    }

    /// <summary>
    /// "즉시 완료" 옵션 제목 문자열 표시.
    /// </summary>
    private void SetTextTitle()
    {
        if( TxtTitle == null) return;
        TxtTitle.text = CStringDataManager.Instance.GetStringData("UI_common_right_away");
    }

    /// <summary>
    /// "즉시 완료" 옵션 설명 문자열 표시.
    /// </summary>
    private void SetTextDesc()
    {
        if( TxtDesc == null ) return;
        TxtDesc.text = CStringDataManager.Instance.GetStringData("UI_common_right_away_desc");
    }

    /// <summary>
    /// 제목 텍스트 오브젝트 표시/숨김.
    /// </summary>
    private void SetEnableTextTitle(bool isEnable)
    {
        if( TxtTitle == null) return;
        TxtTitle.gameObject.SetActive(isEnable);

    }
    /// <summary>
    /// 설명 텍스트 오브젝트 표시/숨김.
    /// </summary>
    private void SetEnableTextDesc(bool isEnable)
    {
        if( TxtDesc == null ) return;
        TxtDesc.gameObject.SetActive(isEnable);

    }

    /// <summary>
    /// 보석 즉시 완료 버튼(ObjLevelLinkReslotoption_Button) 클릭 시 호출.
    /// 클릭 시점의 보석 비용(value)을 옵션 데이터의 GemCost에 기록한 뒤 상위로 올려 보낸다.
    /// 팝업은 이 GemCost로 보석 사용 확인 팝업을 띄운다.
    /// </summary>
    private void OnClickEvent(int value)
    {
        if( setting.OnClickEventButton != null )
        {
            // LevelLinkManager의 ShortTimeList 원소를 직접 수정한다 (같은 참조)
            setting.Data.GemCost = value;
            setting.OnClickEventButton(setting.Data);
        }
    }

    /// <summary>
    /// 광고 시청 완료(AD) 또는 티켓 버튼 클릭(TICKET) 시 호출. 옵션 데이터를 그대로 상위로 올려 보낸다.
    /// </summary>
    private void OnClickEvent()
    {
        if( setting.OnClickEventButton != null )
        {
            setting.OnClickEventButton(setting.Data);
        }
    }

    /// <summary>
    /// 옵션 아이템 입력 데이터. ObjLevelLinkInstantcomplete_Bottom.OnGetItemByIndex에서 생성된다.
    /// </summary>
    public class Setting
    {
        // 단축 옵션 데이터 원본 (LevelLinkManager.GetShortTimeData(index))
        public LevelLinkSlot_ShortTime Data;
        
        // 아래는 Data의 필드를 바로 읽는 편의 프로퍼티
        // 옵션 타입 (NORMAL / AD / TICKET)
        public SHORT_TIME_UI_TYPE UIType => Data.UIType;
        // NORMAL일 때만 값이 있음: 보석 즉시 완료 설정값(초당 비용 기준)
        public GameConfigData ConfigData => Data.ConfigData;
        // AD일 때만 값이 있음: 광고 테이블 데이터
        public AdvertiseData AdData => Data.AdData;
        // TICKET일 때만 값이 있음: 보유 티켓 아이템(서버 인벤토리 데이터)
        public ItemList TicketData => Data.TicketData;

        // 옵션 실행 시 상위(Bottom)로 알리는 콜백
        public Action<LevelLinkSlot_ShortTime> OnClickEventButton;
    }

}
