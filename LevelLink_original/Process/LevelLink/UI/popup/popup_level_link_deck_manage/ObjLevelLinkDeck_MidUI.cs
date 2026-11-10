using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.RestAPI;


/// <summary>
/// 덱 편집 팝업(PopupLevelLinkDeckManage)의 중단 영역
/// - 부모 : PopupLevelLinkDeckManage (UpdateUIData 에서 SetData, SetUI / UpdateUI 호출)
/// - 자식 : SortUI(정렬 드롭다운 + 필터 버튼) / ScrollUI(보유 카드 스크롤) / ButtonUI(저장, 맨 위로) / SortFilterUI(정렬 필터 패널)
/// - Setting : 저장 버튼 활성 여부, 저장 / 카드 선택 콜백, 현재 슬롯 배치 정보(스크롤 선택 표시용)
/// - 카드 리스트(필터 + 정렬 적용)는 SetData 때마다 LevelLinkManager 에서 새로 조회해 ScrollUI 에 넘긴다
/// </summary>
public class ObjLevelLinkDeck_MidUI : BaseUILayout<ObjLevelLinkDeck_MidUI.Setting>
{
    //정렬 드롭다운 + 정렬 필터 열기 버튼
    [SerializeField] private ObjLevelLinkDeck_MidUI_Sort SortUI;
    //덱 편집용 보유 카드 스크롤 (한 줄에 5장)
    [SerializeField] private ObjLevelLinkDeck_MidUI_Scroll ScrollUI;
    //저장 버튼 / 맨 위로 버튼
    [SerializeField] private ObjLevelLinkDeck_MidUI_Button ButtonUI;
    //멤버 / 퍼즐 속성 필터 패널 (SortUI 의 필터 버튼으로 열림)
    [SerializeField] private ObjLevelLinkDeck_MidUI_SortFilter SortFilterUI;

    //DelaySetUI 의 대기(WaitUntil)를 취소하기 위한 토큰 소스 / 토큰
    // [참고] 이 파일 안에서는 disable_cts 를 Cancel / Dispose 하는 곳이 없어, 팝업이 SHOW 상태가 되기 전에 파괴되면 대기가 정리되지 않을 수 있다
    private CancellationTokenSource disable_cts;
    private CancellationToken disable_ctk;

    /// <summary>
    /// 하위 UI 초기화 (PopupLevelLinkDeckManage.InitUI → OnAwake 시점 1회)
    /// - ScrollUI 는 여기서 초기화하지 않고 DelaySetUI 에서 팝업 표시 이후 초기화한다
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( SortUI != null )
        {
            SortUI.InitUI();
        }

        if( ButtonUI != null )
        {
            ButtonUI.InitUI();
        }

        if( SortFilterUI != null )
        {
            SortFilterUI.InitUI();
        }
    }

    /// <summary>
    /// 부모(PopupLevelLinkDeckManage.UpdateUIData)에서 데이터가 바뀔 때마다 호출
    /// - 덱 편집용 카드 리스트를 새로 조회해 하위 UI 에 Setting 을 다시 전달한다 (화면 반영은 SetUI / UpdateUI 에서)
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);


        //덱 편집 카드 리스트(카드 GDID 목록)
        //인벤토리 카드 중 '다른 LevelLink'에 등록된 카드와 LevelLink 레벨이 적용된 카드(level_link_lv != 0)를 빼고,
        //멤버 / 퍼즐 속성 필터를 적용한 뒤 현재 정렬 타입(CurrentDeckSortType)으로 정렬한 결과
        var cList = LevelLinkManager.Instance.GetLevelLinkCardDeckList();

        if( SortUI != null )
        {
            SortUI.SetData(new ObjLevelLinkDeck_MidUI_Sort.Setting()
            {
                //필터 버튼 → 정렬 필터 패널 열기
                OnClickEventSort = OnClickEventSort,

            });
        }

        if( ScrollUI != null )
        {
            ScrollUI.SetData(new ObjLevelLinkDeck_MidUI_Scroll.Setting()
            {
                CList = cList,
                SlotCardIDs = setting.SlotCardIDs,
                //카드 클릭은 부모 팝업의 OnClickEventCardSelect 로 바로 전달
                OnClickEventCardSelect = setting.OnCliCkEventCardSelect,

                GroupList = setting.GroupList,
                SlotList = setting.SlotList,
                SData = setting.SData,

            });
        }

        if( ButtonUI != null )
        {
            ButtonUI.SetData(new ObjLevelLinkDeck_MidUI_Button.Setting()
            {
                //모든 슬롯이 찼으면(IsEnableDim == true) 저장 버튼 활성(None), 아니면 비활성(Disable)
                State = setting.IsEnableDim == true ? Button_State.None : Button_State.Disable,
                OnClickEventOK = setting.OnClickEventOK,
                OnClickEventGoTop = OnClickEventGoTop,
            });
        }

        if( SortFilterUI != null )
        {
            SortFilterUI.SetData(new ObjLevelLinkDeck_MidUI_SortFilter.Setting()
            {
                //필터 패널이 닫힐 때 호출 (true = 확인 버튼, false = 바깥 딤 터치로 취소)
                OnClickEventOK = OnClickEventSortFilter,

            });

        }
    }

    /// <summary>
    /// 최초 화면 구성 (부모 SetUI 에서 1회)
    /// - ScrollUI 는 팝업 연출 때문에 바로 초기화하지 않고 DelaySetUI 로 미룬다
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();

        if( SortUI != null )
        {
            SortUI.SetUI();
        }

        if( ButtonUI != null )
        {
            ButtonUI.SetUI();
        }

        if( SortFilterUI != null )
        {
            SortFilterUI.SetUI();
        }


        //비동기 실행 후 결과를 기다리지 않는다 (Forget)
        DelaySetUI().Forget();
    }

    /// <summary>
    /// 데이터 변경 후 화면 갱신 (부모 UpdateUI 에서 호출: 카드 배치 / 해제, 자동 배치, 정렬 변경, 카드 인벤토리 복귀)
    /// - ScrollUI 는 표시 중인 아이템만 다시 그린다 (아이템 개수 재설정은 하지 않음)
    /// </summary>
    public override void UpdateUI()
    {
        base.UpdateUI();

        if( SortUI != null )
        {
            SortUI.UpdateUI();
        }

        if( ScrollUI != null )
        {
            ScrollUI.UpdateUI();
        }

        if( ButtonUI != null )
        {
            ButtonUI.UpdateUI();
        }

    }

    /// <summary>
    /// 팝업 연출 끝나고 스크롤 초기화 세팅해주도록 한다
    /// </summary>
    /// <returns></returns>
    // SetUI 에서 1회 호출
    private async UniTaskVoid DelaySetUI()
    {
        try
        {
            disable_cts = new CancellationTokenSource();
            disable_ctk = disable_cts.Token;

            if( ScrollUI != null )
            {
                //스크롤 사이즈 잡아야되서 딜레이가 필요하다 팝업에 사이즈 연출이 있기 떄문에 ScrollView Rect를 제대로 잡아주질 못한다
                //열린 덱 편집 팝업을 찾아 Status 가 SHOW 가 될 때까지 매 프레임 확인하며 대기
                // [참고] CPopupUI.ShowAsObservable 기준으로 Status = SHOW 는 transition.In() 직전에 설정되고, 연출이 끝나면 AFTER_SHOW 로 바뀐다
                //        따라서 이 대기는 '연출 시작 시점'에 풀릴 수 있으며, 프레임 사이에 SHOW 를 건너뛰고 AFTER_SHOW 가 되면 계속 대기하게 된다
                var popup = CCoreServices.GetCoreService<CPopupService>().GetPopup<PopupLevelLinkDeckManage>();
                await UniTask.WaitUntil(() => popup.Status == PopupStatus.SHOW, cancellationToken: disable_ctk);

                //이 시점에 LoopListView2 를 초기화 (현재 CList 기준 줄 수로)
                ScrollUI.InitUI();
            }
        }
        catch (Exception e)
        {
            CDebug.LogError($"LevelLink popup DelaySetUI : {e.ToString()}");
            throw e;
        }
    }


    #region  button click event

    /// <summary>
    /// 맨 위로 버튼(ButtonUI) → 카드 스크롤을 첫 줄로 이동
    /// </summary>
    private void OnClickEventGoTop()
    {
        if( ScrollUI != null )
        {
            ScrollUI.GoToScrollTop();
        }

    }

    /// <summary>
    /// 필터 버튼(SortUI) → 정렬 필터 패널을 연다
    /// </summary>
    private void OnClickEventSort()
    {
        CDebug.Log("OnClickEventSort");

        if( SortFilterUI != null )
        {
            SortFilterUI.SetEnable(true);
        }
    }

    /// <summary>
    /// 정렬 필터 패널이 닫힐 때 호출 (SortFilterUI 의 확인 버튼 / 딤 터치)
    /// - isChange == true  : 패널에서 고른 선택 상태를 확정(Original 로 저장)하고, 필터를 적용한 카드 리스트로 스크롤을 다시 구성
    /// - isChange == false : 패널에서 바꾼 선택을 버리고 마지막 확정 상태로 되돌린다 (스크롤은 그대로)
    /// </summary>
    private void OnClickEventSortFilter(bool isChange)
    {
        CDebug.Log($"OnClickEventSortFilter - {isChange}");
        //필터 선택 상태 확정 또는 되돌리기 (LevelLinkManager.SaveSortInfo)
        LevelLinkManager.Instance.SaveSortInfo(isChange);

        if( isChange )
        {
            //확정된 필터 기준으로 카드 리스트를 다시 조회
            var cList = LevelLinkManager.Instance.GetLevelLinkCardDeckList();
            if( ScrollUI != null )
            {
                ScrollUI.SetData(new ObjLevelLinkDeck_MidUI_Scroll.Setting()
                {
                    CList = cList,
                    SlotCardIDs = setting.SlotCardIDs,
                    OnClickEventCardSelect = setting.OnCliCkEventCardSelect,

                    GroupList = setting.GroupList,
                    SlotList = setting.SlotList,
                    SData = setting.SData,

                });

                //카드 수가 바뀌었을 수 있으므로 SetUI 로 아이템 개수(줄 수)부터 다시 설정
                ScrollUI.SetUI();
            }
        
        }
    }

    #endregion

    /// <summary>
    /// 중단 영역 입력 데이터 (PopupLevelLinkDeckManage.UpdateUIData 에서 생성)
    /// </summary>
    public class Setting
    {
        //true = 빈 슬롯이 없음 → 저장 버튼 활성. (이름은 Dim 이지만 true 일 때 버튼이 활성화된다)
        public bool IsEnableDim = false;
        //저장 버튼 클릭 콜백 (PopupLevelLinkDeckManage.OnClickEventOK)
        public Action OnClickEventOK;
        //스크롤 카드 클릭 콜백 (PopupLevelLinkDeckManage.OnClickEventCardSelect)
        public Action<int> OnCliCkEventCardSelect;
        //현재 슬롯에 배치된 카드 GDID 목록 (빈 슬롯 0 포함) → 스크롤에서 '선택됨' 표시
        public List<int> SlotCardIDs;


        //선택 여부 때문에 필요한 데이터들
        public List<LevelLinkGroupList> GroupList;
        public Dictionary<int, int> SlotList;
        public LevelLinkList SData;

    }

}
