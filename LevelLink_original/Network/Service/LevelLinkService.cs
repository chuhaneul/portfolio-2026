using System;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using Newtonsoft.Json;
using Game.RestAPI;



/// <summary>
/// LevelLink 서버 API 서비스. APIHelper.LevelLinkService로 접근한다.
/// 각 메서드는 GameRestAPI의 level_link/* 엔드포인트를 호출하는 Observable을 반환하며,
/// Do()에서 응답으로 로컬 데이터(LevelLinkManager, 카드/아이템 인벤토리)를 갱신하고 게임 로그(GameLogService)를 남긴다.
///
/// 공통 동작 (GameRestAPI.Post):
///  - Cold Observable이라 구독해야 요청이 나간다
///  - 서버 에러(e != 0)는 공통 처리에서 에러 팝업을 띄우고 Where로 걸러지므로, Do()와 구독자의 OnNext는 보통 성공 응답에서만 실행된다
///  - Do()는 구독자의 OnNext보다 먼저 실행되므로, 호출부(LevelLinkManager)의 Subscribe 시점에는 데이터가 이미 갱신되어 있다
///
/// 호출부: LevelLinkManager (ShowLevelLinkPage, Request* 메서드들)
/// </summary>
public class LevelLinkService 
{
    // 요청에 넣을 로그인 사용자 ID
    string UID { get { return CPlayer.GetStatusLoginValue(PLAYER_STATUS.USER_ID); } }

    /// <summary>
    /// LevelLink 정보 조회 (level_link/top).
    /// 응답: LevelLink 리스트(level_link_list) + 해금 미션 진행도 리스트(mission_list) → LevelLinkManager에 저장
    /// 호출: LevelLinkManager.ShowLevelLinkPage() (카드 정보 API 다음, 페이지 로드 전)
    /// </summary>
    public IObservable<Level_linkTopRes.Root> ReqLevelLinkInfo()
    {
        return GameRestAPI.Level_linkTop(UID)
        // Do: 값을 바꾸지 않고 부수 효과만 실행 (로컬 데이터 저장)
        .Do(res=>
        {
            LevelLinkManager.Instance.SetLevelLinkListData(res.d.level_link_list);
            LevelLinkManager.Instance.SetLevelLinkMissionListData(res.d.mission_list);
        });
    }

    /// <summary>
    /// 
    /// (원본 summary는 비어 있음 — 설명은 아래 remarks 참고)
    /// </summary>
    /// <param name="gdid"></param>
    /// <param name="deckList"></param> Dicionary<order, cardid>
    /// <returns></returns>
    /// <remarks>
    /// LevelLink 덱 저장 (level_link/deck_save).
    /// gdid: LevelLink ID, deckList: key 슬롯 번호(1~7) / value 카드 gdid
    /// 응답: LevelLink 리스트 + 변경된 카드 리스트(card_list, LevelLink 레벨 변동 반영용) → 로컬 갱신 후 덱 저장 로그
    /// 호출: LevelLinkManager.RequestLevelLinkDeckSave()
    /// </remarks>
    public IObservable<Level_linkDeck_saveRes.Root> ReqLevelLinkDeckSave(int gdid, Dictionary<int, int> deckList)
    {
        // 서버 API가 슬롯별 파라미터(card_1~card_7)를 받으므로 Dictionary를 풀어서 넘긴다. 없는 슬롯 번호는 0(빈 슬롯)
        int card1 = deckList.ContainsKey(1)? deckList[1] : 0;
        int card2 = deckList.ContainsKey(2)? deckList[2] : 0;
        int card3 = deckList.ContainsKey(3)? deckList[3] : 0;
        int card4 = deckList.ContainsKey(4)? deckList[4] : 0;
        int card5 = deckList.ContainsKey(5)? deckList[5] : 0;
        int card6 = deckList.ContainsKey(6)? deckList[6] : 0;
        int card7 = deckList.ContainsKey(7)? deckList[7] : 0;

        // 프로토콜 파라미터는 모두 문자열이라 ToString()으로 변환해서 넘긴다
        return GameRestAPI.Level_linkDeck_save(UID, gdid.ToString(), card1.ToString(), card2.ToString(), card3.ToString(), card4.ToString(), card5.ToString(), card6.ToString(), card7.ToString())
        .Do(res=>
        {
            // 카드 정보가 함께 오면 카드 인벤토리에 반영 (level_link_lv 등 서버 값 갱신)
            if( res.d.card_list != null )
            {
                CardInventoryManager.Instance.AddCardServerInfoList(res.d.card_list);
            }
            // LevelLink 데이터 갱신 (덱 카드 card_1~7, LevelLink 레벨 lv 등)
            LevelLinkManager.Instance.SetLevelLinkListData(res.d.level_link_list);
            // 덱 저장 게임 로그 (저장된 덱 카드 목록 포함)
            CCoreServices.GetCoreService<GameLogService>().LevelLinkDeckSave(res, gdid);


        });
    }

    /// <summary>
    /// LevelLink 덱 초기화 (level_link/reset_all). 해당 LevelLink 덱 카드를 모두 해제한다.
    /// 응답: LevelLink 리스트 → 로컬 갱신 후 로그
    /// 호출: LevelLinkManager.RequestLevelLinkReset()
    /// </summary>
    public IObservable<Level_linkReset_allRes.Root> ReqLevelLinkResetAll(int gdid)
    {
        return GameRestAPI.Level_linkReset_all(UID, gdid.ToString())
        .Do(res=>
        {
            LevelLinkManager.Instance.SetLevelLinkListData(res.d.level_link_list);
            // 초기화도 "LevelLink 카드 등록 해제" 로그 타입으로 남긴다
            // (reset_all 응답 프로토콜에는 card_list가 없어서, 덱 저장 / 등록 / 해제와 달리 여기서는 카드 인벤토리를 갱신하지 않는다)
            CCoreServices.GetCoreService<GameLogService>().UnRegisterLevelLinkCard(gdid);

        });
    }

    /// <summary>
    /// LevelLink 해금 / 슬롯 추가 (level_link/slot_add).
    /// 응답: LevelLink 리스트(state가 해금으로 바뀐 데이터) → 로컬 갱신 후 슬롯 오픈 로그(소모 재화 reward_list 포함)
    /// 호출: LevelLinkManager.RequestSlotAdd()
    /// </summary>
    public IObservable<Level_linkSlot_addRes.Root> ReqLevelLinkSlotAdd(int gdid)
    {
        return GameRestAPI.Level_linkSlot_add(UID, gdid.ToString())
        .Do(res=>
        {
            LevelLinkManager.Instance.SetLevelLinkListData(res.d.level_link_list);
            CCoreServices.GetCoreService<GameLogService>().LevelLinkSlotOpen(res, gdid);

        });
    }
    /// <summary>
    /// LevelLink 해금 재료 구매 (level_link/slot_buy). 부족한 재료를 보석으로 구매한다.
    /// 응답: 아이템 리스트(item_list) 및/또는 상품 리스트(product_list) → 각각 인벤토리 갱신 후 구매 로그
    /// 호출: LevelLinkManager.RequestItemBuy()
    /// </summary>
    public IObservable<Level_linkSlot_buyRes.Root> RequestLevelLinkSlotBuy(int gdid)
    {
        return GameRestAPI.Level_linkSlot_buy(UID, gdid.ToString())
        .Do(res=>
        {
            // 일반 아이템 재료를 구매한 경우: 아이템 인벤토리 갱신 + 아이템 구매 로그
            if( res.d.item_list != null )
            {
                ItemInventoryManager.Instance.SetItemListData(res.d.item_list);
                CCoreServices.GetCoreService<GameLogService>().LevelLinkBuyItem(res.c.reward_list);
            }

            // 상품(Product) 재료를 구매한 경우: 상품 데이터 갱신 + 상품 구매 로그
            // [참고] item_list와 product_list가 둘 다 오면 같은 reward_list로 로그가 두 번(ItemBuy, Productbuy) 기록된다
            if( res.d.product_list != null )
            {
                APIHelper.ProductsService.UpdatehProductsData(res.d.product_list);
                CCoreServices.GetCoreService<GameLogService>().LevelLinkProductbuy(res.c.reward_list);
            }

        });
    }

    /// <summary>
    /// 중앙 LevelLink 슬롯에 카드 등록 (level_link/slot_set).
    /// gdid: LevelLink ID, cardID: 등록할 카드 gdid
    /// 응답: LevelLink 리스트(slot 갱신) + 카드 리스트(등록 카드의 level_link_lv 갱신) → 로컬 갱신 후 등록 로그
    /// 호출: LevelLinkManager.RequestLevelLinkCardSave()
    /// </summary>
    public IObservable<Level_linkSlot_setRes.Root> ReqLevelLinkSlotSet(int gdid, int cardID)
    {
        return GameRestAPI.Level_linkSlot_set(UID, gdid.ToString(), cardID.ToString())
        .Do(res=>
        {
            LevelLinkManager.Instance.SetLevelLinkListData(res.d.level_link_list);
            // [참고] deck_save와 달리 card_list null 검사 없이 넘긴다. AddCardServerInfoList는 null을 검사하지 않으므로 card_list가 null이면 NullReferenceException이 난다 (slot_unset_card도 동일)
            CardInventoryManager.Instance.AddCardServerInfoList(res.d.card_list);

            CCoreServices.GetCoreService<GameLogService>().RegisterLevelLinkCard(res, gdid);

        });
    }

    /// <summary>
    /// 중앙 LevelLink 슬롯 카드 등록 해제 (level_link/slot_unset_card).
    /// 응답: LevelLink 리스트(slot 해제, remain_time 쿨타임) + 카드 리스트(해제 카드의 level_link_lv 복원) → 로컬 갱신 후 해제 로그
    /// 호출: LevelLinkManager.RequestLevelLinkCardRemove()
    /// </summary>
    public IObservable<Level_linkSlot_unset_cardRes.Root> ReqLevelLinkSlotUnsetCard(int gdid)
    {
        return GameRestAPI.Level_linkSlot_unset_card(UID, gdid.ToString())
        .Do(res=>
        {
            LevelLinkManager.Instance.SetLevelLinkListData(res.d.level_link_list);
            CardInventoryManager.Instance.AddCardServerInfoList(res.d.card_list);

            CCoreServices.GetCoreService<GameLogService>().UnRegisterLevelLinkCard(gdid);
        });
    }

    /// <summary>
    /// LevelLink 쿨타임 단축 / 즉시 완료 (level_link/reduce_time).
    /// itemID — 1: 보석 즉시 완료, 그 외 양수: 시간 단축 티켓 아이템 gdid, 0: 아이템 미사용(광고)
    /// ad_id  — 광고 단축이면 AD_TYPE_LEVELLINK_SLOT 값, 아니면 0
    /// 응답: LevelLink 리스트(remain_time 갱신) → 로컬 갱신, 쿨타임이 끝났으면 TimeStream 제거, 단축 방식에 맞는 로그
    /// 호출: LevelLinkManager.ReqLevelLinkReduceTime() ← PopupLevelLinkInstantcomplete
    /// </summary>
    public IObservable<Level_linkReduce_timeRes.Root> ReqLevelLinkReduceTime(int gdid, int itemID, int ad_id)
    {
        return GameRestAPI.Level_linkReduce_time(UID, gdid.ToString(), itemID.ToString(), ad_id.ToString())
        .Do(res=>
        {
            LevelLinkManager.Instance.SetLevelLinkListData(res.d.level_link_list);
            // 갱신된 remain_time이 0 이하이면 현재 LevelLink의 쿨타임 TimeStream을 제거
            // (CheckTimeStream은 인자의 gdid가 아니라 LevelLinkManager의 CurrentGDID 기준으로 검사한다. 호출부가 CurrentPageGDID를 넘기므로 같은 값)
            LevelLinkManager.Instance.CheckTimeStream();

            // itemID == 1(보석 즉시 완료)이면 즉시 완료 로그, 그 외(티켓 / 광고)는 시간 단축 로그(보상/소모 reward_list 포함)
            if( itemID == 1 ) //즉시완료
            {
                CCoreServices.GetCoreService<GameLogService>().CoolTimeImmediateComplete(res, gdid);
            }
            else
            {
                CCoreServices.GetCoreService<GameLogService>().LevelLinkReduceTime(res.c.reward_list, gdid);
            }

        });
    }


}
