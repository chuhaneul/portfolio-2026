using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UniRx;
using Game.RestAPI;
using System.Linq;

/// <summary>
/// LevelLink 덱 편집 팝업
/// - 여는 곳 : PageLevelLink.OnClickCard() → PageLevelLink.OpenPopupLevelLinkDeckManage() (LevelLink 페이지 하단 덱 카드 영역 클릭)
/// - Setting : 편집 전 LevelLink 레벨(서버 lv), 슬롯 구성 테이블(GroupList), 서버 데이터(SData), 현재 슬롯 배치(SlotList)
/// - Result  : 저장 확정 시 IsSuccess = true 와 편집된 SlotList 를 반환한다
///             → 호출측은 IsSuccess 일 때 LevelLinkManager.RequestLevelLinkDeckSave(res.SlotList) 로 덱 저장 API 를 호출한다
///             → 닫기/취소 시 IsSuccess = false 그대로 반환되어 아무 것도 저장되지 않는다
/// - 이 팝업 자체는 서버 통신을 하지 않는다. 편집은 setting.SlotList(로컬 사본)에만 반영된다
/// - 화면 구성
///   TitleUI : 닫기 / 튜토리얼 버튼
///   TopUI   : 덱 슬롯 카드 + LevelLink 레벨(기존 → 현재) + 자동 배치 / 카드 관리 버튼
///   MidUI   : 정렬 드롭다운 / 정렬 필터 패널 / 보유 카드 스크롤 / 저장 버튼
/// - CPopupUI 규칙 : Close() 하면 OnClose 에서 result 를 한 번 OnNext 한 뒤 OnCompleted 된다
/// </summary>
public class PopupLevelLinkDeckManage : CPopupUI<PopupLevelLinkDeckManage.Setting, PopupLevelLinkDeckManage.Result>
{
    //팝업 상단 타이틀 영역 (닫기, 튜토리얼 버튼)
    [SerializeField] private ObjLevelLinkDeck_TitleUI TitleUI;
    //팝업 상단 덱 영역 (덱 슬롯 카드, LevelLink 레벨 변화, 자동 배치 / 카드 관리 버튼)
    [SerializeField] private ObjLevelLinkDeck_TopUI TopUI;
    //팝업 중단 영역 (정렬 드롭다운 / 필터, 보유 카드 스크롤, 저장 버튼)
    [SerializeField] private ObjLevelLinkDeck_MidUI MidUI;

    //정렬 타입(LevelLinkManager.CurrentDeckSortType) 변경 구독 핸들
    SingleAssignmentDisposable DisposableDeckSort = new SingleAssignmentDisposable();
    //카드 인벤토리에서 돌아올 때 발행되는 RefreshCardData 메시지 구독 핸들
    SingleAssignmentDisposable DisposableRefresh = new SingleAssignmentDisposable();


    /// <summary>
    /// 팝업 생성 시(Awake) 1회 호출
    /// - 결과 Subject 와 기본 결과(IsSuccess = false) 생성
    /// - 정렬 필터 데이터 / 정렬 타입 초기화 후 하위 UI 초기화 및 구독 등록
    /// </summary>
    protected override void OnAwake()
    {
        base.OnAwake();

        //ShowAsObservable() 구독자(호출측)에게 결과를 전달할 Subject. 기본 결과는 실패(취소)
        subject = new Subject<Result>();
        result = new Result {IsSuccess = false};

        //DISABLE_HIDE : 이 팝업 위에 다른 팝업(저장 확인 등)이 열려도 숨김 연출 없이 그대로 보이게 한다
        Flags = PopupFlags.DISABLE_HIDE;

        //멤버 / 퍼즐 속성 필터 선택 상태를 초기 상태(전체 선택)로 되돌린다
        LevelLinkManager.Instance.InitSortFilterData();
        //정렬 타입을 0(LevelLinkDeckSort.List[0] = 레벨순)으로 초기화
        //CurrentDeckSortType 은 static 이라 이전에 열었던 정렬 값이 남아 있을 수 있어 매번 리셋한다
        LevelLinkManager.Instance.InitCurrentDeckSortType();

        //InitUI 안에서 CurrentDeckSortType 구독을 등록하므로 반드시 위의 정렬 타입 초기화 이후에 호출되어야 한다
        InitUI();
    }

    /// <summary>
    /// 첫 프레임 전 1회 호출. RectTransform 오프셋을 0 으로 맞춰 부모 영역에 꽉 차게 한다
    /// </summary>
    protected override void OnStart()
    {
        base.OnStart();
        RectTransform rc = GetComponent<RectTransform>();
        rc.offsetMin = Vector2.zero;
        rc.offsetMax = Vector2.zero;        

    }

    /// <summary>
    /// 호출측(PageLevelLink)이 팝업을 연 직후 Setting 을 전달할 때 호출
    /// - 하위 UI 에 데이터 전달(SetUIData) 후 최초 화면 구성(SetUI)
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);

        SetUIData();
        SetUI();
    }

    /// <summary>
    /// 하위 UI 초기화(버튼 바인딩 등) 및 메시지 / 정렬 타입 구독 등록. OnAwake 에서 1회 호출
    /// </summary>
    private void InitUI()
    {
        if( TitleUI != null )
        {
            TitleUI.InitUI();
        }

        if( TopUI != null )
        {
            TopUI.InitUI();
        }

        if( MidUI != null )
        {
            MidUI.InitUI();
        }

        //카드 인벤토리에서 돌아오면(PageCardinventory 종료 시) RefreshCardData 메시지가 발행된다
        //카드 레벨 등이 바뀌었을 수 있으므로 하위 UI 데이터를 다시 만들고 화면을 갱신한다
        //AddTo(this) : 팝업 GameObject 가 파괴될 때 구독이 자동 해제된다
        DisposableRefresh.Disposable = MessageBroker.Default.Receive<RefreshCardData>().Subscribe(res=>
        {
            UpdateUIData();
            UpdateUI();

        }).AddTo(this);

        
        //정렬 드롭다운(ObjLevelLinkDeck_MidUI_Sort)에서 CurrentDeckSortType 값이 바뀌면 카드 리스트를 다시 정렬해 갱신한다
        //ReactiveProperty 는 구독하는 즉시 현재값을 한 번 흘려보낸다
        // - DistinctUntilChanged : 직전과 같은 값이 들어오면 무시
        // - Skip(1) : 구독 시점에 들어오는 현재값(OnAwake 에서 초기화한 0)은 건너뛰고, 이후 실제 변경분만 처리
        // - AddTo(this) : 팝업 파괴 시 자동 해제
        DisposableDeckSort.Disposable = LevelLinkManager.CurrentDeckSortType
        .DistinctUntilChanged()
        .Skip(1)
        .Subscribe(res=>
        {
            UpdateUIData();
            UpdateUI();

        }).AddTo(this);

    }

    /// <summary>
    /// 하위 UI 에 Setting 을 전달한다 (SetData 에서 1회)
    /// - TitleUI 에는 닫기 / 튜토리얼 콜백만 전달하고, TopUI / MidUI 는 UpdateUIData 에서 처리
    /// </summary>
    private void SetUIData()
    {
        if( setting == null ) return;

        if( TitleUI != null)
        {
            TitleUI.SetData(new ObjLevelLinkDeck_TitleUI.Setting()
            {
                OnClickClose = OnClickEventClose,
                OnClickTutorial = OnClickEventTutorial,
            });
        }


        UpdateUIData();
    }

    /// <summary>
    /// TopUI / MidUI 에 현재 setting(특히 편집 중인 SlotList)을 기반으로 새 Setting 을 만들어 전달한다
    /// - 호출 시점 : 최초 SetData, 카드 배치 / 해제, 자동 배치, 정렬 타입 변경, 카드 인벤토리 복귀(RefreshCardData)
    /// - 데이터만 갱신하므로 화면 반영은 이어서 SetUI / UpdateUI 를 호출해야 한다
    /// - MidUI.SetData 내부에서 덱 편집용 카드 리스트(필터 + 정렬)를 매번 새로 조회한다
    /// </summary>
    public void UpdateUIData()
    {
        if( setting == null ) return;

        if( TopUI != null )
        {
            TopUI.SetData(new ObjLevelLinkDeck_TopUI.Setting()
            {
                OriginLevelLinkLv = setting.OriginLevelLinkLv,
                GroupList = setting.GroupList,
                SData = setting.SData,
                SlotList = setting.SlotList,
                //덱 슬롯의 카드를 누르면 해당 카드를 슬롯에서 해제
                OnClickCardEvent = OnClickEventCardRemove,
                //자동 배치 버튼
                OnClickAutoEvent = OnClickEventAuto,
                //카드 관리(카드 인벤토리 이동) 버튼
                OnClickCardInvenEvent = OnClickEventCardInven,

            });
        }

        if( MidUI != null )
        {
            MidUI.SetData(new ObjLevelLinkDeck_MidUI.Setting()
            {
                //빈 슬롯이 하나도 없을 때만 true → 저장 버튼 활성(Button_State.None). 이름과 달리 true 가 '버튼 활성' 의미
                IsEnableDim = setting.IsEmptySlot() == false,
                //현재 슬롯에 배치된 카드 GDID 목록(빈 슬롯의 0 포함) → 스크롤에서 '선택됨' 표시용
                SlotCardIDs = setting.GetSlotCardIDs,
                //저장 버튼
                OnClickEventOK = OnClickEventOK,
                //스크롤 카드 클릭 → 빈 슬롯에 배치
                OnCliCkEventCardSelect = OnClickEventCardSelect,

                //아래 3개는 스크롤 카드의 '선택 불가' 딤 표시 판단에 사용 (ObjLevelLinkSelectCardListGroup.Setting.IsNotSelect)
                GroupList = setting.GroupList,
                SData = setting.SData,
                SlotList = setting.SlotList,

            });
        }
    }

    /// <summary>
    /// 최초 화면 구성 (SetData 에서 1회). 카드 스크롤 초기화는 MidUI 내부에서 팝업 연출 대기 후 처리된다
    /// </summary>
    private void SetUI()
    {
        if( TitleUI != null )
        {
            TitleUI.SetUI();
        }

        if(TopUI != null )
        {
            TopUI.SetUI();
        }
        
        if( MidUI != null )
        {
            MidUI.SetUI();
        }
    }

    /// <summary>
    /// 데이터 변경 후 TopUI / MidUI 화면만 다시 그린다 (TitleUI 는 바뀌는 내용이 없어 제외)
    /// </summary>
    private void UpdateUI()
    {
        if( TopUI != null )
        {
            TopUI.UpdateUI();
        }

        if( MidUI != null )
        {
            MidUI.UpdateUI();
        }
    }

    /// <summary>
    /// 카드 인벤토리 페이지로 이동 (OnClickEventCardInven 에서 호출)
    /// - 이 팝업을 닫지 않고 열린 팝업 전체를 비활성화(SetActive(false))만 하므로, 편집 중인 setting.SlotList 는 그대로 유지된다
    /// - 카드 인벤토리를 나가면 PageCardinventory 에서 팝업을 다시 활성화하고 RefreshCardData 를 발행 → InitUI 의 구독에서 화면 갱신
    /// </summary>
    private void GoToCardInventory()
    {
        // 카드인벤토리 이동
        // 카드 인벤토리에서 뒤로가기 버튼 눌렸을떄,
        // 다시 LevelLink 덱 편집으로 와야 해서 네비게이션을 이용하지 않는다
        //로딩 연출(MenuChange) 사이에 아래 작업을 실행한다
        LoadingLayer.Instance.MenuChange(() =>
        {
            //열려 있는 팝업들을 모두 숨긴다 (닫는 것이 아님)
            var popupService = CCoreServices.GetCoreService<CPopupService>();
            popupService.SetEnablePopupAll(false);

            //카드 인벤토리 페이지 로드
            CResourceManager.Instance.LoadPagePopup<PageCardinventory>(CPREFAB_KEY.page_cardinventory);

        }, null, false);
    }

#region  button click event
    /// <summary>
    /// 닫기 버튼(TitleUI) / 백키(OnBackPress) 처리
    /// - 서버 데이터와 다른 슬롯 배치가 있으면 "저장하지 않고 나갈지" 확인 팝업을 띄우고, 확인 시에만 닫는다
    /// - result.IsSuccess 는 false 그대로이므로 호출측은 저장 API 를 호출하지 않는다
    /// </summary>
    private void OnClickEventClose()
    {
        CDebug.Log("OnClickEventClose");

        //변경사항 체크
        //IsChangeSlotInfo : 편집 중인 SlotList 와 서버 데이터(card_1 ~ card_7)를 슬롯별로 비교해 하나라도 다르면 true
        if( setting.IsChangeSlotInfo() )
        {
            //변경 사항을 저장하지 않고
            var popup = CCoreServices.GetCoreService<CPopupService>().NoticeAlertByKey("levellink_popup_desc_08", PopupAlert.BUTTON_TYPE.BTN_TWO);
            //알림 팝업은 결과를 한 번 내보내므로, 결과를 받으면 직접 Dispose 해서 구독을 정리한다
            SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
            disposable.Disposable = popup.ShowAsObservable()
            .Subscribe(res=>
            {
                //확인 버튼 → 변경 사항을 버리고 팝업 닫기 (취소 → 덱 편집 화면 유지)
                if( res.IsSucess)
                {
                    Close();
                }

                disposable.Dispose();
            });
        }
        else
        {
            Close();
        }
    }

    /// <summary>
    /// 튜토리얼(도움말) 버튼(TitleUI). 현재는 로그만 남기고 아무 동작도 하지 않는다 (웹뷰 연결 자리로 남겨진 상태)
    /// </summary>
    private void OnClickEventTutorial()
    {
        CDebug.Log("OnClickEventTutorial");

        
        //웹뷰
    }

    /// <summary>
    /// 자동 배치 버튼(TopUI_Info) 처리
    /// - LevelLinkManager.GetLevelLinkCardDeckAutoList() 로 추천 배치를 받아 setting.SlotList 를 통째로 교체한다
    ///   (멤버 지정 슬롯은 해당 멤버 카드 중, 나머지 슬롯은 남은 카드 중 레벨 > 전투력 > 등급 높은 순, 같으면 id 순으로 채움)
    /// - 서버 저장은 하지 않는다. 저장 버튼을 눌러야 Result 로 반환된다
    /// </summary>
    private void OnClickEventAuto()
    {
        CDebug.Log("OnClickEventAuto");

        //자동배치
        //Item1 : 모든 슬롯을 채울 수 있는지 여부, Item2 : Dictionary<슬롯 순서, 카드 GDID>
        // [참고] 지역 변수 이름 result 가 팝업 결과 필드 result 를 가린다. 이 메서드 안의 result 는 자동 배치 결과(Tuple)이다
        var result = LevelLinkManager.Instance.GetLevelLinkCardDeckAutoList();

        if( result.Item1 == false )
        {
            //배치할 수 있는 카드가 부족합니다.
            CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("levellink_toast_02");
            return;
        }

        //편집 중인 슬롯 배치를 자동 배치 결과로 교체한 뒤 하위 UI 데이터 / 화면 갱신
        setting.SlotList = result.Item2;
        UpdateUIData();
        UpdateUI();

    }

    /// <summary>
    /// 카드 관리 버튼(TopUI_Info) 처리 → 카드 인벤토리로 이동
    /// - 저장하지 않은 배치 변경이 있으면 확인 팝업을 거친 뒤 이동한다
    /// </summary>
    private void OnClickEventCardInven()
    {
        CDebug.Log("OnClickEventCardInven");

        //배치에 변동사항이 있는 경우, 이동 확인 팝업을 출력한다.
        if(setting.IsChangeSlotInfo())
        {
            //덱에 저장하지 않은 변경 사항이 있습니다. 카드 관리 화면으로 이동 할까요?
            var popup = CCoreServices.GetCoreService<CPopupService>().NoticeAlertByKey("levellink_popup_desc_06", PopupAlert.BUTTON_TYPE.BTN_TWO);
            //결과를 한 번 받으면 직접 Dispose
            SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
            disposable.Disposable = popup.ShowAsObservable()
            .Subscribe(res=>
            {
                //확인 → 카드 인벤토리로 이동 (이 팝업은 숨겨질 뿐이라 편집 상태는 유지됨)
                if( res.IsSucess)
                {
                    GoToCardInventory();
                }

                disposable.Dispose();
            });        
        }
        else
        {
            GoToCardInventory();
        }
    }


    /// <summary>
    /// TopUI 덱 슬롯의 카드를 눌렀을 때 호출 → 해당 카드를 슬롯에서 해제(값을 0 으로)
    /// - SlotList : Dictionary<슬롯 순서(1 ~ 7), 카드 GDID(0 = 빈 슬롯)>
    /// - 서버 저장은 하지 않고 로컬 편집 상태만 바꾼다
    /// </summary>
    private void OnClickEventCardRemove(int cardID)
    {
        //값(카드 GDID)이 cardID 인 슬롯을 찾는다. 없으면 FirstOrDefault 가 default(KeyValuePair) → Key = 0
        // [참고] 빈 슬롯 클릭으로 cardID 0 이 들어오면 첫 번째 빈 슬롯이 매칭되어 0 → 0 으로 다시 대입되고 UI 만 갱신된다 (결과상 변화는 없음)
        var data = setting.SlotList.Where(d=>d.Value == cardID).FirstOrDefault();

        // 이미 등록한 카드 해제 한 후 UI 업데이트 한다
        //슬롯 순서는 1 부터 시작하므로 Key 0 은 '찾지 못함'을 의미
        if( data.Key != 0 )
        {
            CDebug.Log($"카드 해제 {data.Key}- {cardID}");

            //해당 슬롯을 빈 슬롯(0)으로 만들고 화면 갱신
            setting.SlotList[data.Key] = 0;
            UpdateUIData();
            UpdateUI();

            return;
        }


    }

    //스크롤에서 카드 선택 또는 해제
    /// <summary>
    /// MidUI 스크롤의 카드를 눌렀을 때 호출 → 빈 슬롯에 카드를 배치한다
    /// (실제로는 해제는 하지 않는다. 이미 배치된 카드를 누르면 안내 토스트만 띄운다)
    /// 배치 우선순위
    ///  1) 카드 멤버가 조건으로 지정된 슬롯(level_link_group_con == 카드 멤버) 중 비어 있는 첫 슬롯
    ///  2) 멤버 제한이 없는 '그룹 전체' 슬롯 중 비어 있는 첫 슬롯
    ///  3) 둘 다 없으면 "더 이상 등록할 수 없음" 토스트
    /// - setting.SlotList 만 변경하고 서버 저장은 하지 않는다
    /// </summary>
    private void OnClickEventCardSelect(int cardID)
    {
        //1 이미 카드가 등록되어 있는지 체크 한다
        //값이 cardID 인 슬롯 검색. 없으면 Key = 0
        var data = setting.SlotList.Where(d=>d.Value == cardID).FirstOrDefault();

        // 이미 등록한 카드 해제 한 후 UI 업데이트 한다
        //(실제 동작은 해제가 아니라 '이미 덱에 배치된 카드' 안내 후 종료)
        if( data.Key != 0 )
        {
            //이미 등록한 카드 입니다 안내
            //이미 덱에 배치된 카드 입니다
            CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("Mirror_deck_toast_noadd");
            return;
        }

        //카드 등록 시작
        //빈 슬롯(값 0)만 추려낸 Dictionary<슬롯 순서, 0>
        Dictionary<int, int> emptyList = setting.SlotList.Where(d=>d.Value == 0 ).ToDictionary(x=>x.Key, x=>x.Value);
        if( emptyList.Count <= 0)
        {
            //더이상 등록 할 슬롯이 없음
            CDebug.Log($"더이성 등록 할 슬롯이 없음");
            CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("levellink_toast_06");

            return;
        }

        //멤버 특정 슬롯이 있는지 확인한다
        //카드 테이블 데이터에서 카드의 멤버 타입을 얻는다
        var cardData = CCardDataManager.Instance.GetCardListData(cardID);
        MEMBER_TYPE cardMemType = cardData.member;

        //슬롯 구성 테이블 중 이 카드의 멤버가 조건으로 지정된 슬롯들
        var memSlotList = setting.GroupList.Where(d=>d.level_link_group_con == cardMemType ).ToList();

        if( 0 < memSlotList.Count() )
        {
            //멤버 슬롯 중 하나에 배치했는지 여부
            bool setSlot = false;
            //멤버 특정 슬롯이 비어 있는지 확인한다
            for(int i = 0 ; i < memSlotList.Count(); i++)
            {
                //level_link_group_order = SlotList 의 키(슬롯 순서)
                int slotID = memSlotList[i].level_link_group_order;
                if( setting.SlotList.ContainsKey(slotID))
                {
                    //비어 있으므로
                    if( setting.SlotList[slotID] == 0)
                    {
                        setting.SlotList[slotID] = cardID;
                        setSlot = true;
                        CDebug.Log($"멤버슬롯 카드 등록 {slotID}- {cardID}");

                        break;
                    }
                }
            }

            //멤버 슬롯에 세팅 하였다
            if( setSlot )
            {
                UpdateUIData();
                UpdateUI();
                CDebug.Log($"멤버슬롯 카드 등록 후 UI 갱신");

                return;
            }
        }
        
        //여기 도달 : 카드 멤버의 지정 슬롯이 없거나, 있어도 모두 차 있는 경우 → '그룹 전체' 슬롯에 배치 시도
        //멤버가 지정되어 있지 않은 슬롯 ID 추출
        //level_link_group_con 이 '그룹 전체' 값(MEMBER_TYPE 299)인 슬롯 = 어떤 멤버 카드든 배치 가능한 슬롯
        var normalSlotIDs = setting.GroupList
            .Where(d=>d.level_link_group_con == MEMBER_TYPE.BTS)
            .Select(d=>d.level_link_group_order);

        if( 0 < normalSlotIDs.Count() )
        {
            //비어 있는 슬롯에 세팅한다
            //빈 슬롯 중 '그룹 전체' 슬롯인 첫 번째. 없으면 Key = 0 → 아래 ContainsKey(0) 가 false 라서 배치하지 않고 넘어간다
            var emptySlot = emptyList.Where(d=>normalSlotIDs.Contains(d.Key)).FirstOrDefault();

            if( setting.SlotList.ContainsKey(emptySlot.Key))
            {
                setting.SlotList[emptySlot.Key] = cardID;

                UpdateUIData();
                UpdateUI();
                CDebug.Log($"빈슬롯 카드 등록 {emptySlot.Key}- {cardID}");
                
                return;
            }
        }

        CDebug.Log("남은 슬롯은 멤버 슬롯 밖에 없음. 해당카드는 등록할 수 없음");
        CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("levellink_toast_06");
    }

    //카드 덱 저장
    /// <summary>
    /// 저장 버튼(MidUI_Button) 처리. 버튼은 모든 슬롯이 찼을 때만 활성 상태이므로 이 시점에 빈 슬롯은 없다
    /// - 새 배치의 LevelLink 레벨(배치 카드 중 최저 레벨)이 서버 레벨보다 낮아지면 → 레벨 하락 확인 팝업(PopupLevelLinkDecksave)
    /// - 그 외 → 바로 Result(IsSuccess, SlotList) 를 채우고 닫는다 → 호출측이 덱 저장 API 호출
    /// - 배치가 바뀌지 않았어도 IsSuccess = true 로 닫히므로 호출측은 저장 API 를 호출한다
    /// </summary>
    private void OnClickEventOK()
    {
        CDebug.Log("OnClickEventOK");
        //IsChangeLevelLinkLowLV : 서버 lv > 새 배치 최저 레벨 일 때 true (레벨이 같거나 오르는 경우는 확인 없이 저장)
        if( setting.IsChangeLevelLinkLowLV() )//|| setting.IsChangeLevelLinkLV() )
        {
            OpenPopupDeckSave();
        }
        else
        {
            //결과를 채우고 닫으면 OnClose 에서 호출측으로 result 가 전달된다
            result.IsSuccess = true;
            result.SlotList = setting.SlotList;
            Close();
        }
    }

    /// <summary>
    /// LevelLink 레벨 하락 확인 팝업을 띄운다 (OnClickEventOK 에서 호출)
    /// - BeforeLV : 서버에 저장된 기존 LevelLink 레벨 / AfterLV : 새 배치 기준 레벨(배치 카드 최저 레벨)
    /// - 확인(IsSuccess) 시 결과를 채우고 이 팝업도 닫는다. 취소 시 덱 편집 화면에 그대로 남는다
    /// </summary>
    private void OpenPopupDeckSave()
    {
        CPopupService popupService = CCoreServices.GetCoreService<CPopupService>();
        var popup = popupService.OpenPopupLevelLinkDecksave();
        popup.SetData(new PopupLevelLinkDecksave.Setting()
        {
            BeforeLV = setting.OriginLevelLinkLv,
            AfterLV = setting.GetSlotLevel(),
        });

        //저장 확인 팝업은 결과를 한 번 내보내고 완료되므로, 결과를 받으면 직접 Dispose 한다
        SingleAssignmentDisposable dispose = new SingleAssignmentDisposable();
        dispose.Disposable = popup.ShowAsObservable()
        .Subscribe(res=>
        {
            //확인 → 덱 편집 결과를 성공으로 채우고 덱 편집 팝업도 닫는다
            if( res.IsSuccess )
            {
                result.IsSuccess = true;
                result.SlotList = setting.SlotList;
                Close();
            }

            dispose.Dispose();

        });
    }

    /// <summary>
    /// 팝업이 닫힐 때(CloseAsObservable 마지막 단계) 호출
    /// - base.OnClose() 에서 result 를 구독자(호출측)에게 OnNext → OnCompleted
    /// - 이후 정렬 필터 선택 데이터를 비운다 (다음에 열 때 OnAwake 에서 다시 초기화)
    /// </summary>
    internal override void OnClose()
    {
        base.OnClose();
        LevelLinkManager.Instance.DisposeSortFilter();
    }

    /// <summary>
    /// 백키 처리. 기본 동작(바로 닫기) 대신 닫기 버튼과 같은 변경 사항 확인 로직을 탄다
    /// - 반환값은 COnBackPressService 에서 따로 사용하지 않는다 (Subscribe 만 함)
    /// </summary>
    public override IObservable<bool> OnBackPress()
    {
        OnClickEventClose(); 
        return Observable.Return(false);
    }

    /// <summary>
    /// 구독 핸들 해제용
    /// </summary>
    // [참고] 메서드 이름이 OnDestroy 가 아닌 Oestroy 라서 Unity 가 자동으로 호출하지 않는다. 두 구독 모두 AddTo(this) 로 파괴 시 해제되므로 실제 누수는 없다
    void Oestroy()
    {
        if( DisposableDeckSort != null )
        {
            DisposableDeckSort.Dispose();
        }

        if( DisposableRefresh != null )
        {
            DisposableRefresh.Dispose();
        }
        
    }

    #endregion

    /// <summary>
    /// 덱 편집 팝업 입력 데이터 (PageLevelLink.OpenPopupLevelLinkDeckManage 에서 생성)
    /// </summary>
    public class Setting
    {
        //편집 전 서버에 저장된 LevelLink 레벨(sData.lv, 서버 데이터가 없으면 0). TopUI 왼쪽 레벨, 저장 확인 팝업 BeforeLV 로 사용
        public int OriginLevelLinkLv;
        //현재 LevelLink의 슬롯 구성 테이블 (level_link_group_order = 슬롯 순서, level_link_group_con = 슬롯 멤버 조건). 순서 오름차순
        public List<LevelLinkGroupList> GroupList;
        public LevelLinkList SData; //LevelLink 서버 데이터
        //편집 중인 슬롯 배치 Dictionary<슬롯 순서(1 ~ 7), 카드 GDID(0 = 빈 슬롯)>
        //LevelLinkManager.GetSlotGroupList 가 새로 만든 Dictionary 라 편집해도 서버 데이터에는 영향 없음
        public Dictionary<int, int> SlotList;
        /// <summary>
        /// 슬롯에 배치된 카드 GDID 목록 (빈 슬롯의 0 도 포함). SlotList 가 없으면 빈 리스트
        /// </summary>
        public List<int> GetSlotCardIDs
        {
            get
            {
                if( SlotList != null )
                {
                    return SlotList.Values.ToList();
                }

                return new List<int>();
            }
        }

        /// <summary>
        /// 서버 데이터(card_1 ~ card_7)와 슬롯 배치가 하나라도 다르면 true
        /// </summary>
        public bool IsChangeSlotInfo()
        {
            return LevelLinkManager.Instance.IsChangeSlotInfo(SlotList);
        }

        /// <summary>
        /// 서버 LevelLink 레벨과 새 배치 레벨이 다르면 true (현재 OnClickEventOK 에서는 주석 처리되어 사용하지 않음)
        /// </summary>
        public bool IsChangeLevelLinkLV()
        {
            return LevelLinkManager.Instance.IsChangeLevelLinkLV(SlotList);
        }
        /// <summary>
        /// 새 배치 레벨이 서버 LevelLink 레벨보다 낮아지면 true → 저장 확인 팝업 표시 조건
        /// </summary>
        public bool IsChangeLevelLinkLowLV()
        {
            return LevelLinkManager.Instance.IsChangeLevelLinkLowLV(SlotList);

        }

        /// <summary>
        /// 빈 슬롯(값 0)이 하나라도 있으면 true
        /// </summary>
        public bool IsEmptySlot()
        {
            int emptyCount = SlotList.Where(slot=>slot.Value == 0 ).Count();
            return emptyCount > 0;
        }

        /// <summary>
        /// 새 배치 기준 LevelLink 레벨 = 배치된 카드 중 최저 레벨 (LevelLinkManager.GetLevelLinkSlotLV)
        /// </summary>
        // [참고] GetLevelLinkSlotLV 는 배치된 카드가 하나도 없으면 빈 시퀀스 Min() 으로 예외가 난다. 저장 버튼이 모든 슬롯이 찼을 때만 동작해서 현재 흐름에서는 발생하지 않는다
        public int GetSlotLevel()
        {
            return LevelLinkManager.Instance.GetLevelLinkSlotLV(SlotList);
        }

    }

    /// <summary>
    /// 덱 편집 결과. IsSuccess = true 일 때만 호출측이 SlotList 로 덱 저장 API(RequestLevelLinkDeckSave)를 호출한다
    /// </summary>
    public class Result
    {
        //저장 확정 여부 (닫기 / 취소 시 false)
        public bool IsSuccess = false;
        //저장할 슬롯 배치 Dictionary<슬롯 순서, 카드 GDID> (setting.SlotList 와 같은 참조)
        public Dictionary<int, int> SlotList = new Dictionary<int, int>();

    }

}
