using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using SuperScrollView;
using System.Linq;
using System;

/// <summary>
/// 쿨타임 단축 팝업(PopupLevelLinkInstantcomplete)의 하단 "단축 옵션 목록" 스크롤 영역.
/// - 부모: PopupLevelLinkInstantcomplete (InitUI는 부모 OnAwake에서, SetData/SetUI는 부모 SetData에서 호출)
/// - 자식: LoopListView2(SuperScrollView)가 만드는 ObjLevelLinkReslotoption 아이템. 아이템 1개 = 단축 옵션 1개
///   (옵션 목록은 LevelLinkManager.ShortTimeList: 광고 → 보석 즉시 완료 → 단축 티켓 순, 조건 안 맞는 옵션은 목록에서 빠짐)
/// - Setting: 옵션이 실행되었을 때 부모에게 알려줄 콜백만 가진다.
/// </summary>
public class ObjLevelLinkInstantcomplete_Bottom : BaseUILayout<ObjLevelLinkInstantcomplete_Bottom.Setting>
{
    // 재사용(가상화) 스크롤 리스트. 화면에 보이는 아이템만 생성/재활용한다
    [SerializeField] private LoopListView2 ScrollUI;

    // InitUI 중복 실행 방지
    private bool IsInit = false;

    // 옵션 개수 = LevelLinkManager가 만들어 둔 단축 옵션 리스트 개수
    private int ScrollCount => LevelLinkManager.Instance.GetShortTimeListCount;

    /// <summary>
    /// 부모 팝업 OnAwake 시점에 1회 호출. 옵션 개수로 스크롤 리스트를 초기화한다.
    /// InitListView 호출 시 화면에 보이는 아이템에 대해 OnGetItemByIndex가 바로 불린다.
    /// (리스트 개수는 이때 한 번만 정해지며, 이후 SetUI에서 갱신하지 않는다)
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
    /// base 호출만 있다 (리스트는 InitUI에서 이미 그려짐).
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();
    }

    /// <summary>
    /// LoopListView2가 index 위치의 아이템이 필요할 때 호출하는 콜백.
    /// 아이템을 생성(또는 재활용)하고, 해당 인덱스의 단축 옵션 데이터로 SetData → SetUI 한다.
    /// </summary>
    private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
    {
        // 범위 밖 인덱스면 null 반환 → 리스트 끝으로 처리됨
        if (index < 0) return null;
        if( ScrollCount <= index ) return null;

        // 프리팹이 하나뿐이므로 첫 번째 프리팹 이름으로 아이템을 꺼낸다 (LINQ First())
        var strPrefabName = listView.ItemPrefabDataList.First().mItemPrefab.name;
        var scrollItem = listView.NewListViewItem(strPrefabName);

        var item = scrollItem.GetComponent<ObjLevelLinkReslotoption>();

        // 새로 만들어진 아이템이면 한 번만 InitUI (재활용된 아이템은 건너뜀)
        if (scrollItem.IsInitHandlerCalled == false)
        {
            scrollItem.IsInitHandlerCalled = true;
            item.InitUI();
        }

        // index번째 단축 옵션 데이터(타입, 광고/설정/티켓 데이터, 타이머 키)
        var data = LevelLinkManager.Instance.GetShortTimeData(index);
        ObjLevelLinkReslotoption.Setting itemSetting = new ObjLevelLinkReslotoption.Setting();
        itemSetting.Data = data;
        // 아이템에서 옵션이 실행되면 이 클래스의 OnClickEventButton으로 전달된다
        itemSetting.OnClickEventButton = OnClickEventButton;

        item.SetData(itemSetting);
        item.SetUI();

        return scrollItem;
    }

    /// <summary>
    /// 옵션 아이템에서 올라온 실행 이벤트를 부모 팝업 콜백(OnClickEventScroll)으로 그대로 전달한다.
    /// </summary>
    private void OnClickEventButton(LevelLinkSlot_ShortTime data)
    {
        if( setting.OnClickScrollItem != null )
        {
            setting.OnClickScrollItem(data);

        }

    }

    /// <summary>
    /// 하단 옵션 영역 입력 데이터.
    /// </summary>
    public class Setting
    {
        // 옵션 실행 시 호출할 부모 콜백 (PopupLevelLinkInstantcomplete.OnClickEventScroll)
        public Action<LevelLinkSlot_ShortTime> OnClickScrollItem;
        
    }
}