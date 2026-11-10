using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using SuperScrollView;
using System.Linq;


/// <summary>
/// LevelLink 페이지의 하단 영역 - 전체 LevelLink 슬롯을 나열하는 가로 스크롤 (SuperScrollView LoopListView2).
/// - 부모: PageLevelLink (BottomUI 필드)
/// - 자식: 스크롤 아이템 ObjLevelLinkSlot (LevelLink 1개 = 아이템 1개, 선택/빈칸/잠금/쿨타임 표시)
/// - Setting 은 PageLevelLink.SetData_BottomUI 에서 테이블의 전체 LevelLink 목록과 클릭 콜백으로 채워진다.
/// - 아이템별 서버 데이터/상태는 아이템을 그릴 때(OnGetItemByIndex) 매니저에서 직접 가져온다.
/// - 생명주기: InitUI(스크롤 초기화) → SetUI(아이템 개수 설정, 최초 1회) → 이후 갱신은 UpdateUI(보이는 아이템 새로고침)
/// </summary>
public class PageLevelLink_Bottom : BaseUILayout<PageLevelLink_Bottom.Setting>
{
    // 재사용 가능한 무한 스크롤 리스트 (보이는 아이템만 생성/재활용)
    [SerializeField] private LoopListView2 Scroll;

    /// <summary>
    /// 스크롤 초기화. 아이템 0개로 시작하고, 아이템이 필요할 때마다 OnGetItemByIndex 가 호출되도록 등록한다.
    /// PageLevelLink.InItUI 에서 1회 호출.
    /// </summary>
    public override void InitUI()
    {
        if( Scroll != null )
        {
            Scroll.InitListView(0, OnGetItemByIndex );
        }
    }

    /// <summary>
    /// 아이템 개수를 LevelLink 개수로 설정 → 화면에 보이는 인덱스마다 OnGetItemByIndex 가 호출되어 슬롯이 그려진다.
    /// PageLevelLink.SetUI (OnStart) 에서 호출.
    /// </summary>
    public override void SetUI()
    {
        if( Scroll != null )
        {
            Scroll.SetListItemCount(setting.SlotCount);
        }
    }

    /// <summary>
    /// 현재 화면에 보이는 아이템들만 다시 그린다 (각 아이템에 대해 OnGetItemByIndex 재호출).
    /// 호출: PageLevelLink 의 LevelLinkSelectGDID / LevelLinkPageUpdate_CoolTime 구독 (선택 표시·상태·쿨타임 갱신)
    /// </summary>
    public override void UpdateUI()
    {
        base.UpdateUI();

        if( Scroll != null )
        {
            Scroll.RefreshAllShownItem();
        }
    }

    /// <summary>
    /// LoopListView2 가 index 번째 아이템이 필요할 때 호출하는 콜백. 아이템을 꺼내(재활용) 데이터를 채워 반환한다.
    /// </summary>
    private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
    {
        // 범위 밖 인덱스는 아이템 없음
        if (setting.SlotCount <= index) return null;
        if (0 > index) return null;

        // 등록된 첫 번째 아이템 프리팹 이름으로 아이템을 생성하거나 풀에서 재활용
        var strPrefabName = listView.ItemPrefabDataList.First().mItemPrefab.name;
        var scrollItem = listView.NewListViewItem(strPrefabName);
        var item = scrollItem.GetComponent<ObjLevelLinkSlot>();

        // 재활용 아이템은 이미 초기화되어 있으므로, 처음 생성된 아이템만 InitUI(버튼 바인딩) 실행
        if (scrollItem.IsInitHandlerCalled == false)
        {
            scrollItem.IsInitHandlerCalled = true;
            item.InitUI();
        }

        // index 번째 LevelLink 테이블 데이터
        var slotData = setting.SlotDataList[index];

        var sData = LevelLinkManager.Instance.GetLevelLinkData(slotData.level_link_id); //서버 데이터

        item.SetData(new ObjLevelLinkSlot.Setting()
        {
            // 슬롯 클릭 → 아래 OnClickSlotEvent → PageLevelLink.OnClickSlot
            OnClickSlotEvent = OnClickSlotEvent,
            TableInfo = slotData,
            // 남은 쿨타임(초) - 쿨타임 상태일 때 슬롯 타이머에 사용
            LevelLink_CoolTime = sData.remain_time, 
            // 서버 데이터 기준 상태 (LOCK / COOLTIME / SETTING / EMPTY)
            CurrentState = LevelLinkManager.Instance.GetStateLevelLink(slotData.level_link_id),
            // 현재 보고 있는 LevelLink이면 선택 표시
            IsSelect = LevelLinkManager.Instance.CurrentPageGDID == slotData.level_link_id,

        });

        item.SetUI();

        return scrollItem;
    }

    /// <summary>
    /// 슬롯 클릭을 받아 상위(PageLevelLink.OnClickSlot)로 level_link_id 만 넘긴다. (state 인자는 사용하지 않음)
    /// </summary>
    private void OnClickSlotEvent(LevelLinkList tData, LevelLink_Slot_State state)
    {
        if( setting.OnClickSlotEvent != null )
        {
            setting.OnClickSlotEvent.Invoke(tData.level_link_id);
        }
    }

    /// <summary>
    /// 하단 영역 데이터. PageLevelLink.SetData_BottomUI 에서 생성.
    /// </summary>
    public class Setting
    {
        // 슬롯 클릭 콜백 (인자: level_link_id) → PageLevelLink.OnClickSlot
        public Action<int> OnClickSlotEvent;
        // 테이블의 전체 LevelLink 목록 (CLevelLinkDataManager.GetLevelLinkLists)
        public List<LevelLinkList> SlotDataList;
        // 스크롤 아이템 개수
        public int SlotCount => SlotDataList.Count;
        
    }

}
