using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

/// <summary>
/// 쿨타임 단축 옵션 아이템의 "광고 시청" 하위 UI.
/// - 부모: ObjLevelLinkReslotoption (옵션 타입이 AD일 때만 SetUI + 켜짐, 아니면 Dispose + 꺼짐)
/// - 광고 재시청 대기시간이 남아 있으면 남은 시간 카운트다운을 보여주고 버튼을 Disable 모양으로,
///   대기시간이 없으면 "남은 횟수/전체 횟수"를 보여주고 버튼을 활성 모양으로 둔다.
/// - 버튼 클릭(활성 상태) → 보상형 광고 재생 → 시청 성공 시 Setting.OnCompleteShowAd 호출
///   → ObjLevelLinkReslotoption.OnClickEvent() → 팝업에서 광고 단축 API 호출.
/// - 이 옵션은 LevelLinkManager.GetShortTimeAD()에서 광고 사용 가능 + 남은 횟수 > 0 일 때만 목록에 들어간다.
/// </summary>
public class ObjLevelLinkReslotoption_AD : BaseUILayout<ObjLevelLinkReslotoption_AD.Setting>
{
    // 광고 옵션 제목 / 설명 (광고 보상 값 ad_rwd_value를 문자열에 끼워 넣음)
    [SerializeField] private Text TxtAdTitle;
    [SerializeField] private Text TxtAdDesc;

    [Header("버튼")]
    // 광고 보기 버튼 (Disable 상태는 표시만 바뀌고, 클릭 시 OnClickButton에서 state로 걸러낸다)
    [SerializeField] private ButtonSetting BtnClick;
    // 남은 광고 횟수 "남은/전체"
    [SerializeField] private Text TxtCount;
    // 광고 재시청 대기 남은 시간 (HH:MM:SS)
    [SerializeField] private Text TxtTime;
    // 버튼 아이콘 (광고 스킵 이용권 보유 여부에 따라 바뀜)
    [SerializeField] private Image ImgIcon;

    // 재시청 대기 타이머 구독 핸들 (Dispose 시 해제)
    private SingleAssignmentDisposable TimerDispos = null;
    // InitUI 중복 실행 방지
    private bool IsInit = false;

    /// <summary>
    /// 부모 InitUI에서 1회 호출. 광고 버튼 클릭 콜백을 연결한다.
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( IsInit ) return;
        IsInit = true;

        if( BtnClick != null )
        {
            BtnClick.Init();
            // 클릭 시 ButtonSetting의 현재 상태(None/Disable)를 인자로 OnClickButton 호출
            BtnClick.SetOnClickButton = OnClickButton;
        }
    }

    /// <summary>
    /// 광고 옵션 화면 갱신. 대기시간 유무에 따라 타이머 또는 남은 횟수를 표시하고, 아이콘/제목/설명을 세팅한다.
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();

        // 광고 테이블 데이터가 없으면 이 UI를 숨김
        if( setting.AdvertiseData == null)
        {
            SetEnable(false);
            return;
        }

        SetEnable(true);

        // RemainTime: 서버가 준 광고 재시청 가능 시각 - 현재 서버 보정 시각 (초)
        if( setting.RemainTime > 0 )
        {
            // 아직 대기 중 → 카운트다운 시작, 버튼은 Disable 모양
            SetTimer(); 
            BtnClick.SetState(Button_State.Disable);
        }
        else
        {
            // 바로 시청 가능 → 남은 횟수 표시, 버튼 활성 모양
            SetAdCountText();
            BtnClick.SetState(Button_State.None);
        }

        SetIcon();

        SetTextTitle();
        SetTextDesc();
    }

    /// <summary>
    /// 제목 문자열("AD_delivery_save_title")에 광고 보상 값(ad_rwd_value)을 넣어 표시.
    /// </summary>
    private void SetTextTitle()
    {
        if( TxtAdTitle == null ) return;

        TxtAdTitle.text = string.Format(CStringDataManager.Instance.GetStringData("AD_delivery_save_title"), setting.AdvertiseData.ad_rwd_value);
    }
    
    /// <summary>
    /// 설명 문자열("AD_delivery_save_desc")에 광고 보상 값(ad_rwd_value)을 넣어 표시.
    /// </summary>
    private void SetTextDesc()
    {
        if( TxtAdDesc == null) return;

        TxtAdDesc.text = string.Format(CStringDataManager.Instance.GetStringData("AD_delivery_save_desc"), setting.AdvertiseData.ad_rwd_value);
    }

    /// <summary>
    /// 남은 광고 횟수(서버 광고 목록의 remain_cnt) / 전체 횟수(테이블 ad_use_count) 표시.
    /// </summary>
    private void SetAdCountText()
    {
        if( TxtCount == null) return;
        int remainCnt = AdsManager.Instance.GetAdsRemainCnt(setting.AdvertiseData.ad_id);
        int totalCnt = setting.AdvertiseData.ad_use_count;

        TxtCount.text = $"{remainCnt}/{totalCnt}";
    }

    /// <summary>
    /// 남은 대기시간(초)을 HH:MM:SS 형식으로 표시.
    /// </summary>
    private void SetTimeText(float remainTime)
    {
        if( TxtTime != null )
        {
            TxtTime.text = Utility.GetTimeFormatHHMMSS((long)remainTime);
        }
    }

    /// <summary>
    /// 광고 스킵 기간(CPlayer.AdSkipEndTime > 현재 서버 시각)이면 스킵 아이콘, 아니면 광고 보상 아이콘을 로드한다.
    /// </summary>
    private void SetIcon()
    {
        if( ImgIcon == null ) return;

        if (AdsManager.Instance.IsADSkip())
            CResourceManager.Instance.LoadImage("icon_common_item_atlas#icon_specialmovie_skip02", ImgIcon);
        else
            CResourceManager.Instance.LoadImage("icon_common_item_atlas#item_ad_reward_pkg02", ImgIcon);
    }

    /// <summary>
    /// 광고 버튼 클릭. 활성(None) 상태일 때만 보상형 광고를 재생한다. Disable 상태(대기 중) 클릭은 무시.
    /// 광고 시청 성공 시 OnCompleteShowAd가 호출되고(광고 스킵 기간이면 재생 없이 즉시 성공 처리),
    /// 실패 콜백은 넘기지 않으므로 실패 시에는 아무 일도 일어나지 않는다.
    /// </summary>
    private void OnClickButton(Button_State state)
    {
        if( state == Button_State.None)
        {
            AdsManager.Instance.ShowRewardedAd(setting.AdvertiseData, setting.OnCompleteShowAd);
        }
    }

    /// <summary>
    /// 광고 재시청 대기시간 카운트다운 시작.
    /// TimeStreamManager의 "AD_TIMER_{AD_TYPE}" 키 TimeStream을 1초 단위 감소 모드로 설정하고 구독한다.
    /// 끝나면 남은 횟수를 표시하고 버튼을 활성 모양으로 돌린다.
    /// </summary>
    private void SetTimer()
    {
        // 대기시간이 정확히 0이면 시작하지 않음 (호출부에서 이미 > 0 일 때만 부름)
        if( setting.RemainTime == 0) return;

        string RemainTimeKey = setting.STR_TIMER_NAME;
        // 같은 키의 TimeStream이 이미 있으면 그것을, 없으면 새 TimeStream 객체를 받는다 (새 객체는 아직 매니저에 등록되지 않은 상태)
        TimeStreamManager.Instance.GetTimeStream(out TimeStream timeStream, RemainTimeKey);

        // [참고] 기존 TimerDispos를 해제하지 않고 새로 만든다. 스크롤 아이템 재활용 등으로 SetUI가 다시 불리면 이전 구독이 남을 수 있다
        TimerDispos = new SingleAssignmentDisposable();
        var timeStreamData = timeStream.GetTimeStreamData();

        // 시작 시간: 기본은 서버 기준 남은 시간, 이미 돌고 있는 TimeStream이 있으면 그 현재값을 이어서 사용
        float startTime = setting.RemainTime;
        if( timeStreamData.CurrentTime > 0 )
        {
            startTime = timeStreamData.CurrentTime;
        }

        // SetTime: startTime → 0 으로 감소하도록 TimeStream 데이터를 재설정
        // OnTimeStreamObservable: TimeStreamManager에 등록(틱 시작)하고 매 틱 TimeStreamData를 발행하는 스트림 반환
        TimerDispos.Disposable = timeStream
        .SetTime(RemainTimeKey, startTime, 0, TimeStreamType.DECREASE)
        .OnTimeStreamObservable()
        .Subscribe(streamData =>
        {
            // 매 틱 남은 시간 텍스트 갱신
            SetTimeText(streamData.CurrentTime);

            // 0 이하가 되면 IsEnd = true (TimeStream은 반복 모드가 아니면 스스로 매니저에서 제거됨)
            if (streamData.IsEnd)
            {
                // 대기 종료 → 남은 횟수 표시 + 버튼 활성 모양
                SetAdCountText();
                BtnClick.SetState(Button_State.None);

                TimerDispose();
            }
        })
        // GameObject 파괴 시에도 구독 해제
        .AddTo(this);
    }
    /// <summary>
    /// 타이머 구독 해제 (TimeStream 자체는 매니저에 남아 계속 흐른다).
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
    /// 부모가 다른 옵션 타입으로 세팅할 때 호출. 타이머 구독을 해제한다.
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        TimerDispose();
    }

    /// <summary>
    /// 광고 옵션 입력 데이터. ObjLevelLinkReslotoption.SetUIDataAD에서 생성된다.
    /// </summary>
    public class Setting
    {
        // 광고 테이블 데이터 (ad_id, ad_use_count, ad_rwd_value 등)
        public AdvertiseData AdvertiseData {get; private set;}
        // 광고 종류 (여기서는 AD_TYPE_LEVELLINK_SLOT) — 타이머 키 생성에 사용
        public AD_TYPE AD_Type {get; private set;}
        
        // 재시청 대기 타이머 TimeStream 키: "AD_TIMER_" + 광고 타입 이름
        public string STR_TIMER_NAME {get; private set;}

        // 광고 시청 성공 시 호출할 콜백 (ObjLevelLinkReslotoption.OnClickEvent())
        public Action OnCompleteShowAd;
 
        public Setting(AdvertiseData adData,AD_TYPE adType, Action onComplete)
        {
            AdvertiseData = adData;
            AD_Type = adType;
            OnCompleteShowAd = onComplete;

            // 예: "AD_TIMER_AD_TYPE_LEVELLINK_SLOT"
            STR_TIMER_NAME = "AD_TIMER_" + AD_Type.ToString();
        }

        /// <summary>
        /// 광고 재시청까지 남은 시간(초). 읽을 때마다 새로 계산한다.
        /// = 서버 광고 목록의 해당 광고 endTime - ServerTimeMgr 보정 현재 시각.
        /// 0 이하이면 지금 바로 시청 가능.
        /// </summary>
        public float RemainTime
        {
            get
            {
                // DateTime - DateTime = TimeSpan → TotalSeconds(double) → float 변환
                var remainTime = (AdsManager.Instance.GetAdsEndTime(AdvertiseData.ad_id) - ServerTimeMgr.Instance.GetInterpolationTime()).TotalSeconds;
                return (float)remainTime;
            }
        }


    }

}
