using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

/// <summary>
/// LevelLink에서 사용하는 시간 단축권
/// [상세] LevelLink 슬롯 쿨타임 단축 / 즉시 완료 팝업.
/// - 여는 곳: PageLevelLink.OnClickLevelLinkCard() → 슬롯에 남은 쿨타임(remain_time > 0)이 있을 때
///   OpenPopupLevelLinkInstantCompleteProcess()가 광고 목록 API(ADList)로 남은 광고 횟수를 먼저 갱신하고,
///   LevelLinkManager.InitShortTimeList()로 단축 옵션 목록(광고 / 보석 즉시 완료 / 단축 티켓 3종)을 만든 뒤 이 팝업을 연다.
/// - Setting : 쿨타임 전체 시간(테이블 level_link_cooldown), 남은 시간(서버 remain_time), 슬롯 타이머 키("LEVELLINK_SLOT_{gdid}")
/// - Result  : IsSuccess. 단축 API(ReqLevelLinkReduceTime)가 성공(e == 0)해서 닫힐 때만 true.
///   호출부는 true면 LevelLinkPageUpdate_CoolTime 메시지를 발행해 LevelLink 페이지 UI(타이머 등)를 다시 세팅한다.
/// - 화면 구성: 중간 타이머 영역(MidUI: 남은 시간 슬라이더/텍스트) / 하단 단축 옵션 스크롤(BottomUI) / 닫기·딤 버튼
/// - 쿨타임이 자연 종료되면(타이머가 IsEndTime=true 메시지 발행) 결과 false로 자동으로 닫힌다.
/// </summary>
public class PopupLevelLinkInstantcomplete : CPopupUI<PopupLevelLinkInstantcomplete.Setting, PopupLevelLinkInstantcomplete.Result>
{
    // 남은 쿨타임 표시 영역. LevelLink 페이지에서 쓰는 공용 타이머 UI(ObjLevelLinkSlot_Timer)를 재사용한다
    // (같은 폴더의 ObjLevelLinkInstantcomplete_Mid가 아니라 ObjLevelLinkSlot_Timer 타입임에 주의)
    [SerializeField] private ObjLevelLinkSlot_Timer MidUI;
    // 단축 옵션 목록 스크롤 영역 (광고 / 보석 즉시 완료 / 티켓)
    [SerializeField] private ObjLevelLinkInstantcomplete_Bottom BottomUI;
    // 닫기 / 딤 버튼 — 결과 IsSuccess=false로 닫는다
    [SerializeField] private Button BtnClose;
    [SerializeField] private Button BtnDim;


    /// <summary>
    /// 팝업 생성 직후(BasePopup.Awake) 호출. SetData보다 먼저 실행된다.
    /// 결과 스트림과 기본 결과를 만들고 UI를 초기화한다.
    /// </summary>
    protected override void OnAwake()
    {
        base.OnAwake();

        // 팝업이 닫힐 때(OnClose) result를 호출부에 1회 전달하고 완료되는 Subject
        subject = new Subject<Result>();
        // 기본은 실패. 단축 API 성공 시에만 true로 바뀐다
        result = new Result {IsSuccess = false};

        // 위에 보석 사용 확인/젬 상점 팝업이 떠도 이 팝업은 숨김 연출을 하지 않는다
        Flags = PopupFlags.DISABLE_HIDE;

        InitUI();
    }    

    /// <summary>
    /// 버튼 바인딩, 하위 영역 초기화, 쿨타임 종료 메시지 구독.
    /// (Isinit 가드는 없지만 OnAwake에서 한 번만 호출된다)
    /// </summary>
    private void InitUI()
    {
        if( BtnClose != null )
        {
            // 클릭 시 닫고, BUTTONDELAYTIME 동안 버튼을 막아 연타를 방지
            BtnClose.BindToOnClick(_=>
            {
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnDim != null )
        {
            BtnDim.BindToOnClick(_=>
            {
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( MidUI != null )
        {
            // ObjLevelLinkSlot_Timer는 InitUI를 재정의하지 않으므로 실질적으로 하는 일은 없다
            MidUI.InitUI();
        }

        if( BottomUI != null )
        {
            // 이 시점에 스크롤 리스트가 생성되고 각 옵션 아이템의 SetData/SetUI까지 실행된다
            // (옵션 목록은 팝업을 열기 전에 LevelLinkManager.InitShortTimeList()로 이미 만들어져 있음)
            BottomUI.InitUI();
        }

        // LevelLink 쿨타임 갱신 메시지 구독
        // - ObjLevelLinkSlot_Timer가 카운트다운 종료 시 IsEndTime=true로 발행한다 (이 팝업의 MidUI 포함)
        // - 쿨타임이 끝났으면 더 단축할 것이 없으므로 결과 false로 팝업을 닫는다
        // - AddTo(this): 팝업 파괴 시 구독 해제
        MessageBroker.Default.Receive<LevelLinkPageUpdate_CoolTime>().Subscribe(res=>
        {
            if( res.IsEndTime )
            {
                Close();
            }
            
        }).AddTo(this);

    
    }

    /// <summary>
    /// 팝업 데이터 세팅. PageLevelLink.OpenPopupLevelLinkInstantComplete()에서 팝업 생성 직후 호출된다.
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);

        SetUIData();
        SetUI();

    }

    /// <summary>
    /// 중간 타이머 영역과 하단 옵션 영역에 넘길 Setting을 만든다.
    /// </summary>
    private void SetUIData()
    {
        if( MidUI != null )
        {
            // 타이머: 남은 시간 / 전체 시간(슬라이더 최대값) / 슬롯 타이머 키
            // 같은 키의 TimeStream이 이미 돌고 있으면 ObjLevelLinkSlot_Timer는 그 현재값을 이어서 사용한다
            MidUI.SetData(new ObjLevelLinkSlot_Timer.Setting()
            {
                RemainTime = setting.RemainTime,
                TotalTime = setting.TotalTime,
                STR_TIMER_NAME = setting.STR_TIMER_NAME,
                
            });
        }

        if( BottomUI != null )
        {
            // 하단 옵션 클릭 시 실행할 콜백만 넘긴다 (옵션 데이터는 Bottom이 LevelLinkManager에서 직접 가져감)
            // [참고] 옵션 아이템들은 OnAwake의 BottomUI.InitUI()에서 이미 그려지므로, 그 시점에는 이 Setting이 아직 없다.
            //        클릭은 SetData 이후에 일어나므로 실제 동작에는 문제가 없다
            BottomUI.SetData(new ObjLevelLinkInstantcomplete_Bottom.Setting()
            {
                OnClickScrollItem = OnClickEventScroll,
            });
        }
    }

    /// <summary>
    /// 타이머를 시작하고 하단 영역을 갱신한다.
    /// </summary>
    private void SetUI()
    {
        if( MidUI != null )
        {
            // 슬라이더 최대값 설정 + 슬롯 타이머 키로 카운트다운 구독 시작
            MidUI.SetUI();
        }

        if( BottomUI != null )
        {
            // Bottom.SetUI는 base 호출만 있어 실질 동작 없음
            BottomUI.SetUI();
        }
    }

    /// <summary>
    /// 하단 옵션 중 하나가 "실행"되었을 때 호출되는 콜백.
    /// 호출 경로: 옵션 버튼 → ObjLevelLinkReslotoption.OnClickEvent → ObjLevelLinkInstantcomplete_Bottom.OnClickEventButton → 여기.
    /// - AD    : 광고 시청이 성공한 뒤에 호출된다(ObjLevelLinkReslotoption_AD가 광고 완료 콜백으로 연결) → 바로 단축 API 호출
    /// - TICKET: 티켓 버튼 클릭 즉시 호출 → 확인 팝업 없이 바로 단축 API 호출
    /// - NORMAL: 보석 즉시 완료. 보석 사용 확인 팝업을 먼저 띄우고 확인 시 단축 API 호출
    /// API 성공 시 result.IsSuccess = true 로 바꾸고 팝업을 닫는다. 실패/취소 시 팝업은 그대로 유지된다.
    /// </summary>
    private void OnClickEventScroll(LevelLinkSlot_ShortTime data)
    {
        CDebug.Log($"OnClickEventScroll : {data.UIType}");

        //API 호출 후 팝업을 닫는다
        // 단축 대상 슬롯 = 현재 LevelLink 페이지에서 보고 있는 슬롯
        int gdid = LevelLinkManager.Instance.CurrentPageGDID;
        // 서버 파라미터: 사용한 아이템 ID(보석 즉시 완료는 1), 광고 ID. 사용하지 않는 쪽은 0
        int itemID = 0;
        int adID = 0;

        // 옵션 타입별 서버 파라미터 결정
        switch(data.UIType)
        {
            // 광고: 광고 테이블 ad_id가 아니라 AD_TYPE 열거값(AD_TYPE_LEVELLINK_SLOT)을 정수로 보낸다
            case SHORT_TIME_UI_TYPE.AD:
                adID = AD_TYPE.AD_TYPE_LEVELLINK_SLOT.ToInt32();
            break;
            // 티켓: 보유 아이템 데이터의 gdid(아이템 ID)를 보낸다
            case SHORT_TIME_UI_TYPE.TICKET:
                itemID = data.TicketData.gdid;
            break;
            // 보석 즉시 완료: itemID = 1 이 "보석 사용 즉시 완료"를 의미 (LevelLinkService도 itemID == 1이면 즉시완료 로그를 남김)
            case SHORT_TIME_UI_TYPE.NORMAL: //보석 사용
                itemID = 1;
            break;
        }

        if( data.UIType == SHORT_TIME_UI_TYPE.NORMAL )
        {
            // 보석 즉시 완료 흐름
            SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
            // 1) data.GemCost: 클릭 시점의 남은 시간으로 계산된 보석 비용 (ObjLevelLinkReslotoption.OnClickEvent(int)에서 채워짐)
            //    보유 보석이 부족하면 "재화 부족 → 젬 상점" 흐름으로 가고 IsOK=false가 온다
            disposable.Disposable = LevelLinkManager.Instance.OpenPopupUseCash(data.GemCost)
            // 2) SelectMany: 확인(IsOK)이면 단축 API Observable로 이어 붙이고, 아니면 false를 바로 흘린다
            .SelectMany(res =>
            {
                if( res.IsOK )
                {
                    // 서버 응답 처리(LevelLinkService): LevelLink 목록 갱신 + 남은 시간이 0 이하면 슬롯 TimeStream 제거
                    return LevelLinkManager.Instance.ReqLevelLinkReduceTime(gdid, itemID, adID);
                }
                
                return Observable.Return(false);
            })
            // 3) 결과 처리 후 직접 Dispose (AddTo가 없으므로 팝업 수명과 무관하게 응답까지 유지된다)
            .Subscribe(res=>
            {
                if( res == true )
                {
                    result.IsSuccess = true;
                    Close();
                }

                disposable.Dispose();

            });
            
        }
        else
        {
            // 광고(시청 완료 후) / 티켓: 확인 단계 없이 바로 단축 API 호출
            SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
            disposable.Disposable = LevelLinkManager.Instance.ReqLevelLinkReduceTime(gdid, itemID, adID)
            .Subscribe(res=>
            {
                // 서버 e == 0 이면 성공 → 결과 true로 닫기
                if( res == true )
                {
                    result.IsSuccess = true;
                    Close();
                }

                disposable.Dispose();

            });
        }
    }
    

    /// <summary>
    /// 팝업 입력 데이터. PageLevelLink.OpenPopupLevelLinkInstantComplete()에서 생성된다.
    /// </summary>
    public class Setting
    {
        // 남은 쿨타임(초). 서버 LevelLink 데이터의 remain_time
        public float RemainTime;
        // 쿨타임 전체 시간(초). 테이블 level_link_cooldown — 타이머 슬라이더 최대값으로 사용
        public float TotalTime;

        // 슬롯 쿨타임 TimeStream 키. "LEVELLINK_SLOT_{gdid}" 형식 (LevelLinkManager.LEVELLINK_SLOT_TIME_KEY_FORMAT)
        // [참고] 아래 줄 끝의 기존 주석("value 초당 1 캐쉬")은 내용상 ObjLevelLinkReslotoption_Button.Setting.CostToTime 설명으로 보인다
        public string STR_TIMER_NAME;        // value 초당 1 캐쉬
    }

    /// <summary>
    /// 팝업 결과. 닫힐 때 호출부로 1회 전달된다.
    /// </summary>
    public class Result
    {
        // true: 단축 API 성공 후 닫힘 → 호출부가 LevelLinkPageUpdate_CoolTime을 발행해 페이지 갱신
        // false: 닫기/딤/백키, 또는 쿨타임 자연 종료로 닫힘
        public bool IsSuccess = false;
    }
}
