using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UniRx;

/// <summary>
/// LevelLink 슬롯 추가(언락) 팝업.
/// - 여는 곳: PageLevelLink.OnClickLock() → PageLevelLink.OpenPopupLevelLinkUnlock()
///   (잠긴 슬롯을 눌렀고, 바로 이전 슬롯은 이미 열려 있을 때만 열린다. 이전 슬롯이 잠겨 있으면 토스트만 띄움)
///   실제 생성은 PopupServiceHelper.OpenPopupLevelLinkUnlock(data)가 프리팹을 로드하고 SetData까지 호출해 준다.
/// - Setting : 현재 페이지 슬롯의 테이블 데이터(LevelLinkList) — 해금 미션 그룹, 필요 재료, 필요 골드가 들어있다.
/// - Result  : IsSuccess. 모든 조건(미션/골드/재료)을 만족한 상태에서 구매 버튼을 눌러 닫힐 때만 true.
///   호출부는 IsSuccess == true 이면 LevelLinkManager.RequestSlotAdd()로 슬롯 추가 API를 호출하고,
///   응답 후 LevelLinkUnLockSlot 메시지가 발행되어 페이지가 갱신된다.
/// - 이 팝업 자체는 슬롯 추가 API를 호출하지 않는다. 단, 재료 부족 시 재료 구매 API(RequestItemBuy)는 이 팝업 안에서 호출한다.
/// - 화면 구성: 상단 미션 목록(MissionListUI) / 필요 재료 목록(ItemListUI) / 골드 가격이 표시된 구매 버튼(BtnBuy) / 닫기·딤 버튼
/// </summary>
public class PopupLevelLinkUnlock : CPopupUI<PopupLevelLinkUnlock.Setting, PopupLevelLinkUnlock.Result>
{
    // 해금 조건 미션 목록 영역 (미션별 진행도 / 달성 여부 표시)
    [SerializeField] private ObjLevelLinkUnlock_MissionListUI MissionListUI;
    // 해금에 필요한 재료 아이템 목록 영역 (보유량 / 필요량 표시)
    [SerializeField] private ObjLevelLinkUnlock_ItemListUI ItemListUI;

    // 구매(해금) 버튼. 필요 골드가 가격으로 표시되고, 골드가 부족하면 Disable 모양이 된다
    [SerializeField] private ButtonSetting BtnBuy;
    // 닫기 버튼 / 팝업 바깥 어두운 영역(딤) 버튼 — 둘 다 결과 IsSuccess=false 그대로 닫는다
    [SerializeField] private Button BtnClose;
    [SerializeField] private Button BtnDim;

    // InitUI 중복 실행 방지 플래그
    private bool Isinit = false;
    // 현재 해금 진행 상태. 우선순위: 미션 미달성(MISSION) → 골드 부족(COST) → 재료 부족(MATERIAL) → 해금 가능(UNLOCK)
    // 값이 바뀌면 InitUI에서 구독한 스트림이 UI 전체를 다시 그린다
    private ReactiveProperty<LevelLink_SLOT_UNLOCK_STATE> CurrentState = new ReactiveProperty<LevelLink_SLOT_UNLOCK_STATE>(LevelLink_SLOT_UNLOCK_STATE.NONE);
    // 구매 버튼 모양 결정용 상태. 골드만 보고 UNLOCK(충분) / COST(부족) 두 값만 사용한다
    private LevelLink_SLOT_UNLOCK_STATE ButtonState = LevelLink_SLOT_UNLOCK_STATE.NONE;

    /// <summary>
    /// 팝업 GameObject 생성 직후(BasePopup.Awake) 호출된다. SetData보다 먼저 실행된다.
    /// 결과 스트림(subject)과 기본 결과(IsSuccess=false)를 준비하고 UI 이벤트를 바인딩한다.
    /// </summary>
    protected override void OnAwake()
    {
        base.OnAwake();

        // ShowAsObservable()을 구독한 호출부에 결과를 전달할 Subject. 팝업이 닫힐 때(OnClose) result를 한 번 보내고 완료된다
        subject = new Subject<Result>();
        // 기본 결과는 실패. 해금 가능 상태에서 구매 버튼을 눌렀을 때만 true로 바뀐다
        result = new Result {IsSuccess = false};

        // 이 팝업 위에 다른 팝업(재료 구매/골드 부족 알림/상점 등)이 열려도 숨김 연출 없이 그대로 둔다
        Flags = PopupFlags.DISABLE_HIDE;

        InitUI();
    }

    /// <summary>
    /// 버튼 클릭 바인딩, 하위 레이아웃 초기화, 상태/골드 변화 구독을 한 번만 설정한다.
    /// </summary>
    private void InitUI()
    {
        if( Isinit ) return;
        Isinit = true;

        if( BtnClose != null )
        {
            // BindToOnClick: 클릭 시 람다를 실행하고, 반환한 Observable이 끝날 때까지 버튼을 비활성화한다(연타 방지)
            BtnClose.BindToOnClick(_=>
            {
                // result.IsSuccess = false 그대로 닫힘 → 호출부는 아무것도 하지 않는다
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnDim != null )
        {
            // 딤(배경) 클릭도 닫기 버튼과 동일하게 동작
            BtnDim.BindToOnClick(_=>
            {
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnBuy != null )
        {
            // ButtonSetting 내부 클릭 바인딩 초기화 후, 클릭 시 현재 버튼 상태(Button_State)를 인자로 OnClickBuyButton 호출
            BtnBuy.Init();
            BtnBuy.SetOnClickButton = OnClickBuyButton;
        }

        if( MissionListUI != null )
        {
            // 미션 슬롯 UI들을 모두 꺼둔 상태로 초기화
            MissionListUI.InitUI();
        }

        if( ItemListUI != null )
        {
            // 재료 아이템 UI들을 초기화 후 모두 꺼둔다
            ItemListUI.InitUI();
        }

        // 해금 상태가 바뀔 때마다 UI 데이터와 화면을 다시 세팅한다
        // - DistinctUntilChanged: 같은 값 연속은 무시 (ReactiveProperty 자체도 같은 값은 발행하지 않음)
        // - Skip(1): 구독 즉시 들어오는 현재값(초기값 NONE)은 건너뛴다
        // - AddTo(this): 팝업 GameObject 파괴 시 구독 해제
        CurrentState.DistinctUntilChanged()
        .Skip(1)
        .Subscribe( state =>
        {
            SetUIData();
            // SetUI 안에서 SetCurrentSatate가 다시 호출되지만 값이 같으면 재발행되지 않으므로 무한 루프는 생기지 않는다
            SetUI();

        }).AddTo(this);

        // 보유 골드가 바뀌면(예: 골드 상점에서 구매 후 돌아옴) 상태와 구매 버튼 모양을 다시 계산한다
        // Skip(1): 구독 시점의 현재 골드 값은 무시하고 이후 변화만 처리
        CPlayer.Instance.Gold.DistinctUntilChanged()
        .Skip(1)        
        .Subscribe(gold=>
        {
            // 상태가 바뀌면 위 CurrentState 구독이 UI 전체를 다시 그린다
            SetCurrentSatate();
            SetButtonState();
            SetBuyButtonUISate();

        }).AddTo(this);

        // MessageBroker.Default.Receive<HudShopRrefresh>()
        // .Subscribe(_=>
        // {
        //     SetButtonState();
        //     SetBuyButtonUISate();

        // }).AddTo(this);
    }

    /// <summary>
    /// 팝업 데이터 세팅. PopupServiceHelper.OpenPopupLevelLinkUnlock()에서 프리팹 로드 직후 호출된다.
    /// 하위 레이아웃 데이터를 만들고(SetUIData) 화면을 그린다(SetUI).
    /// </summary>
    public override void SetData(Setting data)
    {
        // base: setting 필드에 data 저장
        base.SetData(data);

        SetUIData();
        // 첫 SetUI에서 CurrentState가 NONE → 실제 상태로 바뀌므로, CurrentState 구독에 의해 SetUIData/SetUI가 한 번 더 실행된다
        SetUI();

    }

    /// <summary>
    /// 테이블 데이터(setting.TLevelLinkData)를 바탕으로 하위 레이아웃들의 Setting을 만들어 넘기고, 구매 버튼 가격을 표시한다.
    /// SetData 시점과 CurrentState 변경 시점에 호출된다.
    /// </summary>
    private void SetUIData()
    {
        if( setting == null) return;
        if( setting.TLevelLinkData == null) return;

        if( MissionListUI != null )
        {
            // 미션 목록: level_link_condition_index(퀘스트 그룹 인덱스)로 퀘스트 테이블 목록을 가져와 전달
            // 진행도(cur/end)는 MissionListUI가 LevelLinkID + 퀘스트ID로 서버 미션 데이터를 직접 조회한다
            MissionListUI.SetData(new ObjLevelLinkUnlock_MissionListUI.Setting()
            {
                LevelLinkID = setting.TLevelLinkData.level_link_id,
                QuestList = CQuestDataManager.Instance.GetQuestListDatas(setting.TLevelLinkData.level_link_condition_index),

            });
        }

        if( ItemListUI != null )
        {
            // 재료 목록: 테이블의 확장 재료 리스트(타입/ID/필요 수량)를 그대로 전달
            ItemListUI.SetData(new ObjLevelLinkUnlock_ItemListUI.Setting()
            {
                ItemList = setting.TLevelLinkData.level_link_extend_list,                
            });
        }

        if( BtnBuy != null )
        {
            // 구매 버튼에 해금 필요 골드 표시
            BtnBuy.SetPrice(setting.TLevelLinkData.level_link_gold_value);
        }
    }

    /// <summary>
    /// 하위 레이아웃을 그리고, 구매 버튼 모양과 현재 해금 상태(CurrentState)를 다시 계산한다.
    /// SetData, CurrentState 변경, 재료 구매 성공 후(OpenShopItemPopup) 호출된다.
    /// </summary>
    private void SetUI()
    {
        if( MissionListUI != null )
        {
            MissionListUI.SetUI();
        }

        if( ItemListUI != null )
        {

            // 필요 재료가 하나도 없는 슬롯이면 재료 영역 자체를 숨긴다
            if( setting.TLevelLinkData.level_link_extend_list.Count <= 0 )
            {
                ItemListUI.SetEnable(false);
            }
            else
            {
                ItemListUI.SetEnable(true);
                ItemListUI.SetUI();
            }
        }

        // 골드 기준 버튼 상태 계산 → 버튼 모양 반영
        SetButtonState();
        SetBuyButtonUISate();

        // 미션/골드/재료를 순서대로 검사해 CurrentState 갱신 (값이 바뀌면 구독에 의해 UI 재갱신)
        SetCurrentSatate();

    }

    /// <summary>
    /// ButtonState에 따라 구매 버튼 모양을 바꾼다. 골드 부족(COST)이면 Disable 모양, 그 외에는 기본 모양.
    /// ButtonSetting.SetState는 ObjDisable 오브젝트 표시만 바꾸고 버튼 interactable은 건드리지 않는다.
    /// 그래서 골드 부족 상태로 클릭되면 OnClickBuyButton의 COST 분기(골드 부족 알림)가 실행되는 구조다.
    /// </summary>
    private void SetBuyButtonUISate()
    {
        if( BtnBuy == null) return;
        if( setting == null) return;
        if( setting.TLevelLinkData == null) return;

        if( ButtonState == LevelLink_SLOT_UNLOCK_STATE.COST)
        {
            BtnBuy.SetState(Button_State.Disable);
            return;
        }

        BtnBuy.SetState(Button_State.None);
    }



    /// <summary>
    /// 구매 버튼 클릭 처리. 인자로 오는 버튼 상태(state)는 사용하지 않고 CurrentState로 분기한다.
    /// </summary>
    private void OnClickBuyButton(Button_State state)
    {
        switch( CurrentState.Value)
        {
            // 미션 미달성: 토스트 메시지만 표시하고 팝업은 유지
            case LevelLink_SLOT_UNLOCK_STATE.MISSION:
                CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("levellink_toast_04"); //달성하지 못한 조건이 있습니다.
            break;
            // 재료 부족: 부족한 재료를 젬으로 구매하는 팝업 흐름 시작
            case LevelLink_SLOT_UNLOCK_STATE.MATERIAL:
                OpenShopItemPopup();
            break;
            // 골드 부족: "재화가 부족합니다" 알림 → 확인 시 골드 상점으로 이동
            case LevelLink_SLOT_UNLOCK_STATE.COST:
                OpenPopupGoldLack();
            break;
            // UNLOCK(또는 NONE): 결과를 성공으로 바꾸고 닫는다 → 호출부가 슬롯 추가 API(RequestSlotAdd) 호출
            default:
                result.IsSuccess = true;
                Close();
            break;

        }
    }

    /// <summary>
    /// 해금 조건을 우선순위대로 검사해 CurrentState를 정한다.
    /// 미션 → 골드 → 재료 순서로 처음 실패한 조건이 상태가 되고, 모두 통과하면 UNLOCK.
    /// (골드가 재료보다 먼저 검사되므로 골드와 재료가 모두 부족하면 COST 상태가 된다)
    /// </summary>
    private void SetCurrentSatate()
    {
        // 1) 해금 미션을 모두 달성했는지
        if( CheckMissionClear() == false)
        {
            CurrentState.Value = LevelLink_SLOT_UNLOCK_STATE.MISSION;
            return;
        }

        // 2) 필요 골드를 보유했는지
        if( CheckCost() == false)
        {
            CurrentState.Value = LevelLink_SLOT_UNLOCK_STATE.COST;
            return;
        }

        // 3) 필요 재료를 모두 보유했는지
        if( CheckMaterial() == false)
        {
            CurrentState.Value = LevelLink_SLOT_UNLOCK_STATE.MATERIAL;
            return;
        }


        // 4) 모든 조건 충족 → 해금 가능
        CurrentState.Value = LevelLink_SLOT_UNLOCK_STATE.UNLOCK;
    }

    /// <summary>
    /// 구매 버튼 모양용 상태(ButtonState) 계산. 골드만 검사한다 (미션/재료는 버튼 모양에 영향 없음).
    /// </summary>
    private void SetButtonState()
    {
        if( CheckCost() )
        {
            ButtonState = LevelLink_SLOT_UNLOCK_STATE.UNLOCK;
        }
        else
        {
            ButtonState = LevelLink_SLOT_UNLOCK_STATE.COST;
        }

    }

#region check func
    /// <summary>
    /// 해금 조건 미션을 모두 달성했는지 검사한다.
    /// 퀘스트 테이블 목록(level_link_condition_index 그룹)을 순회하며, 서버 미션 데이터(MissionList)의
    /// 현재 진행도(cur_point)가 목표치(end_point)에 못 미치는 것이 하나라도 있으면 false.
    /// </summary>
    private bool CheckMissionClear()
    {
        if( setting == null ) return false;
        if( setting.TLevelLinkData == null ) return false;

        ///미션 클리어 체크
        var questList = CQuestDataManager.Instance.GetQuestListDatas(setting.TLevelLinkData.level_link_condition_index);
        int levelLinkID = setting.TLevelLinkData.level_link_id;
        bool isClear = true;
        
        for(int i = 0; i < questList.Count; i++)
        {
            var quest = questList[i];
            // 테이블의 quest_id(문자열 등)를 int로 변환 → 서버 미션 데이터 조회 키로 사용
            int questID = quest.quest_id.ToInt32();
            // LevelLinkManager.SMissionList[levelLinkID][questID] 조회 (없으면 null 반환)
            var sMissionData = LevelLinkManager.Instance.GetLevelLinkMissionListData(levelLinkID, questID);
            // [참고] 서버 미션 데이터가 없으면 sMissionData가 null이라 아래 비교에서 NullReferenceException이 날 수 있다 (MissionListUI.SetUI도 동일)
            // 진행도가 목표 미만인 미션이 하나라도 있으면 미달성 → 즉시 중단
            if( sMissionData.cur_point < sMissionData.end_point)
            {
                isClear = false;
                break;
            }
        };

        return isClear;
    }


    /// <summary>
    /// 해금 재료를 모두 보유했는지 검사한다.
    /// 재료마다 LevelLinkManager.CheckItemCount(보유 수량 >= 필요 수량)를 확인하고, 하나라도 부족하면 false.
    /// 재료 목록이 비어 있으면 true.
    /// </summary>
    private bool CheckMaterial()
    {
        //재료 체크
        var materialList = setting.TLevelLinkData.level_link_extend_list;
        bool isClear = true;
        
        for( int i = 0 ; i < materialList.Count; i++)
        {
            bool check = LevelLinkManager.Instance.CheckItemCount(materialList[i]);
            if( check == false)
            {
                isClear = false;
                break;
            }
        }

        return isClear;
    }

    /// <summary>
    /// 해금에 필요한 골드(level_link_gold_value)를 현재 보유 골드가 충족하는지 검사한다.
    /// </summary>
    private bool CheckCost()
    {
        //골드 체크
        return setting.TLevelLinkData.level_link_gold_value <= CPlayer.Instance.Gold.Value;
    }
    #endregion

    /// <summary>
    /// 재료 부족 상태에서 구매 버튼을 눌렀을 때 호출된다. 부족한 재료를 젬으로 구매하는 흐름:
    /// 1) 재료 구매 팝업(PopupLevelLinkBuy) 표시 → 2) 확인 시 젬 사용 확인 팝업(젬 부족 시 젬 상점 안내)
    /// → 3) 사용 확인 시 재료 구매 API(RequestItemBuy) 호출 → 4) 성공하면 이 팝업 UI 갱신.
    /// </summary>
    private void OpenShopItemPopup()
    {
        // 재료별 필요/부족 수량과 개당 젬 가격 목록
        List<CBuyItemData> buyList = GetBuyItemDataList();

        var popup = CCoreServices.GetCoreService<CPopupService>().OpenPopupLevelLinkBuy();
        popup.SetData(new PopupLevelLinkBuy.Setting()
        {
            BuyList = buyList,

        });

        // 체인 마지막 Subscribe에서 직접 Dispose하기 위한 1회용 Disposable
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        // ShowAsObservable: 구매 팝업이 닫힐 때 결과(PopupLevelLinkBuy.Result)를 1회 발행
        disposable.Disposable = popup.ShowAsObservable()
        // SelectMany ①: 구매 팝업 결과 → 젬 사용 단계 Observable로 이어 붙인다
        .SelectMany(result =>
        {
            // 구매 팝업에서 확인을 눌렀으면 총 젬 비용으로 젬 사용 확인 팝업을 띄운다
            // (보유 젬이 부족하면 OpenPopupUseCash 내부에서 "재화 부족 → 젬 상점" 흐름으로 가고 IsOK=false가 온다)
            if( result.IsSuccess)
            {
                return LevelLinkManager.Instance.OpenPopupUseCash(result.TotalCost);
            }

            // 취소했으면 IsOK=false인 기본 결과를 바로 흘려보낸다
            return Observable.Return(new CashShopResult());
        })
        // SelectMany ②: 젬 사용 확인 결과 → 서버 구매 요청 Observable로 이어 붙인다
        .SelectMany(result =>
        {
            if( result.IsOK == true )
            {
                //재료 구매 API 호출
                // 서버 응답 e == 0 이면 true를 발행
                return LevelLinkManager.Instance.RequestItemBuy();
            }

           return Observable.Return(false);
 
        })
        .Subscribe(result=>
        {
            // 구매 성공 시 재료 보유량이 바뀌었으므로 재료 목록과 해금 상태를 다시 계산
            if( result == true )
            {
                SetUI();
            }
            // 결과 1회 처리 후 구독 해제
            disposable.Dispose();
        })
        .AddTo(this);
    }

    /// <summary>
    /// 재료 구매 팝업에 넘길 구매 목록을 만든다.
    /// 재료마다 보상 타입/ID/필요 수량/부족 수량/개당 젬 가격을 CBuyItemData로 채운다.
    /// 가격은 테이블의 비용 타입이 캐시(COST_CASH)일 때만 설정되고, 그 외에는 0.
    /// </summary>
    private List<CBuyItemData> GetBuyItemDataList()
    {
        List<CBuyItemData> buyList = new List<CBuyItemData>();

        var materialList = setting.TLevelLinkData.level_link_extend_list;
        for( int i = 0 ; i < materialList.Count; i++)
        {
            var material = materialList[i];

            // 테이블의 재료 타입 값 → 보상 타입(아이템/상품 등)으로 변환
            REWARD_TYPE rwType = material.level_link_extend_type.ConvertToRewardType();

            int itemID = material.level_link_extend_id;
            // 현재 인벤토리 보유 수량
            int currentCount = rwType.GetRewardCount(itemID);
            // 해금에 필요한 수량
            int needCount = material.level_link_extend_value;
            // 보상 타입에 맞는 테이블 데이터(ItemData 또는 ProductData)
            object itemData = CRewardTypeManager.GetRewardData(rwType, itemID);
            int cost = 0;

            switch(rwType)
            {
                // 일반 아이템: item_cost_type이 캐시면 item_cost_value를 개당 젬 가격으로 사용
                case REWARD_TYPE.RW_ITEM:
                {
                    ItemData data = itemData as ItemData;
                    if( data.item_cost_type.ToEnum<COMMON_CATEGORY>() == COMMON_CATEGORY.COST_CASH)
                    {
                        cost = data.item_cost_value;
                    }
                }
                break;
                // 상품(Product): instant_cost_type이 캐시면 instant_cost_value를 개당 젬 가격으로 사용
                case REWARD_TYPE.RW_PRODUCT:
                {
                    ProductData data = itemData as ProductData;
                    if( data.instant_cost_type.ToEnum<COMMON_CATEGORY>() == COMMON_CATEGORY.COST_CASH)
                    {
                        cost = data.instant_cost_value;
                    }
                }
                break;
            }

            var buyData = new CBuyItemData()
            {
                RWType = rwType,
                ItemID = itemID,
                NeedCount = needCount,

                // 구매해야 할 수량 = 필요 수량 - 보유 수량
                // [참고] 이미 충분히 보유한 재료도 목록에 포함되며, 이 경우 BuyCount가 0 이하가 된다 (음수 처리는 구매 팝업 쪽에 달려 있음)
                BuyCount = needCount - currentCount,
                GemCost = cost,
            };

            buyList.Add(buyData);
        }

        return buyList;
    }

    /// <summary>
    /// 골드 부족 상태에서 구매 버튼을 눌렀을 때 호출된다.
    /// "재화가 부족합니다. 구매하러 가시겠습니까?" 2버튼 알림을 띄우고, 확인 시 골드 상점을 연다.
    /// </summary>
    private void OpenPopupGoldLack()
    {
        var alert = CCoreServices.GetCoreService<CPopupService>().NoticeAlertByKey("UI_common_pop_dontbuy", PopupAlert.BUTTON_TYPE.BTN_TWO);    //재화가 부족합니다. 구매하러 가시겠습니까?
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        // 알림 팝업이 닫힐 때 1회 결과 수신
        disposable.Disposable = alert.ShowAsObservable()
        .Subscribe(res=>
        {
            // 확인(IsSucess)을 눌렀을 때만 골드 상점으로 이동
            if( res.IsSucess )
            {
                OpenPopupGoldShop();
            }
            disposable.Dispose();

        }).AddTo(this);

    }
    /// <summary>
    /// 골드 카테고리로 HUD 상점 팝업을 연다. 닫힌 뒤 별도 처리는 없고,
    /// 골드가 바뀌었다면 InitUI의 CPlayer.Gold 구독이 상태/버튼을 자동으로 갱신한다.
    /// </summary>
    private void OpenPopupGoldShop()
    {
        //골드샵 이동
        var popup = CCoreServices.GetCoreService<CPopupService>().OpenPopupHudShop(GOODS_CATEGORY_TYPE.SHOP_CATEGORY_GOLD);

        SingleAssignmentDisposable dispose = new SingleAssignmentDisposable();
        // 상점이 닫히면 구독만 해제
        dispose.Disposable = popup.ShowAsObservable()
        .Subscribe(res=>
        {
            dispose.Dispose();

        }).AddTo(this);
    }


    /// <summary>
    /// 팝업 입력 데이터.
    /// </summary>
    public class Setting
    {
        // 해금하려는 슬롯의 테이블 데이터 (level_link_id, level_link_condition_index: 해금 미션 그룹,
        // level_link_extend_list: 필요 재료 목록, level_link_gold_value: 필요 골드)
        public LevelLinkList TLevelLinkData;
    }

    /// <summary>
    /// 팝업 결과. 닫힐 때 호출부(PageLevelLink.OpenPopupLevelLinkUnlock)로 1회 전달된다.
    /// </summary>
    public class Result
    {
        // true: 모든 해금 조건을 만족한 상태에서 구매 버튼을 눌러 닫힘 → 호출부가 슬롯 추가 API 호출
        // false: 닫기/딤/백키로 닫힘
        public bool IsSuccess = false;
    }

}
