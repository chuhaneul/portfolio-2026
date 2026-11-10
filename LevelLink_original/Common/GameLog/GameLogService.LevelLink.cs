using Newtonsoft.Json;
using Game.RestAPI;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 게임 로그 서비스(GameLogService)의 LevelLink 부분 (partial class).
/// LevelLinkService의 각 API 응답 처리(Do)에서 호출되어, LevelLink 관련 행동을 게임 로그 서버로 보낸다.
///
/// 모든 메서드의 공통 흐름:
///  1) GetRoot(콘텐츠 = LevelLink, 로그 타입)으로 로그 루트(콘텐츠/로그 타입/플레이 시간/타임스탬프) 생성
///  2) GameLog.user_info.GetLogWithSessionID()로 사용자 정보 + 세션 ID가 담긴 logdata Dictionary 생성
///  3) 필요하면 SetResInfo()로 재화 변동(reward_list)을 "resource_info" 키에 추가, LevelLink ID 등 추가 필드 기록
///  4) logdata를 JSON으로 직렬화해 root.logdata에 넣고 Log()로 전송 (Log는 CDataManager.GAME_LOG == 1일 때만 실제 전송)
/// </summary>
public partial class GameLogService : ICoreService
{
    // logdata에 넣는 키 이름들
    // LevelLink ID (이름은 slot이지만 값은 LevelLink gdid)
    private const string Slot_ID = "slot_id";
    // 등록한 카드 gdid
    private const string Card_ID = "card_id";
    // 덱에 저장된 카드 gdid 목록
    private const string Card_List = "card_list";
    
    /// <summary>
    /// LevelLink 해금(슬롯 오픈) 로그. LevelLinkService.ReqLevelLinkSlotAdd() 응답에서 호출된다.
    /// 기록: 재화 변동(res.c.reward_list), LevelLink ID
    /// </summary>
    public void LevelLinkSlotOpen(Level_linkSlot_addRes.Root res, int gdid)
    {
        var root = GetRoot(GameLog.Contents.LevelLink, GameLog.LogType.LevelLinkSlotOpen);
        var logdata = GameLog.user_info.GetLogWithSessionID();

        // 해금에 소모된 재화 등 reward_list를 resource_info로 기록
        SetResInfo(logdata, res.c.reward_list);

        logdata.Add(Slot_ID, gdid);

        root.logdata = JsonConvert.SerializeObject(logdata);
        Log(root);        
    }

    /// <summary>
    /// LevelLink 덱 저장 로그. LevelLinkService.ReqLevelLinkDeckSave() 응답에서 호출된다.
    /// 기록: 재화 변동, LevelLink ID, 저장된 덱 카드 gdid 목록(빈 슬롯 제외)
    /// </summary>
    public void LevelLinkDeckSave(Level_linkDeck_saveRes.Root res, int gdid)
    {
        var root = GetRoot(GameLog.Contents.LevelLink, GameLog.LogType.DeckSave);
        var logdata = GameLog.user_info.GetLogWithSessionID();

        SetResInfo(logdata, res.c.reward_list);

        List<int> cardIDList = new List<int>();

        // 응답 LevelLink 리스트의 첫 번째 항목을 저장한 LevelLink로 보고 덱 카드를 읽는다
        // [참고] 응답 리스트에 여러 LevelLink가 오거나 순서가 다르면 다른 LevelLink의 덱이 기록될 수 있고, 리스트가 비어 있으면 levelLink이 null이라 아래에서 예외가 난다 (gdid로 찾지 않음)
        var levelLink = res.d.level_link_list.FirstOrDefault();

        // card_1 ~ card_7 중 카드가 있는 슬롯(0보다 큰 값)만 목록에 추가
        if( levelLink.card_1 > 0 ) cardIDList.Add(levelLink.card_1);
        if( levelLink.card_2 > 0 ) cardIDList.Add(levelLink.card_2);
        if( levelLink.card_3 > 0 ) cardIDList.Add(levelLink.card_3);
        if( levelLink.card_4 > 0 ) cardIDList.Add(levelLink.card_4);
        if( levelLink.card_5 > 0 ) cardIDList.Add(levelLink.card_5);
        if( levelLink.card_6 > 0 ) cardIDList.Add(levelLink.card_6);
        if( levelLink.card_7 > 0 ) cardIDList.Add(levelLink.card_7);
        
        logdata.Add(Slot_ID, gdid);
        logdata.Add(Card_List, cardIDList);

        root.logdata = JsonConvert.SerializeObject(logdata);
        Log(root);        

    }

    /// <summary>
    /// 중앙 LevelLink 슬롯 카드 등록 로그. LevelLinkService.ReqLevelLinkSlotSet() 응답에서 호출된다.
    /// 기록: LevelLink ID, 등록된 카드 gdid(응답 LevelLink 데이터의 slot 값). 재화 변동은 기록하지 않는다.
    /// </summary>
    public void RegisterLevelLinkCard(Level_linkSlot_setRes.Root res, int gdid)
    {
        var root = GetRoot(GameLog.Contents.LevelLink, GameLog.LogType.RegisterLevelLinkCard);
        var logdata = GameLog.user_info.GetLogWithSessionID();

        logdata.Add(Slot_ID, gdid);
        // 응답 LevelLink 리스트 첫 번째 항목의 slot(중앙 슬롯 카드 gdid)을 등록 카드로 기록
        // [참고] LevelLinkDeckSave와 같이 첫 번째 항목을 대상 LevelLink로 가정하며, 리스트가 비어 있으면 NullReferenceException이 난다
        logdata.Add(Card_ID, res.d.level_link_list.FirstOrDefault().slot);

        root.logdata = JsonConvert.SerializeObject(logdata);
        Log(root);          

    }
    /// <summary>
    /// LevelLink 카드 등록 해제 로그. LevelLink ID만 기록한다.
    /// 호출: LevelLinkService.ReqLevelLinkSlotUnsetCard()(중앙 슬롯 해제), ReqLevelLinkResetAll()(덱 초기화도 이 로그 타입을 사용)
    /// </summary>
    public void UnRegisterLevelLinkCard(int gdid)
    {
        var root = GetRoot(GameLog.Contents.LevelLink, GameLog.LogType.UnRegisterLevelLinkCard);
        var logdata = GameLog.user_info.GetLogWithSessionID();
        
        logdata.Add(Slot_ID, gdid);

        root.logdata = JsonConvert.SerializeObject(logdata);
        Log(root);          
    }

    /// <summary>
    /// 쿨타임 보석 즉시 완료 로그. LevelLinkService.ReqLevelLinkReduceTime()에서 itemID == 1(보석)일 때 호출된다.
    /// 기록: 재화 변동(res.c.reward_list, 소모 보석 등), LevelLink ID
    /// </summary>
    public void CoolTimeImmediateComplete(Level_linkReduce_timeRes.Root res, int gdid)
    {
        var root = GetRoot(GameLog.Contents.LevelLink, GameLog.LogType.CoolTimeImmediateComplete);
        var logdata = GameLog.user_info.GetLogWithSessionID();
        SetResInfo(logdata, res.c.reward_list);
        logdata.Add(Slot_ID, gdid);

        root.logdata = JsonConvert.SerializeObject(logdata);
        Log(root);          
    }

    /// <summary>
    /// 쿨타임 시간 단축 로그 (티켓 사용 또는 광고 시청). LevelLinkService.ReqLevelLinkReduceTime()에서 itemID != 1일 때 호출된다.
    /// 기록: 재화 변동(rewardList = res.c.reward_list), LevelLink ID
    /// </summary>
    public void LevelLinkReduceTime(List<RewardList> rewardList, int gdid)
    {
        var root = GetRoot(GameLog.Contents.LevelLink, GameLog.LogType.ReduceTime);
        var logdata = GameLog.user_info.GetLogWithSessionID();
        SetResInfo(logdata, rewardList);
        logdata.Add(Slot_ID, gdid);

        root.logdata = JsonConvert.SerializeObject(logdata);
        Log(root);
    }    

    /// <summary>
    /// 해금 재료(상품) 구매 로그. LevelLinkService.RequestLevelLinkSlotBuy() 응답에 product_list가 있을 때 호출된다.
    /// 기록: 재화 변동만 (LevelLink ID는 기록하지 않음)
    /// </summary>
    public void LevelLinkProductbuy(List<RewardList> rewardList)
    {
        var root = GetRoot(GameLog.Contents.LevelLink, GameLog.LogType.Productbuy);
        var logdata = GameLog.user_info.GetLogWithSessionID();

        SetResInfo(logdata, rewardList);
        root.logdata = JsonConvert.SerializeObject(logdata);
        Log(root);
    }
    
    /// <summary>
    /// 해금 재료(아이템) 구매 로그. LevelLinkService.RequestLevelLinkSlotBuy() 응답에 item_list가 있을 때 호출된다.
    /// 기록: 재화 변동만. 다른 로그들과 달리 rewardList가 null이면 로그를 보내지 않는다.
    /// </summary>
    public void LevelLinkBuyItem(List<RewardList> rewardList)
    {
        var root = GetRoot(GameLog.Contents.LevelLink, GameLog.LogType.ItemBuy);
        var logdata = GameLog.user_info.GetLogWithSessionID();

        SetResInfo(logdata, rewardList);
        // 재화 변동 정보가 있을 때만 전송
        if (rewardList != null)
        {
            root.logdata = JsonConvert.SerializeObject(logdata);
            Log(root);
        }
    }        

}
