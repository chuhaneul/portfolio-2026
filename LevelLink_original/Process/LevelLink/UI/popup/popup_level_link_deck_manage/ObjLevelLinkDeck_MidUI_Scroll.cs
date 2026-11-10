using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SuperScrollView;
using System.Linq;
using System;
using Game.RestAPI;


/// <summary>
/// 덱 편집 팝업 중단의 보유 카드 스크롤 (SuperScrollView LoopListView2 사용)
/// - 부모 : ObjLevelLinkDeck_MidUI (SetData / SetUI / UpdateUI 호출, InitUI 는 팝업 표시 후 DelaySetUI 에서 호출)
/// - 스크롤 아이템 1개 = 카드 5장 묶음(ObjLevelLinkSelectCardListGroup). 카드 GDID 리스트를 5개씩 잘라 한 줄로 보여준다
/// - Setting : 표시할 카드 GDID 리스트(CList), 슬롯 배치 정보(선택 / 선택 불가 표시용), 카드 클릭 콜백
/// </summary>
public class ObjLevelLinkDeck_MidUI_Scroll : BaseUILayout<ObjLevelLinkDeck_MidUI_Scroll.Setting>
{
    [SerializeField] private LoopListView2 ScrollUI;
    //InitListView 는 한 번만 호출해야 하므로 중복 방지 플래그
    private bool IsInit = false;
    //스크롤 한 줄에 들어가는 카드 수
    private readonly int CardGropuCount = 5;
    //스크롤 아이템(줄) 수 = 카드 수 / 5 의 올림. setting 이 없으면 0
    private int ScrollCount
    {
        get
        {
            if( setting != null )
            {
                //q = 꽉 찬 줄 수, r = 마지막 줄에 남는 카드 수
                int q = setting.CList.Count / CardGropuCount;
                int r = setting.CList.Count % CardGropuCount;
                
                //남는 카드가 있으면 한 줄 추가
                if( r > 0 ) q++;
                return q;
            }
            
            return 0;
        }
    }


    /// <summary>
    /// LoopListView2 초기화 (1회). ObjLevelLinkDeck_MidUI.DelaySetUI 에서 팝업이 SHOW 상태가 된 뒤 호출된다
    /// - 아이템이 화면에 보여야 할 때마다 OnGetItemByIndex 가 호출된다
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();
        if( IsInit ) return;
        IsInit = true;


        if( ScrollUI != null )
        {
            ScrollUI.InitListView(ScrollCount, OnGetItemByIndex);
        }
    }

    /// <summary>
    /// 카드 리스트 자체가 바뀌었을 때 호출 (정렬 필터 확정 시 ObjLevelLinkDeck_MidUI.OnClickEventSortFilter)
    /// - 아이템(줄) 수를 다시 설정하고 보이는 아이템을 다시 그린다
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();

        if( ScrollUI != null )
        {
            ScrollUI.SetListItemCount(ScrollCount);
            ScrollUI.RefreshAllShownItem();
        }
        
    }

    /// <summary>
    /// 보이는 아이템만 다시 그린다 (선택 표시 / 정렬 순서 변경 반영용)
    /// </summary>
    // [참고] SetListItemCount 를 호출하지 않으므로, 카드 인벤토리 복귀(RefreshCardData) 등으로 카드 수가 바뀐 경우 스크롤 줄 수가 갱신되지 않을 수 있다
    public override void UpdateUI()
    {
        base.UpdateUI();

        if( ScrollUI != null )
        {
            ScrollUI.RefreshAllShownItem();
        }
    }

    /// <summary>
    /// 맨 위로 버튼 → 첫 번째 아이템(줄)으로 이동 (ObjLevelLinkDeck_MidUI.OnClickEventGoTop)
    /// </summary>
    public void GoToScrollTop()
    {
        if( ScrollUI != null )
        {
            ScrollUI.MovePanelToItemIndex(0, 0);
        }
    }

    /// <summary>
    /// LoopListView2 아이템 생성 / 재사용 콜백. index 번째 줄에 해당하는 카드 최대 5장을 세팅한다
    /// </summary>
    private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
    {
        //범위 밖 인덱스는 null 반환 (아이템 없음)
        if (index < 0) return null;
        if( ScrollCount <= index ) return null;

        //프리팹 목록의 첫 번째 프리팹으로 아이템 생성(또는 풀에서 재사용)
        var strPrefabName = listView.ItemPrefabDataList.First().mItemPrefab.name;
        var scrollItem = listView.NewListViewItem(strPrefabName);

        var item = scrollItem.GetComponent<ObjLevelLinkSelectCardListGroup>();

        //풀에서 처음 만들어진 아이템만 InitUI 1회 실행
        if (scrollItem.IsInitHandlerCalled == false)
        {
            scrollItem.IsInitHandlerCalled = true;
            item.InitUI();
        }

        //이 줄의 시작 인덱스와 카드 수 (기본 5장)
        int startID = index * CardGropuCount;
        int count = CardGropuCount;

        //마지막 줄은 남는 카드 수만큼만 (나누어 떨어지면 5장 그대로)
        if( index == ScrollCount - 1)
        {
            int r = setting.CList.Count % CardGropuCount;
            if( r != 0 )
            {
                count = r;
            }
        }

        item.SetData(new ObjLevelLinkSelectCardListGroup.Setting()
        {
            //전체 카드 리스트에서 이 줄에 해당하는 구간만 잘라서 전달
            CardIDs = setting.CList.GetRange(startID, count),
            //슬롯에 배치된 카드는 '선택됨' 표시
            SelectCardIDs = setting.SlotCardIDs,
            OnClickCardEvent = OnClickCardEvent,

            //true : 더 이상 배치할 수 없는 카드(맞는 빈 슬롯이 없는 카드)에 딤 표시. 아래 3개 데이터로 판단
            IsCheckNotSelect = true,
            GroupList = setting.GroupList,
            SlotList = setting.SlotList,
            SData = setting.SData,


        });
        
        item.SetUI();

        return scrollItem;
    }

    /// <summary>
    /// 스크롤 카드 클릭 → 부모 팝업의 OnClickEventCardSelect(빈 슬롯에 배치)로 전달
    /// </summary>
    private void OnClickCardEvent(int cardID)
    {
        if( setting.OnClickEventCardSelect != null )
        {
            setting.OnClickEventCardSelect.Invoke(cardID);
        }
    }

    /// <summary>
    /// 카드 스크롤 입력 데이터 (ObjLevelLinkDeck_MidUI.SetData / OnClickEventSortFilter 에서 생성)
    /// </summary>
    public class Setting
    {
        //표시할 카드 GDID 리스트 (필터 + 정렬 적용 완료된 순서)
        public List<int> CList;
        //현재 슬롯에 배치된 카드 GDID 목록 (선택 표시용)
        public List<int> SlotCardIDs;
        //카드 클릭 콜백
        public Action<int> OnClickEventCardSelect;


        //선택 여부 때문에 필요한 데이터들
        public List<LevelLinkGroupList> GroupList;
        public Dictionary<int, int> SlotList;
        public LevelLinkList SData;

   }
   
}
