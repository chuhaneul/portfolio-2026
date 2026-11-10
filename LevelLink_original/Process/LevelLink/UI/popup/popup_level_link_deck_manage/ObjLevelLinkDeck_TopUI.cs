using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UniRx;
using System.Linq;
using Game.RestAPI;

/// <summary>
/// 덱 편집 팝업(PopupLevelLinkDeckManage)의 상단 영역
/// - 부모 : PopupLevelLinkDeckManage (UpdateUIData 에서 SetData, SetUI / UpdateUI 호출)
/// - 자식 : DeckGroup(LevelLink 페이지 하단과 같은 덱 슬롯 UI 재사용) / InfoUI(LevelLink 레벨 기존 → 현재, 자동 배치 / 카드 관리 버튼)
/// - Setting : 편집 중 슬롯 배치(SlotList), 슬롯 구성(GroupList), 기존 LevelLink 레벨, 버튼 콜백
/// </summary>
public class ObjLevelLinkDeck_TopUI : BaseUILayout<ObjLevelLinkDeck_TopUI.Setting>
{
    //덱 슬롯 카드 목록 UI (PageLevelLink_Mid_BottomUI 재사용). 카드 클릭 → 슬롯에서 해제
    [SerializeField] private PageLevelLink_Mid_BottomUI DeckGroup;
    //레벨 정보 + 자동 배치 / 카드 관리 버튼
    [SerializeField] private ObjLevelLinkDeck_TopUI_Info InfoUI;
    /// <summary>
    /// 하위 UI 초기화 (부모 InitUI 에서 1회)
    /// </summary>
    public override void InitUI()
    {
        if( DeckGroup != null )
        {
            DeckGroup.InitUI();
        }

        if( InfoUI != null )
        {
            InfoUI.InitUI();
        }
    }

    /// <summary>
    /// 부모(PopupLevelLinkDeckManage.UpdateUIData)에서 슬롯 배치가 바뀔 때마다 호출
    /// - 덱 슬롯 UI 와 레벨 정보 UI 에 새 Setting 전달
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);

        if( DeckGroup != null )
        {
            //MinCardID 는 넘기지 않으므로 기본값 0 → 최저 레벨 카드 이펙트는 표시되지 않는다
            DeckGroup.SetData(new PageLevelLink_Mid_BottomUI.Setting()
            {
                GroupList = setting.GroupList,
                SlotList = setting.SlotList,
                OnClickCardEvent = setting.OnClickCardEvent,
            });
        }

        if( InfoUI != null )
        {
            InfoUI.SetData(new ObjLevelLinkDeck_TopUI_Info.Setting()
            {
                //왼쪽 : 서버에 저장된 기존 LevelLink 레벨
                OriginLevel = setting.OriginLevelLinkLv,
                //오른쪽 : 현재 편집 중 배치 기준 레벨(배치된 카드 중 최저 레벨)
                CurrentLevel = setting.GetCurrentLevelLinkLevel(),
                OnClickAutoEvent = setting.OnClickAutoEvent,
                OnClickCardInvenEvent = setting.OnClickCardInvenEvent,

            });
        }
    }

    /// <summary>
    /// 최초 화면 구성
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();

        if(DeckGroup != null )
        {
            DeckGroup.SetUI();
        }

        if( InfoUI != null )
        {
            InfoUI.SetUI();
        }
    }

    /// <summary>
    /// 슬롯 변경 후 갱신. 하위 UI 는 별도 UpdateUI 없이 SetUI 로 다시 그린다 (SetUI 와 동일 동작)
    /// </summary>
    public override void UpdateUI()
    {
        base.UpdateUI();

        if(DeckGroup != null )
        {
            DeckGroup.SetUI();
        }

        if( InfoUI != null )
        {
            InfoUI.SetUI();
        }


    }

    /// <summary>
    /// 상단 영역 입력 데이터 (PopupLevelLinkDeckManage.UpdateUIData 에서 생성)
    /// </summary>
    public class Setting
    {
        //서버에 저장된 기존 LevelLink 레벨
        public int OriginLevelLinkLv;
        //슬롯 구성 테이블 (슬롯 순서 / 멤버 조건)
        public List<LevelLinkGroupList> GroupList;

        public LevelLinkList SData; //LevelLink 서버 데이터
        //편집 중 슬롯 배치 Dictionary<슬롯 순서, 카드 GDID(0 = 빈 슬롯)>
        public Dictionary<int, int> SlotList = new Dictionary<int, int>();
        
        //덱 슬롯 카드 클릭 → PopupLevelLinkDeckManage.OnClickEventCardRemove
        public Action<int> OnClickCardEvent;
        //자동 배치 → PopupLevelLinkDeckManage.OnClickEventAuto
        public Action OnClickAutoEvent;
        //카드 관리 → PopupLevelLinkDeckManage.OnClickEventCardInven
        public Action OnClickCardInvenEvent;

        /// <summary>
        /// 현재 편집 중 배치의 LevelLink 레벨 = 배치된 카드 중 최저 레벨. 배치된 카드가 없으면 0
        /// (빈 슬롯이 있어도 배치된 카드만으로 계산한다)
        /// </summary>
        public int GetCurrentLevelLinkLevel()
        {
            if (SlotList.Count == 0)
            {
                return 0;
            }

            //빈 슬롯(0) 제외 → 각 카드의 보유 레벨 → 최솟값
            //DefaultIfEmpty(int.MaxValue) : 배치 카드가 하나도 없을 때 Min() 예외를 막기 위한 기본값
            int lv =  SlotList.Values
                .Where(slot=> slot > 0 )
                .Select(slot => CardInventoryManager.Instance.GetCardInfoByGDID(slot).lv)
                .DefaultIfEmpty(int.MaxValue)
                .Min();

            //기본값이 그대로 나왔다면 배치된 카드가 없다는 뜻 → 0
            if( lv == int.MaxValue )
            {
                lv = 0;
            }

            return lv;
        }
    }
}
