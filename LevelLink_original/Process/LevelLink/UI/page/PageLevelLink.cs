using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;
using Game.RestAPI;

/// <summary>
/// LevelLink 메인 페이지 (전체 화면 PagePopup).
/// - 로비의 LevelLink 버튼 → LevelLinkManager.ShowLevelLinkPage() 에서 카드 정보/LevelLink 정보 API 를 받은 뒤
///   CResourceManager.LoadPagePopup (프리팹 키 CPREFAB_KEY.page_level_link) 로 생성된다.
/// - 화면 구성(BaseUILayout 계층의 최상위 부모):
///   TopUI(PageLevelLink_Top: 뒤로가기/도움말) / MidUI(PageLevelLink_Mid: 현재 LevelLink의 카드·덱 영역)
///   / BottomUI(PageLevelLink_Bottom: LevelLink 슬롯 목록 스크롤) / LockUI(PageLevelLink_Lock: 잠긴 LevelLink 덮개)
///   + 좌우 화살표(BtnPrev/BtnNext)로 LevelLink 페이지를 넘긴다.
/// - 이 페이지가 각 자식의 Setting 을 직접 만들어 SetData 로 넘겨주고(SetData_XXX), 자식은 SetUI 에서 그 값을 화면에 그린다.
/// - 데이터 갱신은 MessageBroker 메시지(LevelLinkSelectGDID / LevelLinkPageUpdate_CoolTime / LevelLinkUnLockSlot / RefreshCardData)를
///   받아서 처리하는 구조이다. (구독은 InItUI 에서 등록, AddTo(this) 로 페이지 파괴 시 해제)
/// - 팝업을 여는 함수들은 partial 파일 PageLevelLink.popup.cs 에 있다.
/// </summary>
public partial class PageLevelLink : PagePopup
{
    // 상단 영역 (뒤로가기 / 도움말 버튼)
    [SerializeField] private PageLevelLink_Top TopUI;
    // 중단 영역 (LevelLink 카드, 쿨타임, LevelLink 레벨, 초기화/자동배치 버튼, LevelLink 덱 카드 목록)
    [SerializeField] private PageLevelLink_Mid MidUI;
    // 하단 영역 (전체 LevelLink 슬롯 가로 스크롤)
    [SerializeField] private PageLevelLink_Bottom BottomUI;
    // 현재 LevelLink가 잠겨있을 때 화면을 덮는 잠금 UI (클릭 시 슬롯 추가 팝업)
    [SerializeField] private PageLevelLink_Lock LockUI;

    // 이전 / 다음 LevelLink로 이동하는 화살표 버튼 (목록 끝에서는 반대쪽 끝으로 순환)
    [SerializeField] private Button BtnPrev;
    [SerializeField] private Button BtnNext;

    // InItUI 중복 실행 방지 플래그 (버튼 바인딩·메시지 구독이 두 번 등록되지 않도록)
    private bool IsInit = false;

    /// <summary>
    /// PagePopup.Start() 에서 ShowAsObservable 결과가 나온 뒤 호출되는 페이지 시작 지점.
    /// 순서가 중요하다:
    ///  1) InItUI  : 자식 UI 초기화 + 버튼 바인딩 + MessageBroker 구독 등록
    ///  2) InitData: 매니저의 LevelLink 목록을 만들고 첫 번째 LevelLink를 CurrentPageGDID 로 지정
    ///               → CurrentPageGDID setter 가 LevelLinkSelectGDID 를 Publish 하고,
    ///                 1)에서 등록한 구독이 동기적으로 실행되어 SetData(...) 까지 처리된다.
    ///  3) SetUI   : 모든 자식의 SetUI 를 한 번 더 호출 (특히 BottomUI 는 여기서 스크롤 아이템 개수가 설정된다)
    /// 그래서 SetData() 직접 호출은 주석 처리되어 있다.
    /// </summary>
    protected override void OnStart()
    {
        base.OnStart();
        InItUI();
        InitData();
        // SetData();
        SetUI();

        // 로비 → LevelLink 진입 시 켜둔 메뉴 전환 연출(LoadingLayer)을 끈다
        LoadingLayer.Instance.DisableMenu();
        // FX 레이어의 딤(어둡게 덮기) 처리를 끈다
        Utility.SetFxLayerDimmed(false);
        
    }

    /// <summary>
    /// 자식 UI 의 InitUI 호출, 화살표 버튼 바인딩, MessageBroker 메시지 구독을 등록한다.
    /// OnStart 에서 한 번만 호출된다. (IsInit 으로 재진입 방지)
    /// 모든 구독은 AddTo(this) 로 이 페이지 GameObject 가 파괴될 때 자동 해제된다.
    /// </summary>
    private void InItUI()
    {
        if( IsInit ) return;
        IsInit = true;

        // 각 영역의 1회성 초기화 (버튼 바인딩, 스크롤 초기화 등)
        if( TopUI != null )
        {
            TopUI.InitUI();
        }

        if( MidUI != null )
        {
            MidUI.InitUI();
        }

        if( BottomUI != null )
        {
            BottomUI.InitUI();
        }

        if( LockUI != null )
        {
            LockUI.InitUI();
        }

        // BindToOnClick(UniRx AsyncReactiveCommand):
        //  클릭 시 람다를 실행하고, 람다가 반환한 Observable 이 끝날 때까지(BUTTONDELAYTIME 초 Timer) 버튼 interactable 을 꺼서
        //  연타를 막는다. Timer 가 값을 내고 완료되면 다시 클릭 가능해진다. 바인딩 자체는 AddTo(this) 로 페이지 파괴 시 해제.
        if( BtnPrev != null )
        {
            // 이전 LevelLink로 이동
            BtnPrev.BindToOnClick(_=>
            {
                OnClickPrev();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnNext != null )
        {
            // 다음 LevelLink로 이동
            BtnNext.BindToOnClick(_=>
            {
                OnClickNext();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        // [쿨타임 갱신 메시지]
        // 발행처: ObjLevelLinkSlot_Timer(쿨타임 종료 시), PageLevelLink.popup.cs 의 즉시 완료 팝업 성공 시
        // 처리: 중단(MidUI) 데이터를 타이머 리셋 + 기존 TimeStream 제거 옵션으로 다시 만들고,
        //       중단을 다시 그리고, 하단 슬롯 목록은 보이는 아이템만 새로고침한다. (res.IsEndTime 값은 사용하지 않음)
        MessageBroker.Default.Receive<LevelLinkPageUpdate_CoolTime>().Subscribe(res=>
        {
            // isResetTimer = true, isDisposeTimer = true
            // → 즉시 완료로 남은 시간이 바뀐 경우 이전 TimeStream 을 지우고 서버에서 받은 새 remain_time 으로 타이머를 다시 시작하기 위함
            SetData_MidUI(true, true);

            if( MidUI != null )
            {
                MidUI.SetUI();
            }

            if( BottomUI != null )
            {
                BottomUI.UpdateUI();
            }

        }).AddTo(this);

        // [현재 LevelLink 변경/갱신 메시지] - 이 페이지의 핵심 갱신 경로
        // 발행처:
        //  - LevelLinkManager.CurrentPageGDID setter (최초 진입, 화살표, 하단 슬롯 클릭) → IsChangeGDID = 실제로 값이 바뀌었는지
        //  - 매니저의 서버 요청 성공 콜백 (덱 저장 / 초기화 / 카드 등록 / 카드 해제) → IsChangeGDID = false
        //  - 아래 RefreshCardData 구독, OnCompleteUnlockAni → IsChangeGDID = false
        // 처리: 모든 영역의 Setting 을 다시 만들고, Mid/Lock 은 다시 그리고, Bottom 은 보이는 아이템만 새로고침한다.
        MessageBroker.Default.Receive<LevelLinkSelectGDID>().Subscribe(res=>
        {
            // IsChangeGDID 가 중단 쿨타임 타이머의 IsResetTimer 로 전달된다
            // (단, ObjLevelLinkSlot_Timer.SetTimer 가 시작할 때 항상 TimerDispose 를 하므로 구독은 매번 새로 만들어진다)
            SetData(res.IsChangeGDID);

            if( MidUI != null )
            {
                MidUI.SetUI();
            }

            if( BottomUI != null )
            {
                BottomUI.UpdateUI();
            }

            if( LockUI != null )
            {
                LockUI.SetUI();
            }

        }).AddTo(this);

        // [슬롯 해금 성공 메시지]
        // 발행처: LevelLinkManager.RequestSlotAdd() 의 슬롯 추가 API 성공 콜백
        // 처리: 잠금 UI 를 "해금 연출 재생" 모드로 다시 그린다.
        //       연출이 끝나면 애니메이션 이벤트 → PageLevelLink_Lock.OnCompleteUnclock → OnCompleteUnlockAni 가 호출되어
        //       LevelLinkSelectGDID 를 발행, 전체 화면이 해금된 상태로 갱신된다.
        MessageBroker.Default.Receive<LevelLinkUnLockSlot>().Subscribe(res=>
        {
            // isUnlockPlay = true → 상태를 강제로 LOCK 으로 두어 잠금 UI 가 켜진 채로 해금 애니메이션을 재생
            SetData_LockUI(true);

            if( LockUI != null )
            {
                LockUI.SetUI();
            }

        }).AddTo(this);

        // [카드 데이터 갱신 메시지]
        // 발행처: PageCardinventory.RestartLobbyMovie() — LevelLink > 덱 편집 > 카드 인벤토리로 이동했다가 돌아온 경우 등
        // 처리: HUD 사이드 버튼을 다시 숨기고, 현재 LevelLink로 LevelLinkSelectGDID 를 발행해 전체 화면을 갱신한다.
        MessageBroker.Default.Receive<RefreshCardData>().Subscribe(res=>
        {
            BarManager.Instance.SetHUDSideBtnStatus(false);
            MessageBroker.Default.Publish(new LevelLinkSelectGDID(CurrentGDID));

        }).AddTo(this);

    }

    /// <summary>
    /// 매니저 데이터 초기화.
    /// - InitData(): 테이블의 LevelLink 목록에서 level_link_id 리스트(LevelLinkUIList)를 만든다. (화살표 이동 순서)
    /// - SetLevelLinkCurrentPage(): 리스트의 첫 번째 LevelLink를 CurrentPageGDID 로 지정 → LevelLinkSelectGDID 발행
    /// </summary>
    private void InitData()
    {
        //네비게이션으로 LevelLink의 특정 페이지에 오고자 한다면 여기에서 데이터를 변경해주면 됨
        LevelLinkManager.Instance.InitData();
        LevelLinkManager.Instance.SetLevelLinkCurrentPage();
    }

    /// <summary>
    /// 네 영역(Top/Mid/Bottom/Lock)의 Setting 을 모두 새로 만들어 넘긴다. (그리기는 하지 않음, SetUI/UpdateUI 는 호출부에서)
    /// LevelLinkSelectGDID 구독에서 호출된다.
    /// </summary>
    /// <param name="isChange">LevelLink가 바뀌었는지 여부 → 중단 쿨타임 타이머의 IsResetTimer 로 전달</param>
    private void SetData(bool isChange)
    {
        SetData_TopUI();
        SetData_MidUI(isChange);
        SetData_BottomUI();
        // 일반 갱신이므로 해금 연출은 재생하지 않는다
        SetData_LockUI(false);
    }

    /// <summary>
    /// 상단 Setting: 뒤로가기 버튼 콜백만 전달한다.
    /// </summary>
    private void SetData_TopUI()
    {
        if( TopUI == null) return;
        TopUI.SetData(new PageLevelLink_Top.Setting()
        {
            OnClickBackEvent = OnClickBack,
        });
    }

    /// <summary>
    /// 중단 Setting 을 만든다. 현재 LevelLink(CurrentPageGDID)의 테이블 데이터 + 서버 데이터 + 상태를 조합한다.
    /// 호출: SetData(isChange) / LevelLinkPageUpdate_CoolTime 구독(true, true)
    /// </summary>
    /// <param name="isResetTimer">중단 쿨타임 타이머 구독을 리셋할지 (ObjLevelLinkSlot_Timer.Setting.IsReset)</param>
    /// <param name="isDisposeTimer">기존 TimeStream 자체를 TimeStreamManager 에서 제거할지 (남은 시간이 서버에서 바뀌었을 때)</param>
    private void SetData_MidUI(bool isResetTimer, bool isDisposeTimer = false)
    {
        if( MidUI == null ) return;

        int currentGDID = LevelLinkManager.Instance.CurrentPageGDID;
        LevelLinkList data = CLevelLinkDataManager.Instance.GetLevelLinkData(currentGDID); //테이블 데이터
        LevelLinkList sData = LevelLinkManager.Instance.GetLevelLinkData(currentGDID); //서버 데이터
        // 서버 데이터로 판정한 상태: state==0 → LOCK, remain_time>0 → COOLTIME, slot>0 → SETTING, 그 외 EMPTY
        LevelLink_Slot_State state = LevelLinkManager.Instance.GetStateLevelLink(currentGDID);

        // LevelLink 카드(가운데 큰 카드)의 원래 레벨. 등록된 카드가 없으면 1
        int cardLevel = 1;

        //등록된 카드가 있다면 카드 정보를 가져온다
        if( 0 < sData.slot )
        {
            var cardData = CardInventoryManager.Instance.GetCardInfoByGDID(sData.slot);
            // [참고] 인벤토리에 해당 카드가 없으면 cardData 가 null 이 되어 예외가 날 수 있음 (null 체크 없음)
            cardLevel = cardData.lv;
        }

        // 중단 Setting 구성. PageLevelLink_Mid.SetUI 에서 다시 Mid_TopUI / Mid_MidUI / Mid_BottomUI 용 Setting 으로 나눠진다.
        MidUI.SetData(new PageLevelLink_Mid.Setting()
        {
            // LevelLink 카드 슬롯에 등록된 카드 GDID (0 이면 비어있음)
            CardID = sData.slot,
            CardLV = cardLevel,
            // LevelLink 레벨 (덱 편집을 완료해야 1 이상이 된다. 0 이면 덱 미완성)
            LevelLinkLV = sData.lv,
            LevelLinkID = currentGDID,
            // 남은 쿨타임(초, 서버) / 전체 쿨타임(초, 테이블) → 쿨타임 슬라이더와 텍스트에 사용
            LevelLink_CoolTime = sData.remain_time,
            LevelLink_CoolTimeMax = data.level_link_cooldown,
            IsResetTimer = isResetTimer,
            IsDisposeTimer = isDisposeTimer,
            CurrentState = state,
            // LevelLink 덱 슬롯 정보: Key = 슬롯 순서(1~7, 테이블의 level_link_group_order), Value = 배치된 카드 GDID (0 이면 빈 칸)
            // 실제로 사용하는 슬롯 개수는 GroupList.Count 이며, 7개 키는 항상 채워서 넘긴다.
            SlotList = new Dictionary<int, int>
            {
                { 1, sData.card_1 },
                { 2, sData.card_2 },
                { 3, sData.card_3 },
                { 4, sData.card_4 },
                { 5, sData.card_5 },
                { 6, sData.card_6 },
                { 7, sData.card_7 }
            },

            // 가운데 LevelLink 카드 클릭 → 카드 등록/해제/즉시완료 팝업 분기
            OnClickLevelLinkCardEvent = OnClickLevelLinkCard,
            // 하단 덱 카드 클릭 → 덱 편집 팝업
            OnClickCardEvent = OnClickCard,
            // 자동 배치 버튼 / 초기화 버튼
            OnClickAuto = OnClickAuto,
            OnClickReset = OnClickReset,
            // 이 LevelLink의 덱 슬롯 구성 테이블 (슬롯 순서별로 배치 가능한 멤버 조건 level_link_group_con 을 가진다)
            GroupList = CLevelLinkDataManager.Instance.GetLevelLinkGroupLists(data.level_link_group_index),
        });
    }

    /// <summary>
    /// 하단 Setting: 전체 LevelLink 테이블 목록과 슬롯 클릭 콜백을 넘긴다.
    /// 각 슬롯의 서버 데이터/상태는 PageLevelLink_Bottom.OnGetItemByIndex 에서 매니저로부터 직접 조회한다.
    /// </summary>
    private void SetData_BottomUI()
    {
        if( BottomUI == null) return;

        BottomUI.SetData(new PageLevelLink_Bottom.Setting()
        {
            // 슬롯 클릭 → 해당 LevelLink로 페이지 전환
            OnClickSlotEvent = OnClickSlot,
            // 테이블의 전체 LevelLink 목록 (스크롤 아이템 하나 = LevelLink 하나)
            SlotDataList = CLevelLinkDataManager.Instance.GetLevelLinkLists(),

        });
    }

    /// <summary>
    /// 잠금 UI Setting 을 만든다.
    /// </summary>
    /// <param name="isUnlockPlay">
    /// true : 슬롯 추가(해금) API 성공 직후. 서버 상태는 이미 해금이지만 상태를 LOCK 으로 강제해서
    ///        잠금 UI 를 켜둔 채 해금 애니메이션을 재생한다.
    /// false: 일반 갱신. 서버 상태 그대로 사용 (LOCK 일 때만 잠금 UI 표시)
    /// </param>
    private void SetData_LockUI(bool isUnlockPlay)
    {
        if( LockUI == null) return;

        int currentGDID = LevelLinkManager.Instance.CurrentPageGDID;

        LevelLink_Slot_State curState = LevelLinkManager.Instance.GetStateLevelLink(currentGDID);

        if( isUnlockPlay )
        {
            curState = LevelLink_Slot_State.LOCK;
        }

        LockUI.SetData(new PageLevelLink_Lock.Setting()
        {
            IsUnlockPlay = isUnlockPlay,
            CurrentState = curState,
            // 잠금 화면 클릭 → 이전 LevelLink 해금 여부 확인 후 슬롯 추가 팝업
            OnClickEvent = OnClickLock,
            // 해금 애니메이션 종료(애니메이션 이벤트) → 전체 화면 갱신
            OnCompleteUnlock = OnCompleteUnlockAni,
        });

    }

    /// <summary>
    /// 네 영역의 SetUI 를 모두 호출해서 화면을 그린다. OnStart 에서만 호출된다.
    /// (BottomUI.SetUI 는 스크롤 아이템 개수를 설정하므로 최초 1회 여기서 실행되어야 슬롯 목록이 나타난다.
    ///  이후 갱신은 메시지 구독에서 BottomUI.UpdateUI = 보이는 아이템 새로고침으로 처리)
    /// </summary>
    private void SetUI()
    {
        if( TopUI != null )
        {
            TopUI.SetUI();
        }

        if( MidUI != null )
        {
            MidUI.SetUI();
        }

        if( BottomUI != null )
        {
            BottomUI.SetUI();
        }

        if( LockUI != null )
        {
            LockUI.SetUI();
        }

    }

    #region  OnclickEvent
    /// <summary>
    /// 좌측 화살표(BtnPrev) 클릭. LevelLinkUIList 기준 이전 LevelLink(첫 번째면 마지막으로 순환)을 현재 페이지로 지정한다.
    /// CurrentPageGDID setter 가 LevelLinkSelectGDID 를 발행 → InItUI 의 구독에서 화면 전체 갱신.
    /// </summary>
    private void OnClickPrev()
    {
        CDebug.Log("OnClickPrev");
        int prevGDID = LevelLinkManager.Instance.GetPrevGDID();
        LevelLinkManager.Instance.CurrentPageGDID = prevGDID;
    }

    /// <summary>
    /// 우측 화살표(BtnNext) 클릭. 다음 LevelLink(마지막이면 첫 번째로 순환)을 현재 페이지로 지정한다. (갱신 흐름은 OnClickPrev 와 동일)
    /// </summary>
    private void OnClickNext()
    {
        CDebug.Log("OnClickNext");
        int nextGDID = LevelLinkManager.Instance.GetNextGDID();
        LevelLinkManager.Instance.CurrentPageGDID = nextGDID;
    }

    /// <summary>
    /// 잠금 UI(PageLevelLink_Lock) 클릭.
    /// LevelLink는 순서대로만 해금할 수 있으므로, 목록상 바로 이전 LevelLink가 아직 LOCK 이면 토스트만 띄우고 끝낸다.
    /// 아니면 슬롯 추가(해금) 팝업을 연다.
    /// </summary>
    private void OnClickLock()
    {
        // GetPrevGDID 는 순환 구조라서 첫 번째 LevelLink에서 호출하면 마지막 LevelLink를 돌려준다
        int prevGDID = LevelLinkManager.Instance.GetPrevGDID();
        var state = LevelLinkManager.Instance.GetStateLevelLink(prevGDID);

        if( state == LevelLink_Slot_State.LOCK)
        {
            CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("levellink_toast_05"); //먼저 이전 LevelLink 슬롯을 추가 해주세요.
            return;
        }

        OpenPopupLevelLinkUnlock();
    }

    /// <summary>
    /// 중단 가운데의 LevelLink 카드(ObjLevelLinkCard) 클릭.
    /// 전달 경로: ObjLevelLinkCard.OnClickEvent → Mid_TopUI.Setting.OnClickEvent → Mid.Setting.OnClickLevelLinkCardEvent → 여기
    /// 서버 데이터 기준으로 아래 순서대로 분기한다. (먼저 걸리는 조건에서 return)
    ///  1) LevelLink 레벨 0 (덱 미완성)      → 토스트 "먼저 LevelLink 덱 편집을 완료해주세요"
    ///  2) 카드가 이미 등록됨 (slot > 0) → 카드 등록 해제 팝업
    ///  3) 쿨타임 진행 중 (remain_time>0) → 광고 목록 API 후 즉시 완료 팝업
    ///  4) 그 외 (비어 있음)              → 카드 선택(등록) 팝업
    /// </summary>
    /// <param name="cardID">클릭한 카드 GDID (로그 출력에만 사용)</param>
    private void OnClickLevelLinkCard(int cardID)
    {
        CDebug.Log($"OnClickLevelLinkCard {cardID}");

        int currentGDID = LevelLinkManager.Instance.CurrentPageGDID;
        var sData = LevelLinkManager.Instance.GetLevelLinkData(currentGDID);

        if( sData.lv == 0 )
        {
            // 먼저 LevelLink 덱 편집을 완료해주세요
            CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("levellink_toast_03");
            return;
        }

        if( sData.slot > 0 )
        {
            //카드 등록 해제 팝업
            OpenPopupLevelLinkCardRemove();
            return;
        }

        //타이머가 있을 경우
        if( sData.remain_time > 0 )
        {
            OpenPopupLevelLinkInstantCompleteProcess();
            return;
        }

        //카드 선택 팝업
        OpenPopupLevelLinkSelectcard();
    }


    /// <summary>
    /// 중단 하단의 LevelLink 덱 카드(빈 칸 포함) 클릭 → 어떤 카드를 눌렀든 덱 편집 팝업(PopupLevelLinkDeckManage)을 연다.
    /// 전달 경로: ObjLevelLinkCard → ObjLevelLinkSelectCardList → Mid_BottomUI → Mid.Setting.OnClickCardEvent → 여기
    /// </summary>
    private void OnClickCard(int cardID)
    {
        CDebug.Log("OnClickCard");

        OpenPopupLevelLinkDeckManage();

    }

    /// <summary>
    /// 하단 슬롯 목록(ObjLevelLinkSlot) 클릭 → 해당 LevelLink를 현재 페이지로 지정 (LevelLinkSelectGDID 발행 → 화면 갱신)
    /// 잠긴 슬롯도 이동은 되며, 이동 후 잠금 UI 가 표시된다.
    /// </summary>
    /// <param name="slotID">클릭한 슬롯의 level_link_id</param>
    private void OnClickSlot(int slotID)
    {
        CDebug.Log($"OnClick Slot {slotID}");
        LevelLinkManager.Instance.CurrentPageGDID = slotID;
    }

    /// <summary>
    /// 자동 배치 버튼(Mid_MidUI.BtnAuto) 클릭 → 자동 배치 확인 팝업
    /// </summary>
    private void OnClickAuto()
    {
        CDebug.Log("OnClickAuto");
        OpenPopupLevelLinkDeckAutoSave();
    }

    /// <summary>
    /// 초기화 버튼(Mid_MidUI.BtnReset) 클릭 → 초기화 확인 팝업
    /// (LevelLink 레벨 0 이면 버튼이 Disable 상태라 Mid_MidUI 쪽에서 이 콜백을 호출하지 않는다)
    /// </summary>
    private void OnClickReset()
    {
        CDebug.Log("OnClickReset");

        OpenPopupResetInfo();
    }
 
    /// <summary>
    /// 상단 뒤로가기 버튼 클릭 → 페이지 닫기
    /// </summary>
    private void OnClickBack()
    {
        CDebug.Log("OnClickBack");
        ClosePage();
    }
    #endregion

    /// <summary>
    /// 잠금 UI 의 해금 애니메이션이 끝났을 때(애니메이션 클립 이벤트 → PageLevelLink_Lock.OnCompleteUnclock) 호출된다.
    /// 현재 LevelLink로 LevelLinkSelectGDID 를 발행해서 해금된 상태(서버 데이터 기준)로 전체 화면을 다시 그린다.
    /// </summary>
    private void OnCompleteUnlockAni()
    {
        int currentGDID = LevelLinkManager.Instance.CurrentPageGDID;
        MessageBroker.Default.Publish(new LevelLinkSelectGDID(currentGDID));
    }

    /// <summary>
    /// 안드로이드 백키 처리. PagePopup.OnBackPress() 에서 (웹뷰/튜토리얼/네트워크 로딩 등이 아닐 때) 호출된다.
    /// </summary>
    protected override void OnBackEventProcess()
    {
        ClosePage();
        base.OnBackEventProcess();
    }


    /// <summary>
    /// LevelLink 페이지를 닫는다. (뒤로가기 버튼, 백키)
    /// LoadingLayer.MenuChange: 메뉴 전환 상태로 두고 1프레임 뒤에 콜백을 실행한다.
    /// 세 번째 인자 false(autoDisableMenu 끔) 이므로 콜백 안에서 직접 DisableMenu 를 호출한다.
    /// </summary>
    public void ClosePage()
    {
        CDebug.Log("ClosePage");

        LoadingLayer.Instance.MenuChange(() =>
        {
            // 페이지 GameObject 파괴 (→ AddTo(this) 로 묶인 버튼 바인딩/메시지 구독도 함께 해제됨)
            OnClosePopup();
            // 로비 HUD 사이드 버튼 / BDD 오브젝트를 다시 켠다 (RefreshCardData 구독 등에서 사이드 버튼을 꺼두었음)
            BarManager.Instance.SetHUDSideBtnStatus(true);
            BarManager.Instance.BDDOnOff();

            // 메뉴 전환 연출 종료
            LoadingLayer.Instance.DisableMenu();

        }, null, false);
    }    


}
