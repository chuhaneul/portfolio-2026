using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

/// <summary>
/// LevelLink 쿨타임 타이머 UI (슬라이더 + "HH:MM:SS" 텍스트). TimeStreamManager 의 TimeStream 을 구독해서 1초 단위로 감소 표시한다.
/// 사용처(부모):
///  - PageLevelLink_Mid_TopUI.CoolTimeUI : 중단의 큰 쿨타임 표시 (현재 LevelLink)
///  - ObjLevelLinkSlot.TimerUI               : 하단 슬롯 목록의 미니 타이머 (각 LevelLink)
///  - PopupLevelLinkInstantcomplete.MidUI     : 즉시 완료 팝업의 타이머
/// 세 곳 모두 키 "LEVELLINK_SLOT_{gdid}" 를 쓰므로, 같은 LevelLink이면 TimeStreamManager 안의 같은 TimeStream 을 공유한다.
/// 자식 레이아웃 없음. Setting 은 위 부모들이 SetData 로 넘긴다.
/// </summary>
public class ObjLevelLinkSlot_Timer : BaseUILayout<ObjLevelLinkSlot_Timer.Setting>
{
    // 남은 시간 슬라이더 (maxValue = 전체 쿨타임, value = 남은 시간)
    [SerializeField] private Slider SliderUI;
    // 남은 시간 텍스트 (HH:MM:SS)
    [SerializeField] private Text TxtTime;

    // 현재 TimeStream 구독 핸들. TimerDispose 에서 해제 후 null 로 비운다
    private SingleAssignmentDisposable TimerDispos = null;

    /// <summary>
    /// 타이머 시작/재시작. 부모가 SetData 직후 호출한다.
    /// 1) IsReset 이면 기존 구독 해제
    /// 2) IsDisposeTimer 이면 TimeStreamManager 에서 기존 TimeStream 자체를 제거 (남은 시간이 서버에서 바뀐 경우 새 값으로 다시 시작하기 위함)
    /// 3) 슬라이더 최대값 설정 후 SetTimer 로 구독 시작
    /// </summary>
    public override void SetUI()
    {
        if( setting.IsReset )
        {
            TimerDispose();
        }

        if( setting.IsDisposeTimer )
        {
            string RemainTimeKey = setting.STR_TIMER_NAME;

            // 제거하면 TimeStream.Dispose 로 내부 Subject 참조가 끊겨, 같은 스트림을 구독하던 다른 UI 도 더 이상 값을 받지 못한다
            // (하단 슬롯 타이머는 이어지는 BottomUI.UpdateUI 에서 새 스트림으로 다시 구독됨)
            if( TimeStreamManager.Instance.HasTimeStream(RemainTimeKey))
            {
                TimeStreamManager.Instance.RemoveTimeStream(RemainTimeKey);
            }
        }

        InitSlider();
        SetTimer();
    }

    // 슬라이더 최대값 = 전체 쿨타임(초)
    private void InitSlider()
    {
        if( SliderUI != null )
        {
            SliderUI.maxValue = setting.TotalTime;
        }
    }

    // 슬라이더 값 = 남은 시간(초) → 시간이 줄수록 게이지가 줄어든다
    private void SetSlider(float remainTime)
    {
        if( SliderUI != null )
        {
            SliderUI.value = remainTime;
        }
    }

    // 남은 시간(초)을 "HH:MM:SS" 형식으로 표시
    private void SetText(float remainTime)
    {
        if( TxtTime != null )
        {
            TxtTime.text = Utility.GetTimeFormatHHMMSS((long)remainTime);
        }
    }

    /// <summary>
    /// TimeStream 을 가져와(없으면 새로 생성) 감소 타이머로 설정하고 구독한다.
    /// TimeStreamManager 는 100ms 간격 Interval 로 등록된 스트림들의 시간을 줄이고, 스트림은 매 틱마다 OnNext 로 데이터를 낸다.
    /// (CurrentTime 은 초 단위로 내림된 값)
    /// </summary>
    private void SetTimer()
    {
        // 이전 구독 해제 (IsReset 여부와 관계없이 항상)
        TimerDispose();

        // 남은 시간이 0 이면 타이머를 돌리지 않는다 (슬라이더/텍스트도 갱신하지 않음)
        if( setting.RemainTime == 0) return;

        string RemainTimeKey = setting.STR_TIMER_NAME;
        // 같은 키의 스트림이 이미 등록되어 있으면 그것을, 없으면 새 TimeStream 인스턴스를 out 으로 받는다 (새 것은 아직 매니저에 미등록)
        TimeStreamManager.Instance.GetTimeStream(out TimeStream timeStream, RemainTimeKey);

        TimerDispos = new SingleAssignmentDisposable();

        // SetTime 으로 덮어쓰기 전의 현재 스트림 상태 (새 스트림이면 모든 값이 0)
        var timeStreamData = timeStream.GetTimeStreamData();

        // 시작 시간: 기본은 Setting 의 남은 시간(서버 값).
        // 이미 돌고 있는 스트림이 있으면 그 진행 중인 값을 이어받아, 다른 UI 와 같은 시간을 표시하게 한다.
        float startTime = setting.RemainTime;
        if( timeStreamData.CurrentTime > 0 )
        {
            startTime = timeStreamData.CurrentTime;
        }

        // 초기 슬라이더 값 (새 스트림이면 0 → 첫 틱에서 실제 값으로 갱신됨. 텍스트는 첫 틱까지 이전 값 유지)
        SetSlider(timeStreamData.CurrentTime);

        // Rx 체인:
        //  SetTime(키, 시작, 끝=0, 감소) : 스트림 데이터를 startTime 부터 0 까지 감소하도록 (재)설정하고 자기 자신을 반환
        //  OnTimeStreamObservable()     : 스트림을 매니저에 등록(이미 있으면 유지)하고 매니저 Interval 을 시작, 내부 Subject 를 Observable 로 반환
        //  Subscribe                    : 틱마다 슬라이더/텍스트 갱신, IsEnd 가 오면 종료 처리
        // 완료 시점: 0 에 도달하면 스트림이 IsEnd 데이터를 OnNext 한 뒤 스스로 Remove(OnCompleted) 한다.
        // 해제 시점: TimerDispose(재설정/종료/부모의 Dispose 호출) 또는 AddTo(this) 로 이 GameObject 파괴 시.
        TimerDispos.Disposable = timeStream
        .SetTime(RemainTimeKey, startTime, 0, TimeStreamType.DECREASE)
        .OnTimeStreamObservable()
        .Subscribe(streamData =>
        {
            SetSlider(streamData.CurrentTime);
            SetText(streamData.CurrentTime);

            // 쿨타임 종료
            if (streamData.IsEnd)
            {
                // 1) 매니저의 서버 데이터 remain_time 을 0 으로 → GetStateLevelLink 이 더 이상 COOLTIME 을 반환하지 않음
                LevelLinkManager.Instance.UpdateLevelLinkListData_RemainTime_TimeKey(RemainTimeKey, 0);
                // 2) 스트림을 매니저에서 제거
                TimeStreamManager.Instance.RemoveTimeStream(RemainTimeKey);
                // 3) 페이지 갱신 요청 → PageLevelLink 이 중단/하단을 다시 그림
                // [참고] 같은 키를 중단 타이머와 하단 슬롯 타이머(및 즉시 완료 팝업)가 함께 구독하므로,
                //        종료 시 구독자마다 이 블록이 실행되어 LevelLinkPageUpdate_CoolTime 이 중복 발행될 수 있음
                MessageBroker.Default.Publish(new LevelLinkPageUpdate_CoolTime(true));

                // 4) 자신의 구독 해제
                TimerDispose();
            }
        })
        .AddTo(this);
    }

    // 현재 구독을 해제하고 핸들을 비운다 (TimeStream 자체는 매니저에 남겨둠 → 다른 UI/재진입 시 이어서 사용 가능)
    private void TimerDispose()
    {
        if (TimerDispos != null)
        {
            TimerDispos.Dispose();
            TimerDispos = null;
        }
    }

    /// <summary>
    /// 외부에서 타이머 구독을 끊을 때 호출. (PageLevelLink_Mid_TopUI.SetUI 에서 쿨타임이 아닐 때)
    /// TimeStream 은 제거하지 않는다.
    /// </summary>
    public override void Dispose()
    {
        TimerDispose();
        base.Dispose();
    }

    /// <summary>
    /// 타이머 데이터. 부모(Mid_TopUI / ObjLevelLinkSlot / PopupLevelLinkInstantcomplete)가 생성.
    /// </summary>
    public class Setting
    {
        // true 면 SetUI 시작 시 기존 구독 해제 (SetTimer 도 항상 해제하므로 사실상 같은 결과)
        public bool IsReset = false;
        // true 면 같은 키의 기존 TimeStream 을 매니저에서 제거한 뒤 RemainTime 으로 새로 시작
        public bool IsDisposeTimer = false;
        // 남은 시간(초). 이미 진행 중인 스트림이 없을 때의 시작값. 0 이면 타이머를 시작하지 않음
        public float RemainTime;
        // 전체 쿨타임(초) - 슬라이더 최대값
        public float TotalTime;

        // TimeStream 키 ("LEVELLINK_SLOT_{gdid}", LevelLinkManager.LEVELLINK_SLOT_TIME_KEY_FORMAT)
        public string STR_TIMER_NAME;

    }

}
