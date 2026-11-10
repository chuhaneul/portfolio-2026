using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// LevelLink 페이지의 중단 영역 (현재 선택된 LevelLink 하나의 상세 화면).
/// - 부모: PageLevelLink (MidUI 필드)
/// - 자식 3개:
///   TopUI    (PageLevelLink_Mid_TopUI)   : 가운데 LevelLink 카드 + 쿨타임 타이머 + 레벨업 연출
///   MidMidUI (PageLevelLink_Mid_MidUI)   : LevelLink 레벨 텍스트 + 초기화 / 자동 배치 버튼
///   BottomUI (PageLevelLink_Mid_BottomUI): LevelLink 덱 카드 목록 (최대 7칸, 2줄)
/// - Setting 은 PageLevelLink.SetData_MidUI 에서 채워지고, 이 클래스의 SetUI 가 그것을 자식별 Setting 으로 나눠 전달한 뒤
///   각 자식의 SetUI 를 호출한다. (자식에 대한 SetData + SetUI 를 한 번에 하는 중계 역할)
/// </summary>
public class PageLevelLink_Mid : BaseUILayout<PageLevelLink_Mid.Setting>
{
    // 중단-상단: LevelLink 카드 / 쿨타임
    [SerializeField] private PageLevelLink_Mid_TopUI TopUI;
    // 중단-중단: LevelLink 레벨 / 초기화·자동배치 버튼
    [SerializeField] private PageLevelLink_Mid_MidUI MidMidUI;
    // 중단-하단: LevelLink 덱 카드 목록
    [SerializeField] private PageLevelLink_Mid_BottomUI BottomUI;

    /// <summary>
    /// 자식 3개의 InitUI 호출 (버튼 바인딩 등 1회성 초기화). PageLevelLink.InItUI 에서 1회 호출.
    /// </summary>
    public override void InitUI()
    {
        if( TopUI != null )
        {
            TopUI.InitUI();
        }

        if( MidMidUI != null )
        {
            MidMidUI.InitUI();
        }

        if( BottomUI != null )
        {
            BottomUI.InitUI();
        }
    }

    /// <summary>
    /// 받은 Setting 을 자식별 Setting 으로 나눠 SetData 후 SetUI 를 호출한다.
    /// 호출: PageLevelLink.SetUI(최초) / LevelLinkSelectGDID·LevelLinkPageUpdate_CoolTime 구독(갱신 시)
    /// </summary>
    public override void SetUI()
    {

        // 중단-상단: LevelLink 카드 + 쿨타임 정보 전달
        if( TopUI != null )
        {
            TopUI.SetData(new PageLevelLink_Mid_TopUI.Setting()
            {
                CardID = setting.CardID,
                CardLV = setting.CardLV,
                LevelLinkLV = setting.LevelLinkLV,
                LevelLinkID = setting.LevelLinkID,
                LevelLink_CoolTime = setting.LevelLink_CoolTime,
                LevelLink_CoolTimeMax = setting.LevelLink_CoolTimeMax,
                IsResetTimer = setting.IsResetTimer,
                IsDisposeTimer = setting.IsDisposeTimer,
                CurrentState = setting.CurrentState,
                OnClickEvent = setting.OnClickLevelLinkCardEvent,

                // TableInfo = setting.TableInfo,

            });
            TopUI.SetUI();
        }

        // 중단-중단: LevelLink 레벨 + 초기화/자동배치 콜백 전달
        if( MidMidUI != null )
        {
            MidMidUI.SetData(new PageLevelLink_Mid_MidUI.Setting()
            {
                LevelLinkLV = setting.LevelLinkLV,
                OnClickReset = setting.OnClickReset,
                OnClickAuto = setting.OnClickAuto,

            });
            MidMidUI.SetUI();
        }

        // 중단-하단: 덱 카드 목록 정보 전달
        if( BottomUI != null )
        {
            BottomUI.SetData(new PageLevelLink_Mid_BottomUI.Setting()
            {
                // 덱에 배치된 카드 중 레벨이 가장 낮은 카드 GDID (동일 최저 레벨이 여러 장이면 마지막 카드, 없으면 0)
                // → LevelLink 레벨은 덱 카드 중 최저 레벨로 정해지므로(매니저 GetLevelLinkSlotLV) 그 기준 카드에 ObjLevelLinkCard 강조 이펙트(IsMinCard)를 켠다
                MinCardID = LevelLinkManager.Instance.GetSlotMinCard(setting.SlotList),
                GroupList = setting.GroupList,
                SlotList = setting.SlotList,
                OnClickCardEvent = setting.OnClickCardEvent,
            });

            BottomUI.SetUI();
        }
    }

    /// <summary>
    /// 중단 영역 데이터. PageLevelLink.SetData_MidUI 에서 테이블/서버 데이터를 조합해 생성한다.
    /// </summary>
    public class Setting
    {
        // LevelLink 카드 슬롯에 등록된 카드 GDID (서버 slot, 0 이면 미등록)
        public int CardID = 0;
        // 등록된 카드의 원래 레벨 (미등록이면 1). Mid_TopUI 로 전달되지만 Mid_TopUI 는 카드 레벨을 인벤토리에서 직접 조회한다
        public int CardLV = 1;
        // LevelLink 레벨 (서버 lv, 0 이면 덱 미완성)
        public int LevelLinkLV = 0;
        // 현재 LevelLink GDID (타이머 키 생성용)
        public int LevelLinkID = 0;
        // 남은 쿨타임(초, 서버 remain_time) / 전체 쿨타임(초, 테이블 level_link_cooldown)
        public int LevelLink_CoolTime = 0;
        public int LevelLink_CoolTimeMax = 0;

        // 쿨타임 타이머 구독 리셋 여부 / 기존 TimeStream 제거 여부 (ObjLevelLinkSlot_Timer 로 전달)
        public bool IsResetTimer = false;
        public bool IsDisposeTimer = false;

        // 현재 LevelLink 상태 (EMPTY / SETTING / COOLTIME / LOCK)
        public LevelLink_Slot_State CurrentState = LevelLink_Slot_State.EMPTY;
        // 덱 슬롯 구성 테이블 (슬롯 개수 = Count, 슬롯별 멤버 조건 level_link_group_con)
        public List<LevelLinkGroupList> GroupList;
        // { 슬롯 순서(1~7) : 배치된 카드 GDID }
        public Dictionary<int, int> SlotList = new Dictionary<int, int>();

        // LevelLink 카드 클릭 (PageLevelLink.OnClickLevelLinkCard)
        public Action<int> OnClickLevelLinkCardEvent;
        // 덱 카드 클릭 (PageLevelLink.OnClickCard → 덱 편집 팝업)
        public Action<int> OnClickCardEvent;
        // 초기화 / 자동 배치 버튼 클릭
        public Action OnClickReset;
        public Action OnClickAuto;

    }
}
