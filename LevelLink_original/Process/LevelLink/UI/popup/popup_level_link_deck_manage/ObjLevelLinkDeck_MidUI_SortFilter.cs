using UnityEngine;
using UnityEngine.UI;
using System;
using UniRx;
using SuperScrollView;
using Cysharp.Threading.Tasks;


/// <summary>
/// 덱 편집 팝업 중단의 정렬 필터 패널 (멤버 / 퍼즐 속성 필터)
/// - 부모 : ObjLevelLinkDeck_MidUI. SortUI 의 필터 버튼을 누르면 SetEnable(true) 로 열린다
/// - 자식 : ScrollUI(필터 그룹 2줄 : 멤버, 퍼즐 속성 → Objsortgroup) / ButtonUIs(확인, 필터 없음, 전체 선택) / BtnDim(바깥 영역)
/// - 필터 선택 상태 자체는 LevelLinkManager(SortFilter_Member / SortFilter_PZTYPE)가 들고 있고, 이 패널은 그 값을 바꾸고 보여주기만 한다
/// - 닫힐 때 Setting.OnClickEventOK(bool) 로 확정(true) / 취소(false)를 부모에 알린다
/// </summary>
public class ObjLevelLinkDeck_MidUI_SortFilter : BaseUILayout<ObjLevelLinkDeck_MidUI_SortFilter.Setting>
{
    //필터 그룹 목록 스크롤 (SortType.CNT = 2 줄)
    [SerializeField] private LoopListView2 ScrollUI;
    //하단 버튼 묶음 (확인 / 필터 없음 / 전체 선택)
    [SerializeField] private ObjLevelLinkDeck_MidUI_SortFilterButton ButtonUIs;
    //패널 바깥 딤 영역 버튼 → 변경 취소 후 닫기
    [SerializeField] private Button BtnDim;

    /// <summary>
    /// 필터 스크롤의 줄(아이템) 종류. 값 = 스크롤 인덱스, CNT = 줄 수
    /// </summary>
    public enum SortType
    {
        Member = 0,
        PZType, //속성임
        CNT,
    }    

    /// <summary>
    /// 버튼 바인딩 (부모 ObjLevelLinkDeck_MidUI.InitUI 에서 1회)
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( ButtonUIs != null )
        {
            ButtonUIs.InitUI();
        }

        if( BtnDim != null )
        {
            //딤(바깥) 터치 : 부모에 false(취소) 전달 → 패널에서 바꾼 필터 선택을 되돌림, 그리고 패널 닫기
            //BindToOnClick 이 반환한 Observable(BUTTONDELAYTIME 타이머)이 끝날 때까지 버튼 연타가 막힌다
            BtnDim.BindToOnClick(_=>
            {
                if( setting.OnClickEventOK != null )
                {
                    setting.OnClickEventOK.Invoke(false);
                }

                SetEnable(false);
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }
    }

    /// <summary>
    /// 필터 스크롤 초기화. 패널이 처음 열릴 때 한 번만 InitListView 한다 (IsListViewInited 로 중복 방지)
    /// </summary>
    private void InitScroll()
    {
        if( ScrollUI != null )
        {
            if( ScrollUI.IsListViewInited == false )
            {
                ScrollUI.InitListView((int)SortType.CNT, OnGetItemByIndex);
            }
        }
    }

    /// <summary>
    /// 부모 SetData 때마다 호출. 확인 버튼을 일단 활성 상태로 다시 세팅한다
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);

        SetButtonUIData(Button_State.None);
    }

    /// <summary>
    /// 하단 버튼 묶음에 Setting 을 다시 만들어 넘기고 바로 화면 반영 (확인 버튼 상태 변경용)
    /// </summary>
    private void SetButtonUIData(Button_State state)
    {
        if( ButtonUIs != null )
        {
            ButtonUIs.SetData(new ObjLevelLinkDeck_MidUI_SortFilterButton.Setting()
            {
                OKState = state,
                OnClickEventOK = OnClickEventOK,
                OnClickEventNofilter = OnClickEventNoFilter,
                OnClickEventAll = OnClickEventAll,
            });

            ButtonUIs.SetUI();
        }
    }

    public override void SetUI()
    {
        base.SetUI();
    }

    /// <summary>
    /// 패널 열기 / 닫기
    /// - 열기 : ObjLevelLinkDeck_MidUI.OnClickEventSort (필터 버튼)
    /// - 닫기 : 딤 터치(BtnDim), 확인 버튼(OnClickEventOK)
    /// - 열 때만 스크롤 초기화, 열고 닫을 때 모두 0.1초 뒤 UpdateUIState 실행
    /// </summary>
    // [참고] DelayUpdate 는 닫을 때(false)도 실행되고 취소 수단이 없어, 0.1초 안에 팝업이 파괴되면 파괴된 오브젝트에 접근할 수 있다
    public override void SetEnable(bool isEnable)
    {
        //IsEnable 갱신 + gameObject.SetActive
        base.SetEnable(isEnable);

        if( ButtonUIs != null )
        {
            ButtonUIs.SetEnable(isEnable);
        }

        if( IsEnable )
        {
            InitScroll();
        }

        //결과를 기다리지 않고 실행 (Forget)
        DelayUpdate().Forget();

    }

    /// <summary>
    /// 0.1초 대기 후 필터 목록 / 확인 버튼 상태 갱신 (SetEnable 에서 호출)
    /// </summary>
    private async UniTaskVoid DelayUpdate()
    {
        //Observable.Timer 는 0.1초 후 값 하나를 내보내고 완료된다 → await 로 그 시점까지 대기
        await Observable.Timer(TimeSpan.FromSeconds(0.1f));
        UpdateUIState();


    }


    /// <summary>
    /// 필터 스크롤 아이템 생성 / 재사용 콜백
    /// - index 0 : 멤버 필터 그룹, index 1 : 퍼즐 속성 필터 그룹 (Objsortgroup 한 개가 한 줄)
    /// - 그룹 안의 항목 데이터(선택 여부 포함)는 LevelLinkManager 의 SortFilter 에서 가져온다
    /// </summary>
    private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
    {
        // if (setting.DataList.Count <= index) return null;
        //범위 밖 인덱스는 아이템 없음
        if (0 > index) return null;
        if( (int)SortType.CNT <= index) return null;

        var strPrefabName = listView.ItemPrefabDataList[0].mItemPrefab.name;
        var scrollItem = listView.NewListViewItem(strPrefabName);
        var item = scrollItem.GetComponent<Objsortgroup>();

        //처음 생성된 아이템만 필터 목록 초기화 1회
        if(scrollItem.IsInitHandlerCalled == false )
        {
            item.InitFilterList();
            scrollItem.IsInitHandlerCalled = true;
        }

        //스크롤 인덱스 → SortType (0 = Member, 1 = PZType)
        SortType sortType = index.ToEnum<SortType>();

        switch(sortType)
        {
            case SortType.Member:
            {
                //멤버 필터 항목 목록 (0번 = '전체', 이후 멤버별). 그룹 제목 문자열 키 지정 후 데이터 세팅
                var list = LevelLinkManager.Instance.GetSortMemberList;
                item.InitCitizenFilter("UI_common_filter_category_member");
                item.SetFilterData(list);
                //항목 클릭 시 해당 항목 인덱스를 받는다
                item.OnSelectEvent = OnClickSelectMember;
            }
            break;
            case SortType.PZType:
            {
                //퍼즐 속성 필터 항목 목록 (PZTYPE_NONE = '전체')
                var list = LevelLinkManager.Instance.GetSortPzTypeList;
                item.InitCitizenFilter("UI_common_filter_category_puzzletype");
                item.SetFilterData(list);
                item.OnSelectEvent = OnClickSelectPZType;
            }
            break;
        }

        return scrollItem;
    }        

    /// <summary>
    /// 멤버 필터 항목 클릭
    /// - index 0('전체') : 전부 선택되어 있으면 전부 해제, 아니면 전부 선택
    /// - 그 외 : 해당 멤버 선택 토글 후 '전체' 항목 표시 갱신
    /// - 아직 확정 전(임시 선택)이며, 확인 버튼을 눌러야 SaveSortInfo(true) 로 확정된다
    /// </summary>
    private void OnClickSelectMember(int index)
    {
        CDebug.Log($"OnClickSelectMember - {index}");
        LevelLinkManager.Instance.UpdateMemberSortUIData(index);
        UpdateUIState();
    }

    /// <summary>
    /// 퍼즐 속성 필터 항목 클릭
    /// - index == PZTYPE_NONE('전체') : 전체 선택 / 전체 해제 토글
    /// - 그 외 : 해당 속성 선택 토글 후 '전체' 항목 표시 갱신
    /// </summary>
    private void OnClickSelectPZType(int index)
    {
        CDebug.Log($"OnClickSelectPZType - {index}");

        LevelLinkManager.Instance.UpdatePZTypeSortUIData(index);
        UpdateUIState();

    }

    /// <summary>
    /// 필터 목록 다시 그리기 + 확인 버튼 활성 / 비활성 갱신
    /// - 호출 : 필터 항목 클릭, 필터 없음 / 전체 선택 버튼, 패널 열기 / 닫기(DelayUpdate)
    /// </summary>
    public void UpdateUIState()
    {
        //선택 상태가 바뀐 항목 표시를 다시 그린다
        ScrollUI.RefreshAllShownItem();

        // 정렬 가능한 상태인지 체크해서 버튼 활성/비활성화 해준다
        //CheckSortFilter : 인벤토리 전체 카드에 현재 (임시) 필터를 적용했을 때 한 장이라도 남으면 true
        //결과가 0장이 되는 필터 조합이면 확인 버튼을 비활성화한다
        bool checkFilter = LevelLinkManager.Instance.CheckSortFilter();

        if( checkFilter )
        {
            SetButtonUIData(Button_State.None);
        }
        else
        {
            SetButtonUIData(Button_State.Disable);
        }

    }    

    #region OnClickEvent
    /// <summary>
    /// '필터 없음' 버튼 → 멤버 / 퍼즐 속성 선택을 모두 해제 (임시 선택)
    /// </summary>
    private void OnClickEventNoFilter()
    {
        LevelLinkManager.Instance.UpdateSortUIData_RemoveAll();
        UpdateUIState();
    }

    /// <summary>
    /// '전체 선택' 버튼 → 멤버 / 퍼즐 속성을 모두 선택 (임시 선택)
    /// </summary>
    private void OnClickEventAll()
    {
        LevelLinkManager.Instance.UpdateSortUIData_SelectAll();
        UpdateUIState();
    }

    /// <summary>
    /// 확인 버튼 → 부모에 true(확정) 전달 후 패널 닫기
    /// 부모(ObjLevelLinkDeck_MidUI.OnClickEventSortFilter)가 선택을 확정하고 카드 스크롤을 다시 구성한다
    /// </summary>
    private void OnClickEventOK()
    {
        if( setting.OnClickEventOK != null )
        {
            setting.OnClickEventOK.Invoke(true);
        }
        
        SetEnable(false);

    }
    #endregion


    /// <summary>
    /// 정렬 필터 패널 입력 데이터 (ObjLevelLinkDeck_MidUI.SetData 에서 생성)
    /// </summary>
    public class Setting
    {
        //패널이 닫힐 때 호출. true = 확인(선택 확정), false = 딤 터치(선택 취소)
        public Action<bool> OnClickEventOK;

    }
}
