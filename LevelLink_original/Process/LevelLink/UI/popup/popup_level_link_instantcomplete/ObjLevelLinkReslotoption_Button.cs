using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

/// <summary>
/// 쿨타임 단축 옵션 아이템의 "보석으로 즉시 완료" 하위 UI.
/// - 부모: ObjLevelLinkReslotoption (옵션 타입이 NORMAL일 때만 SetUI + 켜짐, 아니면 Dispose + 꺼짐)
/// - 슬롯 쿨타임 TimeStream("LEVELLINK_SLOT_{gdid}")을 구독해 남은 시간이 줄어들 때마다 필요한 보석 수를 다시 계산해 표시한다.
///   비용 = 남은 초 / CostToTime 을 올림한 값 (CostToTime초마다 보석 1개)
/// - 버튼 클릭 → 그 시점의 비용(TotalCost)을 부모 콜백으로 전달 → 팝업이 보석 사용 확인 팝업을 띄운다.
/// - 이 옵션은 게임 설정값 "levellink_cash_shorten_payment"가 있을 때만 목록에 들어간다(LevelLinkManager.GetShorTimeNow).
/// </summary>
public class ObjLevelLinkReslotoption_Button : BaseUILayout<ObjLevelLinkReslotoption_Button.Setting>
{
    // 즉시 완료(보석 사용) 버튼
    [SerializeField] private Button BtnBuy; //OnClickInstantComplete();
    // 현재 필요한 보석 수 텍스트 (천 단위 콤마)
    [SerializeField] private Text TxtCost;

    // InitUI 중복 실행 방지
    private bool IsInit = false;
    // 남은 시간 TimeStream 구독 핸들
    private SingleAssignmentDisposable TimerDispos = null;

    /// <summary>
    /// 부모 InitUI에서 1회 호출. 버튼 클릭 바인딩 (BUTTONDELAYTIME 동안 연타 방지).
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( IsInit ) return;
        IsInit = true;

        if( BtnBuy != null )
        {
            BtnBuy.BindToOnClick(_=>
            {
                OnClickEvent();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }
    }

    /// <summary>
    /// 데이터가 없으면 숨기고, 있으면 남은 시간 구독(SetTimer)을 시작해 비용을 표시한다.
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();

        if( setting == null )
        {
            SetEnable(false);
            return;
        }

        SetTimer();
    }

    /// <summary>
    /// 보석 비용을 콤마 형식 문자열로 표시.
    /// </summary>
    private void SetTextCost(int cost)
    {
        if( TxtCost == null ) return;
        TxtCost.text = cost.ToCommaString();
    }

    /// <summary>
    /// 남은 시간(초)으로 필요한 보석 수를 계산한다. CostToTime초당 보석 1개, 나머지가 있으면 1개 추가(올림).
    /// 예) CostToTime=60: 남은 125초 → 125/60=2, 나머지 5 → 3개 / 남은 30초 → 나머지 30 → 1개
    /// </summary>
    private int GetCalculateCost(float remainTime)
    {
        int totalCost = 0;
        // 음수(이미 종료)면 0
        if( remainTime < 0 ) return totalCost;

        // 남은 시간이 CostToTime보다 클 때만 몫을 더한다 (float → int 버림 후 정수 나눗셈)
        if( remainTime > setting.CostToTime)
        {
            totalCost = (int)remainTime / setting.CostToTime;
        }
        
        // 나머지 초가 있으면 보석 1개 추가
        // [참고] 남은 시간이 CostToTime과 정확히 같으면 몫도 더해지지 않고(조건이 >) 나머지도 0이라 비용이 0이 된다.
        //        또한 1초 미만은 (int) 변환으로 버려져 0이 되며, CostToTime이 0이면 0으로 나누기 예외가 난다
        int r =  (int)remainTime % setting.CostToTime;
        if( r > 0 )
        {
            totalCost += 1;
        }

        return totalCost;
    }

    /// <summary>
    /// 슬롯 쿨타임 TimeStream을 구독해 매 틱 비용을 다시 계산/표시한다.
    /// 쿨타임이 끝나면 비용 0을 표시하고 구독을 해제한다.
    /// </summary>
    private void SetTimer()
    {
        // 타이머 키가 없으면 비용을 계산할 수 없으므로 종료
        if( setting.STR_TIMER_NAME.IsNullOrEmpty()) return;

        string RemainTimeKey = setting.STR_TIMER_NAME;
        // LevelLink 페이지 타이머가 이미 돌리고 있는 같은 키의 TimeStream을 가져온다 (없으면 새 객체)
        TimeStreamManager.Instance.GetTimeStream(out TimeStream timeStream, RemainTimeKey);

        // [참고] 기존 TimerDispos를 해제하지 않고 새로 만든다. 스크롤 아이템 재활용 등으로 SetUI가 다시 불리면 이전 구독이 남을 수 있다
        TimerDispos = new SingleAssignmentDisposable();
        var timeStreamData = timeStream.GetTimeStreamData();

        // 시작 시간은 TimeStream의 현재값만 사용한다 (서버 remain_time을 받지 않음)
        // [참고] 이 함수는 팝업 OnAwake(Bottom.InitUI) 시점에 실행되어 팝업 MidUI 타이머보다 먼저 돈다.
        //        해당 키의 TimeStream이 아직 없으면 새 객체라 CurrentTime이 0 → 처음엔 비용 0이 표시되고,
        //        이후 MidUI가 같은 스트림을 서버 남은 시간으로 재설정해야 다음 틱부터 정상 비용이 표시된다
        float startTime = timeStreamData.CurrentTime;

        // 구독 전에 현재 비용을 먼저 한 번 표시
        setting.TotalCost = GetCalculateCost(startTime);
        SetTextCost(setting.TotalCost);


        // SetTime: 같은 TimeStream 데이터를 현재값 기준으로 재설정(감소 모드) — 페이지/팝업 타이머와 공유되는 스트림
        // OnTimeStreamObservable: 매니저 등록 + 매 틱 TimeStreamData 발행
        TimerDispos.Disposable = timeStream
        .SetTime(RemainTimeKey, startTime, 0, TimeStreamType.DECREASE)
        .OnTimeStreamObservable()
        .Subscribe(streamData =>
        {
            // 남은 시간이 줄 때마다 비용 갱신 (클릭 시 이 TotalCost가 전달된다)
            setting.TotalCost = GetCalculateCost(streamData.CurrentTime);
            SetTextCost(setting.TotalCost);

            // 쿨타임 종료 → 비용 0 표시 후 구독 해제
            if (streamData.IsEnd)
            {
                SetTextCost(0);
                TimerDispose();
            }
        })
        .AddTo(this);
    }    
    /// <summary>
    /// TimeStream 구독 해제 (TimeStream 자체는 해제하지 않음).
    /// </summary>
    private void TimerDispose()
    {
        if (TimerDispos != null)
        {
            TimerDispos.Dispose();
            TimerDispos = null;
        }
    } 

    /// <summary>
    /// 즉시 완료 버튼 클릭. 현재 계산된 보석 비용(TotalCost)을 부모(ObjLevelLinkReslotoption.OnClickEvent(int))로 전달한다.
    /// </summary>
    private void OnClickEvent()
    {
        if( setting.OnClickEvent != null )
        {
            setting.OnClickEvent(setting.TotalCost);
        }
    }

    /// <summary>
    /// 부모가 다른 옵션 타입으로 세팅할 때 호출. TimeStream 구독 해제.
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        TimerDispose();
    }

    /// <summary>
    /// 보석 즉시 완료 입력 데이터. ObjLevelLinkReslotoption.SetUIDataNormal에서 생성된다.
    /// </summary>
    public class Setting
    {
        // 보석 1개당 단축되는 초 (게임 설정 "levellink_cash_shorten_payment" 값)
        public int CostToTime; // value 초당 1 캐쉬
        // 현재 남은 시간 기준 필요 보석 수 (SetTimer에서 매 틱 갱신)
        public int TotalCost = 0;
        // 버튼 클릭 시 TotalCost를 넘겨 호출하는 부모 콜백
        public Action<int> OnClickEvent;
        // 슬롯 쿨타임 TimeStream 키 ("LEVELLINK_SLOT_{gdid}")
        public string STR_TIMER_NAME;
    }

}
