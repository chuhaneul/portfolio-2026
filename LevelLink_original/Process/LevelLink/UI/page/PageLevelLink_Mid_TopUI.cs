using UnityEngine;
using System;
using DG.Tweening;

/// <summary>
/// LevelLink 중단 영역의 상단 부분 - 가운데 큰 "LevelLink 카드" 와 쿨타임 타이머, LevelLink 효과(레벨 상승) 연출.
/// - 부모: PageLevelLink_Mid (TopUI 필드)
/// - 자식: CardUI(ObjLevelLinkCard, LevelLink 카드 1장), CoolTimeUI(ObjLevelLinkSlot_Timer, 쿨타임 슬라이더/텍스트)
/// - Setting 은 PageLevelLink_Mid.SetUI 에서 Mid.Setting 값을 옮겨 담아 채운다.
/// - SetData 를 override 해서, 데이터를 받는 시점에 자식(CardUI / CoolTimeUI)의 Setting 까지 만들어 둔다.
///   SetUI 에서는 자식 SetUI 호출과 표시/연출 on/off 만 한다.
/// </summary>
public class PageLevelLink_Mid_TopUI : BaseUILayout<PageLevelLink_Mid_TopUI.Setting>
{
    // LevelLink 카드 슬롯 (클릭 시 등록/해제/즉시완료 팝업 분기)
    [SerializeField] private ObjLevelLinkCard CardUI;
    // 쿨타임 타이머 (상태가 COOLTIME 일 때만 표시)
    [SerializeField] private ObjLevelLinkSlot_Timer CoolTimeUI;
    
    [Header("LevelLink 효과 연출")]
    // [참고] 선언만 되어 있고 코드에서 사용되지 않음
    [SerializeField] private GameObject ObjEffect;
    // LevelLink 효과가 적용될 때 재생되는 화살표 이동 / 색상 DOTween 연출
    [SerializeField] private DOTweenAnimation EffectArrow_Move;
    [SerializeField] private DOTweenAnimation EffectArrow_Color;

    // 카드 이펙트(Fx_LevelLink_Complete)를 붙일 부모 오브젝트
    [SerializeField] private GameObject ObjFxCardParent;
    
    // 런타임에 한 번만 생성해서 재사용하는 카드 이펙트 인스턴스 (SetFxEffect_Card 에서 지연 생성)
    private GameObject ObjCardEffect;


    /// <summary>
    /// 자식(CardUI, CoolTimeUI) 1회성 초기화. PageLevelLink_Mid.InitUI 에서 호출.
    /// (ObjLevelLinkSlot_Timer 는 InitUI 를 override 하지 않아 실제로는 빈 함수)
    /// </summary>
    public override void InitUI()
    {
        if( CardUI != null )
        {
            CardUI.InitUI();
        }

        if( CoolTimeUI != null )
        {
            CoolTimeUI.InitUI();
        }
    }

    /// <summary>
    /// Setting 저장(base.SetData) 후, 자식 CardUI / CoolTimeUI 의 Setting 을 만든다.
    /// 호출: PageLevelLink_Mid.SetUI
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);
        if( CardUI != null )
        {
            // 등록된 카드 정보 (미등록이면 CardID = 0 → null)
            var cardInfo = CardInventoryManager.Instance.GetCardInfoByGDID(setting.CardID);
            // true: 카드 원래 레벨 표시 / false: LevelLink 레벨 표시 (ObjCardInfo2.SetEditEnableLevel)
            bool isOrigin = false;
            int cardLevelLinkLevel = 0;
            
            if( cardInfo != null )
            {
                // 카드 원래 레벨이 LevelLink 레벨 이상이면 원래 레벨 그대로, 낮으면 LevelLink 레벨(상승된 레벨)로 표시
                isOrigin = cardInfo.lv >= setting.LevelLinkLV;
                // 카드 레벨 텍스트 대신 보여줄 LevelLink 레벨 값 (카드 데이터의 level_link_lv)
                cardLevelLinkLevel = cardInfo.level_link_lv;
            }
        

            CardUI.SetData(new ObjLevelLinkCard.Setting()
            {
                CardID = setting.CardID,
                // 1 이상이면 카드 배경을 활성 배경으로, 0 이면 비활성 배경으로 표시
                LevelLinkLevel = setting.LevelLinkLV,
                CardLevelLinkLevel = cardLevelLinkLevel,
                IsOrigLevel = isOrigin,
                // 카드 클릭 → PageLevelLink.OnClickLevelLinkCard
                OnClickCardEvent = setting.OnClickEvent,
            });
        }

        // 쿨타임 중일 때만 타이머 Setting 을 갱신한다 (아니면 이전 Setting 이 남아있지만 SetUI 에서 타이머를 끄고 Dispose 함)
        if( setting.CurrentState == LevelLink_Slot_State.COOLTIME)
        {
            if( CoolTimeUI != null )
            {
                CoolTimeUI.SetData(new ObjLevelLinkSlot_Timer.Setting()
                {
                    // 기존 타이머 구독 해제 여부 / 기존 TimeStream 제거 여부 (PageLevelLink 에서 결정)
                    IsReset = setting.IsResetTimer,
                    IsDisposeTimer = setting.IsDisposeTimer,
                    RemainTime = setting.LevelLink_CoolTime,
                    TotalTime = setting.LevelLink_CoolTimeMax,
                    // TimeStream 키 "LEVELLINK_SLOT_{gdid}". 하단 슬롯(ObjLevelLinkSlot)의 타이머와 같은 키 → 같은 TimeStream 을 공유
                    STR_TIMER_NAME = string.Format(LevelLinkManager.LEVELLINK_SLOT_TIME_KEY_FORMAT, setting.LevelLinkID),
               });
            }
        }
    }

    /// <summary>
    /// LevelLink 카드 표시, 쿨타임 타이머 on/off, LevelLink 효과 연출 on/off.
    /// 호출: PageLevelLink_Mid.SetUI (SetData 직후)
    /// </summary>
    public override void SetUI()
    {
        if( CardUI != null )
        {
            CardUI.SetUI();
        }

        // 쿨타임 상태: 타이머를 켜고 TimeStream 구독 시작
        // 그 외 상태: 타이머를 끄고 구독 해제 (ObjLevelLinkSlot_Timer.Dispose → TimerDispose, TimeStream 자체는 남겨둠)
        // [참고] SetData 와 달리 여기서는 CoolTimeUI null 체크가 없음
        if( setting.CurrentState == LevelLink_Slot_State.COOLTIME )
        {
            CoolTimeUI.SetEnable(true);
            CoolTimeUI.SetUI();
        }
        else
        {
            CoolTimeUI.SetEnable(false);
            CoolTimeUI.Dispose();
        }



        // LevelLink 효과 연출 여부:
        // 카드가 등록되어 있고, 카드 원래 레벨 < LevelLink 레벨 (= LevelLink 덕분에 카드 레벨이 올라간 상태) 일 때만 켠다
        bool enableCardEffect = false;

        if( setting.CardID > 0 )
        {
            var cardData = CardInventoryManager.Instance.GetCardInfoByGDID(setting.CardID);
            // [참고] SetData 와 달리 cardData null 체크가 없음 (인벤토리에 없는 카드면 예외 가능)
            enableCardEffect = cardData.lv < setting.LevelLinkLV;
        }

        // 화살표 DOTween 연출과 카드 이펙트를 같은 조건으로 on/off
        SetFxEffect_Arrow(enableCardEffect);
        SetFxEffect_Card(enableCardEffect);
    }



    /// <summary>
    /// 화살표 연출(이동/색상 DOTweenAnimation) 제어.
    /// 매번 DORestart 로 처음부터 다시 시작한 뒤, 켜야 하면 DOPlay(재생 유지), 꺼야 하면 DOPause(시작 지점에서 정지)한다.
    /// </summary>
    private void SetFxEffect_Arrow(bool isEnable)
    {
        if( EffectArrow_Move != null )
        {
            EffectArrow_Move.DORestart();

            if( isEnable == true)
            {
                EffectArrow_Move.DOPlay();
            }
            else
            {
                EffectArrow_Move.DOPause();
            }
        }

        if( EffectArrow_Color != null )
        {
            EffectArrow_Color.DORestart();

            if( isEnable == true)
            {
                EffectArrow_Color.DOPlay();
            }
            else
            {
                EffectArrow_Color.DOPause();
            }
        }

    }

    /// <summary>
    /// 카드 이펙트(Fx_LevelLink_Complete) on/off.
    /// 처음 호출될 때 한 번만 에셋번들에서 프리팹을 로드해 ObjFxCardParent 아래에 생성하고, 이후에는 SetActive 로만 제어한다.
    /// </summary>
    private void SetFxEffect_Card(bool isEnable)
    {
        // 지연 생성 (최초 1회)
        if( ObjCardEffect == null )
        {
            var path = CPREFAB_KEY.Fx_LevelLink_Complete;
            // 에셋번들에서 이펙트 프리팹 로드 (세 번째 인자 this: 로드 요청 주체)
            var objFx = CResourceManager.Instance.LoadObject(path, ResourceLoadFrom.ASSETBUNDLE, this) as GameObject;
            // 부모 아래에 인스턴스 생성
            ObjCardEffect = Utility.AddChild( ObjFxCardParent, objFx);

            // 형제 중 맨 앞(첫 번째)으로 보내서 다른 UI 보다 뒤에 그려지게 한다
            var rc = ObjCardEffect.GetComponent<RectTransform>();
            rc.SetAsFirstSibling();
        }


        ObjCardEffect.SetActive(isEnable);
    }

    /// <summary>
    /// 중단-상단 데이터. PageLevelLink_Mid.SetUI 에서 Mid.Setting 값을 옮겨 담아 생성.
    /// </summary>
    public class Setting
    {
        // LevelLink 카드 슬롯에 등록된 카드 GDID (0 이면 빈 슬롯)
        public int CardID;
        // 카드 원래 레벨 (이 클래스에서는 사용하지 않음, 레벨은 인벤토리에서 직접 조회)
        public int CardLV = 1;
        public int LevelLinkLV = 0 ; //LevelLink 레벨이 1레벨 이상일때 투명화가 되지 않는다.

        // 현재 LevelLink GDID (타이머 키 생성용)
        public int LevelLinkID = 0;
        // 남은 쿨타임(초) / 전체 쿨타임(초)
        public int LevelLink_CoolTime = 0;
        public int LevelLink_CoolTimeMax = 0;

        // ObjLevelLinkSlot_Timer.Setting 의 IsReset / IsDisposeTimer 로 전달
        public bool IsResetTimer = false;
        public bool IsDisposeTimer = false;

        // COOLTIME 일 때만 타이머 표시
        public LevelLink_Slot_State CurrentState = LevelLink_Slot_State.EMPTY;

        // 테이블 정보 (PageLevelLink_Mid 에서 대입 코드가 주석 처리되어 있어 현재 항상 null, 사용처 없음)
        public LevelLinkList TableInfo;
        // LevelLink 카드 클릭 콜백 (PageLevelLink.OnClickLevelLinkCard)
        public Action<int> OnClickEvent;
        
    }
}
