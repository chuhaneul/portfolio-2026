using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.RestAPI;
using UniRx;
using System.Linq;
using System;

/// <summary>
/// LevelLink 한 칸(LevelLink ID 하나)의 현재 상태.
/// LevelLinkManager.GetStateLevelLink()이 서버 데이터(LevelLinkList)의 state / remain_time / slot 값을 보고 계산한다.
/// PageLevelLink(중앙 UI, 잠금 UI), PageLevelLink_Bottom(하단 슬롯 목록) 등이 이 값으로 화면을 분기한다.
/// 판정 우선순위: LOCK → COOLTIME → SETTING → EMPTY
/// </summary>
public enum LevelLink_Slot_State
{
    // 해금은 되었지만 중앙 LevelLink 슬롯(LevelLinkList.slot)에 등록된 카드가 없고 쿨타임도 없는 상태
    EMPTY,
    // 중앙 LevelLink 슬롯에 카드가 등록된 상태 (LevelLinkList.slot > 0)
    SETTING,
    // 카드 등록 해제 후 재등록까지 남은 쿨타임이 있는 상태 (LevelLinkList.remain_time > 0)
    COOLTIME,
    // 아직 해금(슬롯 추가)되지 않은 상태 (LevelLinkList.state == 0)
    LOCK,
}

/// <summary>
/// LevelLink 해금(슬롯 추가) 팝업(PopupLevelLinkUnlock)에서 사용하는 해금 가능 여부 상태.
/// PopupLevelLinkUnlock이 미션 → 골드 → 재료 순서로 검사해서 처음 실패한 조건을 상태로 정한다.
/// 구매 버튼을 눌렀을 때 이 상태에 따라 토스트/재료 구매/골드 상점 이동/해금 진행으로 분기한다.
/// </summary>
public enum LevelLink_SLOT_UNLOCK_STATE
{
    // 초기값 (아직 검사 전). 구매 버튼 클릭 시에는 UNLOCK과 같이 해금 진행으로 처리된다
    NONE = 0,
    UNLOCK, //언락 가능
    MISSION, // 미션 미 달성
    MATERIAL, // 재료 부족
    COST, // 재화 부족
}

/// <summary>
/// 쿨타임 단축 팝업(PopupLevelLinkInstantcomplete)에 표시되는 단축 수단 한 줄의 종류.
/// ObjLevelLinkReslotoption이 이 값으로 버튼/광고/티켓 UI 중 하나만 켜고,
/// PopupLevelLinkInstantcomplete가 이 값으로 서버에 보낼 itemID / adID를 결정한다.
/// </summary>
public enum SHORT_TIME_UI_TYPE
{
    // 보석(젬)으로 즉시 완료. 서버에는 itemID = 1 로 요청한다
    NORMAL = 0,
    // 광고 시청으로 단축. 서버에는 adID = AD_TYPE_LEVELLINK_SLOT 값으로 요청한다
    AD = 1,
    // 시간 단축 티켓 아이템 사용. 서버에는 itemID = 티켓 아이템 gdid 로 요청한다
    TICKET = 2,
}

/// <summary>
/// 쿨타임 단축 팝업의 리스트 한 줄(단축 수단 하나)에 대한 데이터.
/// LevelLinkManager.InitShortTimeList()에서 광고 → 즉시완료(보석) → 티켓(1~3번) 순서로 만들어 ShortTimeList에 담고,
/// ObjLevelLinkInstantcomplete_Bottom이 GetShortTimeData(index)로 꺼내 ObjLevelLinkReslotoption에 넘긴다.
/// UIType에 따라 ConfigData / AdData / TicketData 중 하나만 채워진다.
/// </summary>
public class LevelLinkSlot_ShortTime
{
    // 이 줄이 어떤 단축 수단인지 (보석 / 광고 / 티켓)
    public SHORT_TIME_UI_TYPE UIType = SHORT_TIME_UI_TYPE.NORMAL;

    // NORMAL일 때만 사용: 게임 설정 "levellink_cash_shorten_payment" 값 (ObjLevelLinkReslotoption_Button에서 몇 초당 보석 1개인지 계산하는 데 사용)
    public GameConfigData ConfigData = null;
    // AD일 때만 사용: LevelLink 슬롯용 광고 테이블 데이터
    public AdvertiseData AdData = null;
    // TICKET일 때만 사용: 인벤토리에 있는 시간 단축 티켓 아이템 (gdid, 보유 수량)
    public ItemList TicketData = null;
    // NORMAL: 현재 LevelLink의 쿨타임 TimeStream 키("LEVELLINK_SLOT_{gdid}"), AD: "LEVELLINK_SLOT_AD", TICKET: 설정하지 않음("")
    public string STR_TIMER_NAME = "";

    // NORMAL일 때 버튼 클릭 시점에 ObjLevelLinkReslotoption.OnClickEvent(int)가 남은 시간으로 계산한 보석 비용을 채워 넣는다
    public int GemCost = 0;
}

/// <summary>
/// 보석 사용 확인 흐름(LevelLinkManager.OpenPopupUseCash)의 결과.
/// PopupLevelLinkInstantcomplete(쿨타임 즉시 완료)와 PopupLevelLinkUnlock(해금 재료 구매)에서
/// IsOK가 true일 때만 다음 서버 요청으로 이어간다.
/// </summary>
public class CashShopResult
{
    // 보유 보석이 비용 이상인지 (보석 상점을 다녀온 경우에는 다녀온 뒤의 보유량 기준)
    public bool EnoughGem = false;
    // 보석 부족으로 "재화 부족" 안내 흐름을 탔는지 (실제로 상점을 열었는지와는 별개로 그 흐름에 들어가면 true)
    public bool OpenGemShop = false;
    
    // 사용자가 보석 사용 확인 팝업에서 확인을 눌렀는지. true일 때만 호출부가 서버 요청을 진행한다
    public bool IsOK = false;

}


//LevelLink 쿨타임 업데이트로 인하여 UI변경이 필요할 경우 호출한다
// (UniRx MessageBroker로 발행/구독하는 이벤트 메시지)
// 발행: ObjLevelLinkSlot_Timer(쿨타임 TimeStream이 끝났을 때), PageLevelLink.popup(쿨타임 단축 팝업이 성공으로 닫혔을 때)
// 구독: PageLevelLink(중앙/하단 UI 갱신), PopupLevelLinkInstantcomplete(IsEndTime이면 팝업 닫기)
public struct LevelLinkPageUpdate_CoolTime
{
    // 쿨타임이 끝났는지 여부. 기본값 true (단축 성공 시에도 인자 없이 발행하므로 true)
    public bool IsEndTime;
    public LevelLinkPageUpdate_CoolTime(bool isEndTime = true)
    {
        IsEndTime = isEndTime;
    }
}

//현재 LevelLink ID가 변경되었을 경우 호출한다
// (MessageBroker 이벤트 메시지. 실제로는 ID 변경뿐 아니라 서버 데이터가 갱신되어 화면을 다시 그려야 할 때도 같은 ID로 발행된다)
// 발행: CurrentPageGDID setter, 덱 저장/초기화/카드 등록/등록 해제 API 성공 후, PageLevelLink(카드 데이터 갱신, 해금 연출 완료)
// 구독: PageLevelLink → SetData(IsChangeGDID) 후 중앙/하단/잠금 UI 전체 갱신
public struct LevelLinkSelectGDID
{
    // 현재 선택된 LevelLink ID (level_link_id)
    public int GDID;
    // 이전 값과 다른 LevelLink로 바뀌었는지. PageLevelLink에서 중앙 UI 타이머 리셋 여부(isResetTimer)로 사용된다
    public bool IsChangeGDID;

    public LevelLinkSelectGDID(int gdid, bool isChange = false)
    {
        GDID = gdid;
        IsChangeGDID = isChange;
    }
}

/// <summary>
/// LevelLink 해금(슬롯 추가) API가 성공했을 때 발행되는 MessageBroker 이벤트 메시지.
/// 발행: LevelLinkManager.RequestSlotAdd()
/// 구독: PageLevelLink → 잠금 UI를 해금 연출 모드(SetData_LockUI(true))로 다시 그린다.
/// 연출이 끝나면 PageLevelLink.OnCompleteUnlockAni()가 LevelLinkSelectGDID를 발행해 전체 화면을 갱신한다.
/// </summary>
public struct LevelLinkUnLockSlot
{
    // 해금된 LevelLink ID
    public int GDID;

    public LevelLinkUnLockSlot(int gdid)
    {
        GDID = gdid;
    }

}


/// <summary>
/// LevelLink 기능의 핵심 매니저 (Singleton: 처음 Instance에 접근할 때 생성되어 런타임 동안 유지).
///
/// [역할]
/// - 서버 데이터 보관: LevelLink별 상태/덱/중앙 슬롯 카드/쿨타임(SLevelLinkList), 해금 미션 진행도(SMissionList)
///   → LevelLinkService가 API 응답을 받을 때마다 SetLevelLinkListData / SetLevelLinkMissionListData로 갱신한다.
/// - 현재 화면에서 보고 있는 LevelLink ID(CurrentPageGDID) 관리 및 이전/다음 이동
/// - 카드 리스트 계산: 중앙 슬롯 등록 후보, 덱 편집 리스트(필터 + 정렬), 덱 자동 배치
/// - 서버 API 요청 래핑: 해금, 재료 구매, 덱 저장, 초기화, 카드 등록/해제, 쿨타임 단축
/// - 덱 편집 팝업의 멤버/속성 필터 상태 관리(SortFilter_Member, SortFilter_PZTYPE)
/// - 쿨타임 단축 수단 리스트(ShortTimeList) 구성
/// - 보석 사용 확인 / 보석 부족 → 보석 상점 이동 공통 팝업 흐름
///
/// [사용처]
/// PageLevelLink(+ .popup partial), LevelLink 하위 UI들, 각종 LevelLink 팝업, LevelLinkService, LobbyButtonManager(진입)
///
/// [데이터 용어]
/// - LevelLink ID / GDID: 테이블 LevelLinkList.level_link_id == 서버 LevelLinkList.gdid
/// - 덱 슬롯: LevelLink 하단의 카드 칸(최대 7개, card_1 ~ card_7). 칸 번호는 테이블 level_link_group_order(1~7)와 같다
/// - 중앙 LevelLink 슬롯: 덱 카드들의 최소 레벨(LevelLink 레벨, LevelLinkList.lv)을 적용받는 카드 1장 (LevelLinkList.slot)
/// </summary>
public class LevelLinkManager : Singleton<LevelLinkManager>
{

    // 화면에 표시할 LevelLink ID 목록 (테이블 level_link_order 오름차순). InitData()에서 채우고 이전/다음 이동 순서로 사용한다
    public List<int> LevelLinkUIList = new List<int>();
    // 서버 LevelLink 데이터. key: LevelLink gdid, value: 서버 LevelLinkList (state, lv, card_1~7, slot, remain_time)
    public Dictionary<int, LevelLinkList> SLevelLinkList = new Dictionary<int, LevelLinkList>();

    /// <summary>
    /// Dictionary<levellink gdid, Dictionary<mission gdid, MissionList>>
    /// </summary>
    public Dictionary<int, Dictionary<int, MissionList>> SMissionList = new Dictionary<int, Dictionary<int, MissionList>>();

    //member filter
    // 덱 편집 팝업의 멤버 필터 상태 (선택된 멤버 HashSet + 필터 UI 항목 목록). InitSortFilterData()에서 초기화된다
    private SortFilter_Member SortFilterMember = new SortFilter_Member();
    // 멤버 필터 UI 항목 목록 (ObjLevelLinkDeck_MidUI_SortFilter의 멤버 필터 스크롤에서 사용)
    public List<ObjsortListItem.Setting> GetSortMemberList { get { return SortFilterMember.GetDataList; } }

    //attribute pztype filter
    // 덱 편집 팝업의 속성(PZTYPE) 필터 상태
    private SortFilter_PZTYPE SortFilterPZTYPE = new SortFilter_PZTYPE();
    // 속성 필터 UI 항목 목록 (ObjLevelLinkDeck_MidUI_SortFilter의 속성 필터 스크롤에서 사용)
    public List<ObjsortListItem.Setting> GetSortPzTypeList { get { return SortFilterPZTYPE.GetDataList; } }

    //LevelLink 쿨타임 리스트
    // (정확히는 쿨타임 단축 팝업에 표시할 "단축 수단" 리스트. InitShortTimeList()에서 구성한다)
    private List<LevelLinkSlot_ShortTime> ShortTimeList = new List<LevelLinkSlot_ShortTime>();
    // 단축 수단 개수 (ObjLevelLinkInstantcomplete_Bottom의 스크롤 아이템 개수로 사용)
    public int GetShortTimeListCount => ShortTimeList.Count;


    // LevelLink 쿨타임 TimeStream 키의 접두사. 키 문자열에서 gdid를 다시 뽑아낼 때(UpdateLevelLinkListData_RemainTime_TimeKey) 사용
    public static readonly string LEVELLINK_SLOT_TIME_KEY = "LEVELLINK_SLOT_";
    // LevelLink별 쿨타임 TimeStream 키 형식. {0}에 LevelLink gdid가 들어간다 (예: "LEVELLINK_SLOT_3")
    public static readonly string LEVELLINK_SLOT_TIME_KEY_FORMAT = "LEVELLINK_SLOT_{0}";
    // 광고 단축 항목에 넣어 두는 타이머 이름 (GetShortTimeAD에서 STR_TIMER_NAME으로 설정)
    public static readonly string LEVELLINK_SLOT_AD = "LEVELLINK_SLOT_AD";

    // 현재 화면에서 보고 있는 LevelLink ID의 실제 저장 필드
    private int CurrentGDID = 0;
    /// <summary>
    /// 현재 화면에서 보고 있는 LevelLink ID.
    /// set 하면 값이 같더라도 항상 LevelLinkSelectGDID 메시지를 발행해서 PageLevelLink이 화면을 다시 그리게 한다.
    /// (메시지의 IsChangeGDID에는 이전 값과 달라졌는지가 들어간다)
    /// 설정하는 곳: SetLevelLinkCurrentPage(진입 시), PageLevelLink의 이전/다음 버튼, 하단 슬롯 클릭
    /// </summary>
    public int CurrentPageGDID
    {
        get
        {
            return CurrentGDID;
        }
        set
        {
            // 이전 값과 다른지 먼저 기록해 두고 값을 바꾼 뒤 이벤트 발행
            bool changed = CurrentGDID != value;
            CurrentGDID = value;
            MessageBroker.Default.Publish(new LevelLinkSelectGDID(CurrentGDID, changed));
        }
    }

    /// <summary>
    /// 덱 편집 팝업의 정렬 드롭다운에서 선택된 정렬 인덱스 (LevelLinkDeckSort.List의 인덱스. 0 = 레벨순).
    /// static ReactiveProperty라서 값이 바뀌면 구독 중인 PopupLevelLinkDeckManage가 덱 카드 리스트를 다시 정렬해 그린다.
    /// 값 변경: ObjLevelLinkDeck_MidUI_Sort(드롭다운 선택)
    /// </summary>
    public static ReactiveProperty<int> CurrentDeckSortType {get; set;} = new ReactiveProperty<int>(0);
    // 현재 정렬 인덱스 값 (GetLevelLinkCardDeckList에서 LevelLinkDeckSort.Get()에 넘긴다)
    public int CurrentDeckSortTypeValue { get { return CurrentDeckSortType.Value; } }
    // 정렬을 기본값(0 = 레벨순)으로 되돌린다. 덱 편집 팝업(PopupLevelLinkDeckManage)이 열릴 때 호출된다
    public void InitCurrentDeckSortType(){ CurrentDeckSortType.Value = 0; }

    /// <summary>
    /// 화면에 표시할 LevelLink ID 목록을 테이블 기준으로 다시 만든다.
    /// PageLevelLink.InitData()(페이지 시작 시)에서 호출된다.
    /// </summary>
    public void InitData()
    {
         LevelLinkUIList.Clear();
         // 테이블 LevelLink 목록(level_link_order 오름차순)에서 level_link_id만 뽑아 리스트로 만든다
         // [참고] 바로 위 Clear()한 리스트를 새 리스트로 교체하므로 Clear()는 실질적인 의미가 없다. 또한 테이블이 로드되지 않아 GetLevelLinkLists()가 null이면 예외가 난다
         LevelLinkUIList = CLevelLinkDataManager.Instance.GetLevelLinkLists().Select(d=>d.level_link_id).ToList();
    }

    /// <summary>
    /// 첫 번째 LevelLink(level_link_order가 가장 작은 것)을 현재 페이지로 설정한다.
    /// PageLevelLink.InitData()에서 InitData() 직후 호출되며, CurrentPageGDID setter를 통해 화면 갱신 이벤트가 발행된다.
    /// </summary>
    public void SetLevelLinkCurrentPage()
    {
        // 목록이 비어 있으면 FirstOrDefault()는 0을 반환한다
        var selectData = LevelLinkUIList.FirstOrDefault();
        CurrentPageGDID = selectData;
    }

    /// <summary>
    /// 현재 LevelLink의 다음 LevelLink ID를 반환한다. 마지막이면 처음으로 순환한다.
    /// PageLevelLink.OnClickNext()(다음 버튼)에서 호출된다. 값만 반환하고 CurrentPageGDID는 바꾸지 않는다.
    /// </summary>
    public int GetNextGDID()
    {
        // 현재 ID의 목록 내 위치. 목록에 없으면 -1이 되어 nextIndex = 0(첫 번째)이 된다
        int index = LevelLinkUIList.IndexOf(CurrentGDID);
        int nextIndex = index + 1;

        // 범위를 넘으면 0번(첫 번째)으로 순환
        if( LevelLinkUIList.Count <= nextIndex)
        {
            nextIndex = 0;
        }

        return LevelLinkUIList[nextIndex];
    }

    /// <summary>
    /// 현재 LevelLink의 이전 LevelLink ID를 반환한다. 첫 번째면 마지막으로 순환한다.
    /// 호출: PageLevelLink.OnClickPrev()(이전 버튼),
    ///       PageLevelLink.OnClickLock()(잠긴 LevelLink 클릭 시 "이전 LevelLink가 아직 잠겨 있으면 해금 불가" 검사)
    /// </summary>
    public int GetPrevGDID()
    {
        int index = LevelLinkUIList.IndexOf(CurrentGDID);
        int prevIndex = index - 1;

        // 0보다 작아지면 마지막 인덱스로 순환
        // [참고] 첫 번째 LevelLink에서 OnClickLock 검사를 하면 "마지막 LevelLink"을 이전 것으로 보게 된다 (순환 구조 때문)
        if( prevIndex < 0)
        {
            prevIndex = LevelLinkUIList.Count - 1;
        }

        return LevelLinkUIList[prevIndex];
    }
    
    /// <summary>
    /// 서버에서 받은 LevelLink 리스트를 SLevelLinkList에 반영한다 (gdid 기준으로 덮어쓰기 또는 추가).
    /// LevelLinkService의 거의 모든 응답 처리(Do)에서 호출된다: top / deck_save / reset_all / slot_add / slot_set / slot_unset_card / reduce_time.
    /// 응답에 일부 LevelLink만 들어와도 해당 gdid만 교체되고 나머지는 유지된다.
    /// </summary>
    public void SetLevelLinkListData(List<LevelLinkList> list)
    {
        if( list == null) return;
        
        list.ForEach(data =>
        {
            // 이미 있는 LevelLink이면 서버 객체로 통째로 교체, 없으면 새로 추가
            if (SLevelLinkList.ContainsKey(data.gdid))
            {
                SLevelLinkList[data.gdid] = data;
            }
            else
            {
                SLevelLinkList.Add(data.gdid, data);
            }
        });
    }

    /// <summary>
    /// 현재 LevelLink의 쿨타임이 끝났으면(remain_time <= 0) 해당 쿨타임 TimeStream을 제거한다.
    /// LevelLinkService.ReqLevelLinkReduceTime()의 응답 처리에서, 서버 데이터를 갱신한 직후 호출된다.
    /// (즉시 완료 등으로 쿨타임이 0이 되었을 때 화면 타이머가 계속 돌지 않도록 정리하는 용도)
    /// </summary>
    public void CheckTimeStream()
    {
        // [참고] ContainsKey 검사 없이 인덱서로 접근하므로 CurrentGDID가 SLevelLinkList에 없으면 KeyNotFoundException이 난다
        var levelLink = SLevelLinkList[CurrentGDID];
        
        if( levelLink.remain_time <= 0 )
        {
            // "LEVELLINK_SLOT_{gdid}" 키의 TimeStream을 Dispose 후 TimeStreamManager에서 제거
            string timerKey = string.Format(LEVELLINK_SLOT_TIME_KEY_FORMAT, CurrentGDID);
            TimeStreamManager.Instance.RemoveTimeStream(timerKey);
        }
    }

    /// <summary>
    /// TimeStream 키("LEVELLINK_SLOT_{gdid}")에서 gdid를 뽑아 해당 LevelLink의 남은 쿨타임을 로컬 데이터에 갱신한다.
    /// ObjLevelLinkSlot_Timer에서 쿨타임 타이머가 끝났을 때(IsEnd) remainTime = 0으로 호출된다.
    /// (서버를 다시 부르지 않고 로컬 데이터만 0으로 맞춰 GetStateLevelLink이 COOLTIME을 벗어나게 한다)
    /// </summary>
    public void UpdateLevelLinkListData_RemainTime_TimeKey(string timerKey, float remainTime)
    {
        // 접두사 "LEVELLINK_SLOT_"를 지우면 gdid 숫자 문자열만 남는다
        // [참고] 접두사 형식이 아닌 키(예: "LEVELLINK_SLOT_AD")가 들어오면 int.Parse에서 FormatException이 난다
        int gdid = int.Parse(timerKey.Replace(LEVELLINK_SLOT_TIME_KEY, ""));
        UpdateLevelLinkListData_RemainTime(gdid, remainTime);
    }

    /// <summary>
    /// 지정한 LevelLink의 남은 쿨타임(remain_time)을 로컬 데이터에서 직접 바꾼다 (초 단위, 소수점 버림).
    /// 해당 gdid가 없으면 아무것도 하지 않는다.
    /// </summary>
    public void UpdateLevelLinkListData_RemainTime(int gdid, float remainTime)
    {
        if( SLevelLinkList.ContainsKey(gdid))
        {
            SLevelLinkList[gdid].remain_time = (int)remainTime;
        }
    }

    /// <summary>
    /// LevelLink의 덱 슬롯 구성을 Dictionary로 반환한다.
    /// key: 슬롯 번호(level_link_group_order, 1~7), value: 그 슬롯에 등록된 카드 gdid (0이면 빈 슬롯)
    /// 테이블의 그룹 목록에 있는 슬롯 번호만 포함되므로, LevelLink마다 슬롯 개수가 다를 수 있다.
    /// PageLevelLink.OpenPopupLevelLinkDeckManage()에서 덱 편집 팝업 초기 슬롯 정보로 넘길 때 호출된다.
    /// </summary>
    public Dictionary<int, int> GetSlotGroupList(int levelLinkGDID)
    {
        LevelLinkList data = CLevelLinkDataManager.Instance.GetLevelLinkData(levelLinkGDID); //테이블 데이터
        LevelLinkList sData =  GetLevelLinkData(levelLinkGDID); //서버 데이터

        // LevelLink 테이블의 그룹 인덱스로 슬롯 구성(그룹 목록, level_link_group_order 오름차순)을 가져온다
        int groupID = data.level_link_group_index;
        var groupList = CLevelLinkDataManager.Instance.GetLevelLinkGroupLists(groupID);


        Dictionary<int, int> slotList = new Dictionary<int, int>();

        // 그룹 목록의 각 슬롯 번호(1~7)에 맞는 서버 카드 필드(card_1~card_7) 값을 넣는다
        // 서버 데이터가 슬롯마다 별도 필드라서 switch로 매핑한다 (1~7 범위 밖의 번호는 무시)
        for(int i = 0 ; i <groupList.Count; i++)
        {
            var groupData = groupList[i];
            switch( groupData.level_link_group_order)
            {
                case 1:
                    slotList.Add(groupData.level_link_group_order, sData.card_1);
                break;
                case 2:
                    slotList.Add(groupData.level_link_group_order, sData.card_2);
                break;
                case 3:
                    slotList.Add(groupData.level_link_group_order, sData.card_3);
                break;
                case 4:
                    slotList.Add(groupData.level_link_group_order, sData.card_4);
                break;
                case 5:
                    slotList.Add(groupData.level_link_group_order, sData.card_5);
                break;
                case 6:
                    slotList.Add(groupData.level_link_group_order, sData.card_6);
                break;
                case 7:
                    slotList.Add(groupData.level_link_group_order, sData.card_7);
                break;

            }
        }

        return slotList;
    }

    /// <summary>
    /// LevelLink gdid로 서버 데이터(LevelLinkList)를 찾아 반환한다. 없으면 null.
    /// 페이지/팝업 곳곳에서 현재 LevelLink의 lv, slot, remain_time, card_1~7 을 읽을 때 사용한다.
    /// </summary>
    public LevelLinkList GetLevelLinkData(int gdid)
    {
        if (SLevelLinkList.ContainsKey(gdid))
        {
            return SLevelLinkList[gdid];
        }

        return null;
    }

    /// <summary>
    /// 서버 데이터로 LevelLink의 현재 상태(LevelLink_Slot_State)를 계산한다.
    /// 판정 순서: 잠김(state == 0) → 쿨타임 중(remain_time > 0) → 카드 등록됨(slot > 0) → 비어 있음
    /// 호출: PageLevelLink(중앙/잠금 UI 데이터, 잠금 클릭 시 이전 LevelLink 검사), PageLevelLink_Bottom(하단 슬롯 상태 표시)
    /// </summary>
    public LevelLink_Slot_State GetStateLevelLink(int gdid)
    {
        // [참고] 서버 데이터가 없으면 data가 null이라 아래 data.state 접근에서 NullReferenceException이 난다
        var data = GetLevelLinkData(gdid);
        // state 0: 아직 해금(슬롯 추가)되지 않음
        if( data.state == 0)
        {
            return LevelLink_Slot_State.LOCK;
        }

        // 남은 쿨타임(초)이 있으면 쿨타임 상태 (카드 등록 해제 후 재등록 대기)
        if( data.remain_time > 0 )
        {
            return LevelLink_Slot_State.COOLTIME;
        }

        // slot: 중앙 LevelLink 슬롯에 등록된 카드 gdid (0이면 미등록)
        if( data.slot > 0 ) 
        {
            return LevelLink_Slot_State.SETTING;
        }

        return LevelLink_Slot_State.EMPTY;
    }

    /// <summary>
    /// 서버에서 받은 해금 미션 진행도 리스트를 SMissionList[LevelLink ID][미션 gdid]에 반영한다 (덮어쓰기 또는 추가).
    /// LevelLinkService.ReqLevelLinkInfo()(level_link/top 응답)에서 호출된다.
    /// </summary>
    public void SetLevelLinkMissionListData(List<MissionList> list)
    {
        // [참고] SetLevelLinkListData와 달리 null 검사가 없어서 응답의 mission_list가 null이면 예외가 난다
        list.ForEach(data=>
        {
            // 바깥 Dictionary: 이 미션이 속한 LevelLink ID가 처음이면 안쪽 Dictionary를 만든다
            if( SMissionList.ContainsKey(data.level_link_id ) == false)
            {
                SMissionList.Add(data.level_link_id, new Dictionary<int, MissionList>());
            }

            // 안쪽 Dictionary: 미션 gdid(퀘스트 ID) 기준으로 교체 또는 추가
            if (SMissionList[data.level_link_id].ContainsKey(data.gdid))
            {
                SMissionList[data.level_link_id][data.gdid] = data;
            }
            else
            {
                SMissionList[data.level_link_id].Add(data.gdid, data);
            }
        });
    }

    /// <summary>
    /// LevelLink ID + 미션 gdid(퀘스트 테이블의 quest_id)로 서버 미션 진행도(cur_point / end_point / state)를 찾는다. 없으면 null.
    /// 호출: PopupLevelLinkUnlock.CheckMissionClear()(해금 조건 검사), ObjLevelLinkUnlock_MissionListUI(미션 진행도 표시)
    /// </summary>
    public MissionList GetLevelLinkMissionListData(int levelLink_id, int gdid)
    {
        if( SMissionList.ContainsKey(levelLink_id))
        {
            if( SMissionList[levelLink_id].ContainsKey(gdid))
            {
                return SMissionList[levelLink_id][gdid];
            }
        }

        return null;
    }


    /// <summary>
    /// 해금 재료 하나(테이블 LevelLinkExtend)를 필요한 수량만큼 보유하고 있는지 검사한다.
    /// PopupLevelLinkUnlock.CheckMaterial()에서 재료마다 호출된다. 보유 수량 >= 필요 수량이면 true.
    /// </summary>
    public bool CheckItemCount(LevelLinkExtend extend)
    {
        // 재료 ID와 필요 수량
        int itemID = extend.level_link_extend_id;
        int targetCount = extend.level_link_extend_value;;

        // 테이블의 재료 타입(COMMON_CATEGORY)을 보상 타입(아이템/상품 등)으로 바꾼 뒤, 그 타입 기준으로 현재 보유 수량을 조회
        REWARD_TYPE rwType = extend.level_link_extend_type.ConvertToRewardType();
        int invenCount = rwType.GetRewardCount(itemID);
        return targetCount <= invenCount;
    }


    //LevelLink 배치 할 수 있는 카드 리스트
    //고마워요 코파일럿
    /// <summary>
    /// 중앙 LevelLink 슬롯에 등록할 수 있는 후보 카드 gdid 리스트를 만든다.
    /// PageLevelLink.OpenPopupLevelLinkSelectcard()에서 카드 선택 팝업(PopupLevelLinkSelectcard)에 넘길 때 호출된다.
    /// 제외 조건: 모든 LevelLink(현재 LevelLink 포함)의 덱 슬롯에 들어간 카드, LevelLink 레벨이 적용 중인 카드(level_link_lv != 0)
    /// 정렬: 등급 높은 순 → 레벨 낮은 순 → gdid 오름차순
    /// </summary>
    public List<int> GetLevelLinkCardIDList()
    {
        //인벤토리 카드 리스트
        var list = CardInventoryManager.Instance.GetCardList();

        //LevelLink 하단에 등록된 카드 리스트
        // (모든 LevelLink의 card_1~card_7 중 0보다 큰 값, 중복 제거)
        var levelLinkCardIDList = GetAreadyLevelLinkCardList();
            
        //LevelLink 등록된 카드 ID 제외
        // [참고] levelLinkCardIDList가 List라서 Contains가 매번 선형 탐색이다 (카드 수 x 등록 카드 수). 카드가 많으면 HashSet이 유리하다
        var filteredList = list.Where(item => !levelLinkCardIDList.Contains(item.gdid))
            .Where(card => card.level_link_lv == 0 ) //-1이면 LevelLink가 적용되었으나, 오리지널 카드 레벨이 더 큰 경우임
            .OrderByDescending(item => item.grade) //등급 높은순
            .ThenBy(item => item.lv)    //레벨
            .ThenBy(item => item.gdid) //id
            .Select(item=>item.gdid)
            .ToList();

        return filteredList;
    }

    /// <summary>
    /// LevelLink 덱 편집의 카드 리스트
    /// </summary>
    /// <remarks>
    /// 덱 편집 팝업(ObjLevelLinkDeck_MidUI)의 카드 스크롤에 표시할 카드 gdid 리스트를 만든다.
    /// 호출: ObjLevelLinkDeck_MidUI 데이터 설정 시, 정렬/필터 변경 후 다시 그릴 때
    /// 처리 순서: 인벤토리 전체 → 다른 LevelLink 덱 카드 제외 → level_link_lv != 0 제외 → 멤버 필터 → 속성 필터 → 선택된 정렬 전략 적용
    /// 현재 LevelLink의 덱 카드는 제외하지 않으므로 리스트에 함께 표시된다.
    /// </remarks>
    /// <returns></returns>
    public List<int> GetLevelLinkCardDeckList()
    {
        //인벤토리 카드 리스트
        var list = CardInventoryManager.Instance.GetCardList();

        //LevelLink 하단에 등록된 카드 리스트
         //다른 LevelLink에서 적용된것도 포함시키려면 이 부분을 삭제 하면됨
        // (현재 LevelLink를 뺀 나머지 LevelLink들의 덱 카드 gdid 목록)
        var levelLinkCardIDList = GetAreadyLevelLinkCardList_IgnoreCurrentSlot();

        //LevelLink 등록된 카드 ID 제외
        // 두 번째 Where: LevelLink 레벨이 적용 중인 카드(level_link_lv가 0이 아닌 카드)는 덱 후보에서 뺀다
        var filteredList = list.Where(item => !levelLinkCardIDList.Contains(item.gdid))
            .Where(card => card.level_link_lv == 0 ); //-1이면 LevelLink가 적용되었으나, 오리지널 카드 레벨이 더 큰 경우임

        // 멤버 필터 → 속성(PZTYPE) 필터 순서로 거른다 (둘 다 덱 편집 팝업의 필터 UI에서 선택한 값 기준)
        var sortList_member = GetFilterSortMemberList(filteredList.ToList());
        var sortList = GetFilterSortPZTypeList(sortList_member);

        // 드롭다운에서 선택한 정렬 전략(LevelLinkDeckSort.List[인덱스])으로 정렬 후 gdid만 뽑는다
        // 각 전략은 1차 기준으로 정렬한 뒤 card_sort 내림차순으로 보조 정렬한다 (CardSortStrategyBase)
        return LevelLinkDeckSort.Get(CurrentDeckSortTypeValue)
            .Sort(sortList)
            .Select(item => item.gdid)
            .ToList();
    }


    /// <summary>
    /// LevelLink 덱 자동 배치
    /// Tuple<배치 가능 여부, Dictionary<슬롯 인덱스, 카드 GDID>>
    /// </summary>
    /// <remarks>
    /// 현재 LevelLink의 덱 슬롯을 자동으로 채운 결과를 계산한다. (서버 요청은 하지 않고 결과만 반환)
    /// 호출:
    ///  - PageLevelLink.OpenPopupLevelLinkDeckAutoSave(): 결과가 true이고 기존 덱과 다르면 바로 덱 저장 API 호출
    ///  - PopupLevelLinkDeckManage.OnClickEventAuto(): 결과가 true이면 팝업의 슬롯 정보만 교체(저장은 사용자가 확인 버튼으로)
    /// 배치 규칙:
    ///  1) 특정 멤버 조건이 걸린 슬롯을 먼저 채운다 → 그 멤버 카드 중 레벨 → 전투력 → 등급 높은 순(동률이면 gdid 작은 순) 1장
    ///  2) 조건 없는(그룹 전체) 슬롯은 남은 카드 중 같은 기준으로 위에서부터 슬롯 수만큼 채운다
    /// 반환 Item1이 false인 경우: 후보 카드 수가 슬롯 수보다 적거나, 멤버 조건 슬롯에 넣을 카드가 없는 경우
    /// (false일 때 Item2에는 그때까지 채운 일부 결과가 들어 있을 수 있지만 호출부는 사용하지 않는다)
    /// </remarks>
    /// <returns></returns>
    public Tuple<bool, Dictionary<int,int>> GetLevelLinkCardDeckAutoList()
    {
        //차곡 차곡 넣어줄 예정
        // key: 슬롯 번호(level_link_group_order), value: 배치할 카드 gdid
        Dictionary<int, int> slotInfo = new Dictionary<int, int>();

        //인벤토리 카드 리스트
        var list = CardInventoryManager.Instance.GetCardList();

        //현재 LevelLink 덱에 등록된 카드 제외 한,
        //LevelLink 하단에 등록된 카드 리스트
        var levelLinkCardIDList = GetAreadyLevelLinkCardList_IgnoreCurrentSlot();
        // 현재 LevelLink의 테이블 데이터 → 그룹 인덱스로 슬롯 구성(슬롯 번호 + 멤버 조건) 목록을 가져온다
        var levelLinkData = CLevelLinkDataManager.Instance.GetLevelLinkData(CurrentGDID);
        var slotList = CLevelLinkDataManager.Instance.GetLevelLinkGroupLists(levelLinkData.level_link_group_index);

        //LevelLink에 등록된 카드 제외
        // (다른 LevelLink 덱 카드 제외 + LevelLink 레벨 적용 중인 카드 제외. 현재 LevelLink 덱 카드는 다시 후보가 된다)
        list = list.Where(item => !levelLinkCardIDList.Contains(item.gdid))
                .Where(card => card.level_link_lv == 0 )
                .ToList();

        //카드 갯수 확인
        // 후보 카드 수가 전체 슬롯 수보다 적으면 덱을 다 채울 수 없으므로 실패
        if( list.Count < slotList.Count)
        {
            return new Tuple<bool, Dictionary<int, int>>(false, slotInfo);
        }

        //특정 멤버 슬롯에 배치 해야 하는지 확인
        // level_link_group_con이 그룹 전체 값이 아니면 "특정 멤버 전용 슬롯", 그룹 전체 값이면 "아무 멤버나 가능한 일반 슬롯"
        var memberTypeList = slotList.Where(d=>d.level_link_group_con != MEMBER_TYPE.BTS).ToList();
        var normalTypeList = slotList.Where(d=>d.level_link_group_con == MEMBER_TYPE.BTS).ToList();

        // 슬롯 개수 집계 (totalSlotCount, memberTypeSlot는 아래에서 사용되지 않는다. normalSlot만 Take()에 사용)
        int totalSlotCount = slotList.Count;
        int memberTypeSlot = memberTypeList.Count;
        int normalSlot = normalTypeList.Count;
        // 멤버 전용 슬롯 중 하나라도 넣을 카드를 못 찾으면 true
        bool notEnoughCard = false;

        // (선언만 되어 있고 사용되지 않는 리스트)
        List<int> MemberCardList = new List<int>();
        
        // 1단계: 멤버 전용 슬롯 채우기
        if( memberTypeList.Count > 0 )
        {
            for(int i= 0 ; i < memberTypeList.Count; i++)
            {
                // 이 슬롯이 요구하는 멤버의 카드만 골라 레벨 → 전투력 → 등급 내림차순, gdid 오름차순으로 정렬
                // [참고] 이미 앞의 멤버 슬롯에 배치한 카드를 후보에서 빼지 않으므로, 같은 멤버를 요구하는 슬롯이 두 개 이상이면 같은 카드가 중복 배치될 수 있다
                var sortList = list.Where(d => d.GetCardData().member == memberTypeList[i].level_link_group_con)
                            .OrderByDescending(item => item.lv)    //레벨
                            .ThenByDescending(item => item.GetBattlePower()) //전투력 높은 순
                            .ThenByDescending(item=> item.grade) //등급
                            .ThenBy(item => item.gdid); //id

                // 정렬 결과의 첫 번째(가장 좋은) 카드. 해당 멤버 카드가 없으면 null
                var card = sortList.FirstOrDefault();

                // 이 슬롯의 번호(1~7)
                int slotIndex = memberTypeList[i].level_link_group_order;

                if( card != null )
                {
                    // 같은 슬롯 번호가 두 번 나올 일은 없으므로(테이블상 order 중복 없음 전제) else는 방어 로그
                    if( slotInfo.ContainsKey(slotIndex) == false)
                    {
                        slotInfo.Add(slotIndex, card.gdid);
                    }
                    else
                    {
                        CDebug.Log("들어오면 안되는 예외 상황이 생겼다");
                    }
                }
                else
                {
                    // 실패 표시만 하고 루프는 계속 돈다 (루프 종료 후 한 번에 실패 반환)
                    notEnoughCard = true; //카드가 충분하지 않다
                }
            }
        }

        if( notEnoughCard ) // 남은 슬롯에 등록 할 수 있는 카드가 없다
        {
            return new Tuple<bool, Dictionary<int, int>>(false, slotInfo);
        }

        //현재 LevelLink
        // 2단계: 일반 슬롯 채우기
        // 후보(list)에서 1단계에 배치한 카드를 빼고, 같은 기준(레벨 → 전투력 → 등급 내림차순, gdid 오름차순)으로 정렬해 일반 슬롯 수만큼만 가져온다
        // (첫 번째 Where의 levelLinkCardIDList 제외는 list를 만들 때 이미 적용되어 있어 중복 조건이다)
        var filteredList = list.Where(item => !levelLinkCardIDList.Contains(item.gdid))
                            .Where(item=> slotInfo.Values.Contains(item.gdid) == false) //멤버에서 이미 배치된 카드 제외
                            .OrderByDescending(item => item.lv)    //레벨
                            .ThenByDescending(item => item.GetBattlePower()) //전투력 높은 순
                            .ThenByDescending(item=>item.grade) //등급
                            .ThenBy(item => item.gdid) //id
                            .Select(item=>item.gdid)
                            .Take(normalSlot)
                            .ToList();

        // 정렬된 카드를 일반 슬롯 목록 순서(level_link_group_order 오름차순)대로 하나씩 대입
        // 즉, 가장 좋은 카드가 번호가 가장 작은 일반 슬롯에 들어간다
        for (int i = 0; i < filteredList.Count; i++)
        {
            int slotIndex = normalTypeList[i].level_link_group_order;

            if (!slotInfo.ContainsKey(slotIndex))
            {
                slotInfo.Add(slotIndex, filteredList[i]);
            }
            else
            {
                CDebug.Log($"들어오면 안되는 예외 상황이 생겼다 {slotIndex} - {slotInfo[slotIndex]}");
            }
        }


        // 앞의 카드 수 검사(list.Count >= slotList.Count) 덕분에 일반 슬롯 수만큼의 카드는 항상 남아 있으므로 여기까지 오면 성공
        return new Tuple<bool, Dictionary<int, int>>(true, slotInfo);
    }

    /// <summary>
    /// 현재 LevelLink(CurrentGDID)을 제외한 나머지 모든 LevelLink의 덱 슬롯(card_1~card_7)에 등록된 카드 gdid 목록 (0 제외, 중복 제거).
    /// 덱 편집 리스트(GetLevelLinkCardDeckList)와 자동 배치(GetLevelLinkCardDeckAutoList)에서
    /// "다른 LevelLink 덱에 이미 쓰인 카드"를 후보에서 빼는 데 사용한다.
    /// 중앙 LevelLink 슬롯 카드(slot)는 포함하지 않는다 (그 카드는 호출부의 level_link_lv 조건으로 걸러진다).
    /// </summary>
    private List<int> GetAreadyLevelLinkCardList_IgnoreCurrentSlot()
    {
        // 1) 현재 LevelLink 제외 → 2) LevelLink마다 card_1~7 을 리스트로 펼침(SelectMany) → 3) 빈 슬롯(0) 제외 → 4) 중복 제거
        var levelLinkCardIDList = SLevelLinkList.Values
            .Where(h => h.gdid != CurrentGDID )
            .SelectMany(h => new List<int> { h.card_1, h.card_2, h.card_3, h.card_4, h.card_5, h.card_6, h.card_7})
            .Where(cardID => cardID > 0)
            .Distinct()
            .ToList();

            return levelLinkCardIDList;

    }

    /// <summary>
    /// 모든 LevelLink(현재 LevelLink 포함)의 덱 슬롯(card_1~card_7)에 등록된 카드 gdid 목록 (0 제외, 중복 제거).
    /// 중앙 슬롯 후보 리스트(GetLevelLinkCardIDList)에서 덱 카드를 빼는 데 사용한다.
    /// </summary>
    private List<int> GetAreadyLevelLinkCardList()
    {
        // LevelLink마다 card_1~7 을 펼쳐서(SelectMany) 하나의 시퀀스로 만든 뒤 0 제외, 중복 제거
        var levelLinkCardIDList = SLevelLinkList.Values
            .SelectMany(h => new List<int> { h.card_1, h.card_2, h.card_3, h.card_4, h.card_5, h.card_6, h.card_7})
            .Where(cardID => cardID > 0)
            .Distinct()
            .ToList();

            return levelLinkCardIDList;
    }

    /// <summary>
    /// UI에서 편집 중인 덱 슬롯 정보가 현재 LevelLink의 서버 덱과 다른지 검사한다.
    /// uiSlotInfo — key: 슬롯 번호(1~7), value: 카드 gdid
    /// 호출:
    ///  - PageLevelLink.OpenPopupLevelLinkDeckAutoSave(): 자동 배치 결과가 기존과 같으면 저장 API를 생략
    ///  - PopupLevelLinkDeckManage(Setting.IsChangeSlotInfo): 닫기/카드 관리 이동 시 "저장하지 않은 변경 사항" 확인 팝업 여부
    /// uiSlotInfo에 들어 있는 슬롯 번호만 비교하며, 하나라도 다르면 즉시 true.
    /// </summary>
    public bool IsChangeSlotInfo(Dictionary<int, int> uiSlotInfo)
    {
        bool isChanged = false;

        var list = uiSlotInfo.Keys.ToList();
        LevelLinkList sData = GetLevelLinkData(CurrentGDID); //서버 데이터

        for(int i = 0 ; i < list.Count; i++)
        {
            int key = list[i];

            // 슬롯 번호에 맞는 서버 필드(card_1~card_7)와 UI 값을 비교
            switch(key)
            {
                case 1: if(uiSlotInfo[key] != sData.card_1) isChanged = true; break;
                case 2: if(uiSlotInfo[key] != sData.card_2) isChanged = true; break;
                case 3: if(uiSlotInfo[key] != sData.card_3) isChanged = true; break;
                case 4: if(uiSlotInfo[key] != sData.card_4) isChanged = true; break;
                case 5: if(uiSlotInfo[key] != sData.card_5) isChanged = true; break;
                case 6: if(uiSlotInfo[key] != sData.card_6) isChanged = true; break;
                case 7: if(uiSlotInfo[key] != sData.card_7) isChanged = true; break;
            }

            if( isChanged ) break;
        }

        return isChanged;        

    }

    /// <summary>
    /// 덱편집시 카드 인벤토리를 다녀와서 레벨이 변경되어 LevelLink 레벨이 변경되었을 경우
    /// </summary>
    /// <remarks>
    /// UI 덱의 LevelLink 레벨(덱 카드 중 최소 레벨)이 서버의 LevelLink 레벨(sData.lv)과 다른지 검사한다.
    /// PopupLevelLinkDeckManage.Setting.IsChangeLevelLinkLV()로 감싸져 있지만, 현재 OnClickEventOK에서는 호출이 주석 처리되어 있다.
    /// </remarks>
    /// <param name="uiSlotInfo"></param>
    /// <returns></returns>
    public bool IsChangeLevelLinkLV(Dictionary<int, int> uiSlotInfo)
    {
        bool isChanged = false;

        LevelLinkList sData = GetLevelLinkData(CurrentGDID); //서버 데이터

        // UI 덱 기준 LevelLink 레벨 (= 등록된 카드들의 최소 레벨)
        int cardLv = GetLevelLinkSlotLV(uiSlotInfo);

        if( sData.lv != cardLv)
        {
            isChanged = true;
        }

        return isChanged;        
    }

    /// <summary>
    /// UI 덱으로 저장하면 LevelLink 레벨이 지금(서버 sData.lv)보다 낮아지는지 검사한다.
    /// PopupLevelLinkDeckManage.OnClickEventOK()(덱 저장 확인)에서 호출되며, true면 "LevelLink 레벨 하락" 확인 팝업(PopupLevelLinkDecksave)을 먼저 띄운다.
    /// </summary>
    public bool IsChangeLevelLinkLowLV(Dictionary<int, int> uiSlotInfo )
    {
        bool isChanged = false;

        LevelLinkList sData = GetLevelLinkData(CurrentGDID); //서버 데이터

        int cardLv = GetLevelLinkSlotLV(uiSlotInfo);

        // 기존 LevelLink 레벨 > 새 덱의 최소 레벨 → 레벨 하락
        if( sData.lv > cardLv)
        {
            isChanged = true;
        }

        return isChanged;        
    }
    

    /// <summary>
    /// 덱 슬롯에 등록된 카드들 중 가장 낮은 카드 레벨을 반환한다 (= 이 덱으로 저장했을 때의 LevelLink 레벨).
    /// 호출: IsChangeLevelLinkLV / IsChangeLevelLinkLowLV, PopupLevelLinkDeckManage.Setting.GetSlotLevel()(레벨 하락 팝업의 변경 후 레벨)
    /// </summary>
    public int GetLevelLinkSlotLV(Dictionary<int, int> uiSlotInfo)
    {
        int lv = 0;

        // 슬롯 값(카드 gdid)만 뽑음 → 빈 슬롯(0) 제외 → 인벤토리에서 카드 정보를 찾아 레벨로 변환 → 최소값
        // [참고] 모든 슬롯이 비어 있으면 빈 시퀀스에 Min()을 호출하게 되어 InvalidOperationException이 난다. 또 인벤토리에 없는 gdid면 GetCardInfoByGDID 결과가 null이라 .lv에서 예외가 날 수 있다
        lv = uiSlotInfo.Select(slot => slot.Value)
        .Where(cardID => cardID > 0 )
        .Select(cardID => CardInventoryManager.Instance.GetCardInfoByGDID(cardID).lv).Min();

        return lv;


    }


    /// <summary>
    /// LevelLink 페이지를 보여준다
    /// </summary>
    /// <remarks>
    /// 로비의 LevelLink 버튼(LobbyButtonManager)에서 호출된다.
    /// 흐름: 메뉴 전환 로딩 → 로비 무비 스크롤 해제 → 카드 정보 API → LevelLink 정보 API(level_link/top) → PageLevelLink 로드
    /// </remarks>
    public void ShowLevelLinkPage()
    {
        // MenuChange: 화면 전환 로딩 연출 중에 콜백을 실행한다
        LoadingLayer.Instance.MenuChange(() =>
        {
            // 로비 화면의 무비 클립 스크롤이 있으면 리소스를 해제한다
            if(BarManager.Instance!= null && BarManager.Instance.lobbyObj != null)
            {
                if (BarManager.Instance.lobbyObj.moviescroll != null)
                    BarManager.Instance.lobbyObj.moviescroll.MovieClipRelease();
            }

            //카드 정보 이거..로비에서 한번만 부르드록 하던가 해야 할듯
            // 1) 카드 정보 API → 2) SelectMany로 응답이 오면 이어서 LevelLink 정보 API 호출
            //    (ReqLevelLinkInfo 내부 Do에서 SLevelLinkList / SMissionList가 갱신된다)
            // 3) 두 API가 모두 성공하면 PageLevelLink 프리팹을 페이지 팝업으로 로드
            // 서버 에러(e != 0)는 공통 Post 처리에서 에러 팝업을 띄우고 응답을 걸러내므로, 그 경우 페이지는 열리지 않는다
            // 반환된 구독(IDisposable)은 보관하지 않는다 (API 스트림은 응답을 한 번 발행하고 끝나므로 구독도 그때 끝난다)
            APIHelper.CardService.CardsInfo()
            .SelectMany(_=> APIHelper.LevelLinkService.ReqLevelLinkInfo())
            .Subscribe(res =>
            {
                CResourceManager.Instance.LoadPagePopup<PageLevelLink>(CPREFAB_KEY.page_level_link);
            });

        }, null, false);
    }


    #region  server API request
    // 이 영역의 void 요청 메서드들은 공통 패턴을 따른다:
    //  - SingleAssignmentDisposable에 구독을 담아 두고, 응답 처리 후 스스로 Dispose (1회성 요청)
    //  - 서버 데이터 갱신(SLevelLinkList 등)과 게임 로그는 LevelLinkService의 Do()에서 이미 처리된 상태로 Subscribe에 도착한다
    //  - 서버 에러(e != 0)는 공통 Post 처리에서 에러 팝업 후 걸러지므로 Subscribe 본문은 성공 응답일 때만 실행된다
    //  - 대상 LevelLink는 모두 현재 페이지의 LevelLink(CurrentGDID)
    /// <summary>
    /// 현재 LevelLink 해금(슬롯 추가) API(level_link/slot_add)를 호출한다.
    /// PageLevelLink.OpenPopupLevelLinkUnlock()에서 해금 팝업이 성공으로 닫혔을 때 호출된다.
    /// 성공하면 LevelLinkUnLockSlot 메시지를 발행해 PageLevelLink이 해금 연출을 재생하게 한다.
    /// </summary>
    public void RequestSlotAdd()
    {
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = APIHelper.LevelLinkService.ReqLevelLinkSlotAdd(CurrentGDID)
        .Subscribe(res=>
        {
            MessageBroker.Default.Publish(new LevelLinkUnLockSlot(CurrentGDID));
            disposable.Dispose();
        });
    }

    /// <summary>
    /// 현재 LevelLink 해금에 부족한 재료를 구매하는 API(level_link/slot_buy)를 호출하고, 성공 여부(bool)를 발행하는 Observable을 반환한다.
    /// PopupLevelLinkUnlock.OpenShopItemPopup()에서 보석 사용 확인(IsOK) 후 체인으로 이어서 호출된다.
    /// 구독해야 요청이 나간다 (Cold Observable). 아이템/상품 인벤토리 갱신은 LevelLinkService의 Do()에서 처리된다.
    /// </summary>
    public IObservable<bool> RequestItemBuy()
    {
        return APIHelper.LevelLinkService.RequestLevelLinkSlotBuy(CurrentGDID)
        // 응답 객체를 성공 여부(bool)로 변환. 에러 응답은 보통 공통 처리에서 걸러지므로 실질적으로 true만 발행된다
        .Select(res=>
        {
            return res.e == 0;
        });
    }

    /// <summary>
    /// 현재 LevelLink의 덱을 저장하는 API(level_link/deck_save)를 호출한다.
    /// slotList — key: 슬롯 번호(1~7), value: 카드 gdid (없는 슬롯 번호는 서비스에서 0으로 보낸다)
    /// 호출: PageLevelLink.OpenPopupLevelLinkDeckManage()(덱 편집 팝업 확인), OpenPopupLevelLinkDeckAutoSave()(자동 배치 후 바로 저장)
    /// 성공 시 "LevelLink 덱을 저장했습니다" 토스트 + LevelLinkSelectGDID 발행(페이지 전체 갱신).
    /// </summary>
    public void RequestLevelLinkDeckSave(Dictionary<int, int> slotList)
    {
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = APIHelper.LevelLinkService.ReqLevelLinkDeckSave(CurrentGDID, slotList)
        .Subscribe(res=>
        {
            //LevelLink 덱을 저장했습니다.
            CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("levellink_toast_01");

            // 갱신된 서버 데이터로 페이지를 다시 그리도록 알린다 (같은 LevelLink이므로 IsChangeGDID = false)
            MessageBroker.Default.Publish(new LevelLinkSelectGDID(CurrentGDID));

            disposable.Dispose();
        });
    }

    /// <summary>
    /// 현재 LevelLink의 덱 카드를 모두 해제(초기화)하는 API(level_link/reset_all)를 호출한다.
    /// PageLevelLink.OpenPopupResetInfo()에서 초기화 확인 팝업의 확인 버튼을 눌렀을 때 호출된다.
    /// 성공 시 토스트 + LevelLinkSelectGDID 발행.
    /// </summary>
    public void RequestLevelLinkReset()
    {
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = APIHelper.LevelLinkService.ReqLevelLinkResetAll(CurrentGDID)
        .Subscribe(res=>
        {
            //LevelLink 덱을 저장했습니다.
            // (초기화인데도 덱 저장과 같은 문자열 키 "levellink_toast_01"을 사용한다)
            CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("levellink_toast_01");

            MessageBroker.Default.Publish(new LevelLinkSelectGDID(CurrentGDID));

            disposable.Dispose();

        });
    }

    /// <summary>
    /// 현재 LevelLink의 중앙 LevelLink 슬롯에 카드를 등록하는 API(level_link/slot_set)를 호출한다.
    /// PageLevelLink.OpenPopupLevelLinkSelectcard()에서 카드 선택 팝업이 성공으로 닫혔을 때, 선택한 카드 gdid로 호출된다.
    /// 성공 시 LevelLinkSelectGDID 발행 (카드 정보 갱신은 LevelLinkService의 Do()에서 처리).
    /// </summary>
    public void RequestLevelLinkCardSave(int cardID)
    {
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = APIHelper.LevelLinkService.ReqLevelLinkSlotSet(CurrentGDID, cardID)
        .Subscribe(res=>
        {
            MessageBroker.Default.Publish(new LevelLinkSelectGDID(CurrentGDID));

            disposable.Dispose();
        });
    }

    /// <summary>
    /// 현재 LevelLink의 중앙 LevelLink 슬롯 카드를 등록 해제하는 API(level_link/slot_unset_card)를 호출한다.
    /// PageLevelLink.OpenPopupLevelLinkCardRemove()에서 등록 해제 확인 팝업이 성공으로 닫혔을 때 호출된다.
    /// (등록 해제 확인 팝업에는 테이블의 쿨타임 level_link_cooldown이 함께 표시되며, 이후 쿨타임 상태는 응답 level_link_list의 remain_time으로 판단한다)
    /// 성공 시 LevelLinkSelectGDID 발행.
    /// </summary>
    public void RequestLevelLinkCardRemove()
    {
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = APIHelper.LevelLinkService.ReqLevelLinkSlotUnsetCard(CurrentGDID)
        .Subscribe(res=>
        {
            MessageBroker.Default.Publish(new LevelLinkSelectGDID(CurrentGDID));

            disposable.Dispose();
        });

    }

    /// <summary>
    /// LevelLink 쿨타임 단축 API(level_link/reduce_time)를 호출하고 성공 여부(bool)를 발행하는 Observable을 반환한다.
    /// PopupLevelLinkInstantcomplete.OnClickEventScroll()에서 호출된다 (보석 사용은 OpenPopupUseCash 확인 후 체인으로 이어서 호출).
    /// itemID — 1: 보석 즉시 완료, 티켓 gdid: 시간 단축 티켓, 0: 사용 안 함 / adID — 광고 단축일 때 AD_TYPE_LEVELLINK_SLOT 값, 아니면 0
    /// </summary>
    public IObservable<bool> ReqLevelLinkReduceTime(int gdid, int itemID, int adID)
    {
        return APIHelper.LevelLinkService.ReqLevelLinkReduceTime(gdid, itemID, adID)
        // 응답을 bool 하나짜리 Observable로 바꿔 이어 붙인다 (Select로도 충분한 형태)
        .SelectMany(res=>
        {
            if( res.e == 0 )
            {
                return Observable.Return(true);
                // result.IsSuccess = true;
                // Close();
            }

            return Observable.Return(false);

        });  

    }

    #endregion

    #region  sort data
    // 덱 편집 팝업(PopupLevelLinkDeckManage)의 멤버 / 속성 필터 상태를 다루는 영역.
    // 각 필터 객체는 "적용된 선택(OriginalSelectSortList)"과 "필터 UI에서 편집 중인 선택(SelectSortList)"을 따로 들고 있고,
    // 필터 UI의 확인/취소에 따라 SaveSortInfo()가 둘 중 하나로 맞춘다.
    /// <summary>
    /// 멤버 / 속성 필터를 초기 상태(전부 선택)로 만들고 필터 UI 항목 목록을 다시 만든다.
    /// PopupLevelLinkDeckManage가 열릴 때 호출된다.
    /// </summary>
    public void InitSortFilterData()
    {
        SortFilterMember.InitData();
        SortFilterPZTYPE.InitData();
    }

    /// <summary>
    /// 멤버 / 속성 필터의 선택 목록과 UI 항목 목록을 모두 비운다.
    /// PopupLevelLinkDeckManage.OnClose()(덱 편집 팝업 닫힘)와 매니저 Dispose()에서 호출된다.
    /// </summary>
    public void DisposeSortFilter()
    {
        SortFilterMember.Dispose();
        SortFilterPZTYPE.Dispose();
    }

    /// <summary>
    /// 필터 UI의 "전체 선택" 버튼: 멤버 / 속성 필터 항목을 모두 선택 상태로 만든다 (편집 중 선택만 변경).
    /// ObjLevelLinkDeck_MidUI_SortFilter.OnClickEventAll()에서 호출된다.
    /// </summary>
    public void UpdateSortUIData_SelectAll()
    {
        SortFilterMember.SelectAllUIData();
        SortFilterPZTYPE.SelectAllUIData();
    }
    /// <summary>
    /// 필터 UI의 "필터 해제(선택 없음)" 버튼: 멤버 / 속성 필터 항목을 모두 해제 상태로 만든다 (편집 중 선택만 변경).
    /// ObjLevelLinkDeck_MidUI_SortFilter.OnClickEventNoFilter()에서 호출된다.
    /// </summary>
    public void UpdateSortUIData_RemoveAll()
    {
        SortFilterMember.RemoeAllUIdata();
        SortFilterPZTYPE.RemoeAllUIdata();
    }

    /// <summary>
    /// 멤버 필터 항목 하나를 눌렀을 때 선택 상태를 토글한다.
    /// i — 멤버 필터 UI 인덱스 (0: "전체" 항목, 1~: 각 멤버)
    /// ObjLevelLinkDeck_MidUI_SortFilter의 멤버 항목 클릭에서 호출된다.
    /// </summary>
    public void UpdateMemberSortUIData(int i)
    {
        // 0번 "전체" 항목: 지금 전부 선택되어 있으면 모두 해제, 아니면 모두 선택
        if( i == 0 )
        {
            if( SortFilterMember.IsAll())
            {
                SortFilterMember.RemoeAllUIdata();
            }
            else
            {
                SortFilterMember.SelectAllUIData();
            }
        }
        else
        {
            // 개별 멤버 토글 후, 전부 선택되었는지에 맞춰 "전체" 항목의 체크 표시를 갱신
            SortFilterMember.UpdateUIData(i);
            SortFilterMember.UpdateUIDataAll();
        }
    }

    /// <summary>
    /// 속성(PZTYPE) 필터 항목 하나를 눌렀을 때 선택 상태를 토글한다.
    /// i — PZTYPE 값 (PZTYPE_NONE(=1)이 "전체" 항목, 2~8이 각 속성)
    /// ObjLevelLinkDeck_MidUI_SortFilter의 속성 항목 클릭에서 호출된다.
    /// </summary>
    public void UpdatePZTypeSortUIData(int i)
    {
        // "전체" 항목: 지금 전부 선택되어 있으면 모두 해제, 아니면 모두 선택
        if( i == (int)PZTYPE.PZTYPE_NONE )
        {
            if( SortFilterPZTYPE.IsAll())
            {
                SortFilterPZTYPE.RemoeAllUIdata();
            }
            else
            {
                SortFilterPZTYPE.SelectAllUIData();
            }
        }
        else
        {
            // 개별 속성 토글 후 "전체" 항목 체크 표시 갱신
            SortFilterPZTYPE.UpdateUIData(i);
            SortFilterPZTYPE.UpdateUIDataAll();
        }
    }

    /// <summary>
    /// 필터 UI를 닫을 때 편집한 선택을 확정하거나 되돌린다.
    /// isChange — true: 확인(편집 중 선택을 적용 선택으로 저장), false: 취소(적용 선택으로 되돌리고 UI 체크 표시 복구)
    /// ObjLevelLinkDeck_MidUI.OnClickEventSortFilter()에서 호출된다.
    /// </summary>
    public void SaveSortInfo(bool isChange)
    {
        if( isChange )
        {
            // 편집 중 선택(SelectSortList)을 복사해 적용 선택(OriginalSelectSortList)으로 확정
            SortFilterMember.SetOriginalSelectSortList();
            SortFilterPZTYPE.SetOriginalSelectSortList();
        }
        else
        {
            // 적용 선택을 복사해 편집 중 선택을 되돌린다
            SortFilterMember.ReverseSelectSortList();
            SortFilterPZTYPE.ReverseSelectSortList();
            
            // 되돌린 선택에 맞춰 각 항목의 체크 표시(IsSelect)를 다시 맞추고, "전체" 항목 표시도 갱신
            SortFilterMember.SetUIDataAll();
            SortFilterMember.UpdateUIDataAll();

            // [참고] 멤버 필터와 달리 SetUIDataAll()이 두 번 호출되고 UpdateUIDataAll()은 호출되지 않는다 (멤버 쪽 패턴대로라면 두 번째 줄은 UpdateUIDataAll()로 보인다)
            //        그래서 속성 필터의 "전체" 항목 체크 표시는 IsAll() 결과가 아니라 선택 목록에 PZTYPE_NONE이 들어 있는지로만 정해진다
            // [참고] SortFilter_PZTYPE.SetUIDataAll()은 i < FilterCount 까지만 돌아서 마지막 속성(PZTYPE_EFFORT)의 체크 표시도 갱신되지 않는다
            SortFilterPZTYPE.SetUIDataAll();
            SortFilterPZTYPE.SetUIDataAll();

        }
    }

    /// <summary>
    /// 현재 편집 중인 멤버 / 속성 필터를 적용했을 때 남는 카드가 한 장이라도 있는지 검사한다.
    /// ObjLevelLinkDeck_MidUI_SortFilter.UpdateUIState()에서 필터 확인 버튼의 활성/비활성 결정에 사용된다.
    /// </summary>
    public bool CheckSortFilter()
    {
        // [참고] 덱 리스트(GetLevelLinkCardDeckList)와 달리 다른 LevelLink 덱 카드 / level_link_lv 제외 조건 없이 인벤토리 전체로 검사하므로, true여도 실제 덱 리스트는 비어 있을 수 있다
        var targetList = CardInventoryManager.Instance.GetCardList();
        var list = GetFilterSortMemberList(targetList);
        list = GetFilterSortPZTypeList(list);
        
        if( list.Count > 0 )
        {
            return true;
        }

        return false;
    }
    /// <summary>
    /// 멤버 필터 적용. 멤버가 전부 선택되어 있으면 그대로 반환하고, 아니면 선택된 멤버의 카드만 남긴다.
    /// (선택 목록은 편집 중 선택 SelectSortList 기준)
    /// </summary>
    private List<CardList> GetFilterSortMemberList(List<CardList> targetList)
    {
        var memberList = SortFilterMember.GetSelectSortList;

        if( SortFilterMember.IsAll())
        {
            return targetList;
        }
        else
        {
            // 카드 테이블 데이터의 member 값이 선택 목록(HashSet)에 있는 카드만 남긴다
            // [참고] 속성 필터와 달리 "아무것도 선택 안 함"을 전체로 취급하지 않아, 멤버를 모두 해제하면 결과가 빈 리스트가 된다
            return targetList.Where(d=> memberList.Contains(d.GetCardData().member) == true).ToList();
        }
    }

    /// <summary>
    /// 속성(PZTYPE) 필터 적용. 아무것도 선택하지 않았거나 전부 선택했으면 그대로 반환하고, 아니면 선택된 속성의 카드만 남긴다.
    /// </summary>
    private List<CardList> GetFilterSortPZTypeList(List<CardList> targetList)
    {
        var pztList = SortFilterPZTYPE.GetSelectSortList;

        // "전체"(PZTYPE_NONE)를 제외하고 선택된 속성이 하나도 없으면 필터를 적용하지 않는다
        if(SortFilterPZTYPE.CheckNothingSelect() == true)
            return targetList;

        // 일부만 선택된 경우: 카드 테이블 데이터의 pztype_id가 선택 목록에 있는 카드만 남긴다
        if( SortFilterPZTYPE.IsAll() == false )
        {
            return targetList.Where(d=> pztList.Contains(d.GetCardData().pztype_id) == true).ToList();
        }
        else
        {
            return targetList;
        }
    }

    #endregion

    #region  min level card
    /// <summary>
    /// 덱 슬롯 카드 중 레벨이 가장 낮은 카드의 gdid를 반환한다 (LevelLink 레벨을 결정하는 카드). 없으면 0.
    /// slotList — key: 슬롯 번호, value: 카드 gdid
    /// PageLevelLink_Mid에서 MinCardID로 넘겨, 하단 덱 카드 UI가 해당 카드를 "최소 레벨 카드"로 표시(IsMinCard)하는 데 사용한다.
    /// </summary>
    public int GetSlotMinCard(Dictionary<int, int> slotList)
    {
        if( slotList.Count <= 0 ) return 0;

        // 빈 슬롯(0)을 제외한 카드 gdid 목록
        var vList = slotList.Values.Where(d=> d > 0 ).ToList();

        // gdid → 인벤토리 카드 정보로 변환, 인벤토리에 없는 카드(null)는 제외
        var sortCardList =  vList
            .Select(d=> CardInventoryManager.Instance.GetCardInfoByGDID(d))
            .Where(d=> d != null)
            .ToList();

        if( sortCardList.Count <= 0 ) return 0;

        //최소 레벨 추출
        int minLevel = sortCardList.Min(d=> d.lv);
        
        //카드 선택
        // 최소 레벨 카드가 여러 장이면 Dictionary 열거 순서상 마지막 카드를 고른다 (보통 슬롯 번호가 가장 큰 카드)
        var card = sortCardList.LastOrDefault(d=> d.lv == minLevel);

        if( card == null ) return 0;

        return  card.gdid;
    }

    #endregion

    #region  short time list
    /// <summary>
    /// 쿨타임 단축 팝업에 표시할 단축 수단 리스트(ShortTimeList)를 새로 구성한다.
    /// PageLevelLink.OpenPopupLevelLinkInstantCompleteProcess()에서 광고 목록 API(ADList) 응답 후, 팝업을 열기 직전에 호출된다.
    /// 추가 순서(= 화면 표시 순서): 광고 → 보석 즉시 완료 → 시간 단축 티켓 1 / 2 / 3
    /// 각 항목은 조건을 만족하지 못하면(광고 횟수 소진, 티켓 미보유 등) null이 되어 리스트에서 빠진다.
    /// </summary>
    public void InitShortTimeList()
    {
        // 이전에 만든 리스트를 비우고 다시 만든다
        DisposeShortTimeList();

        //광고 데이터 추가
        var adData = GetShortTimeAD();
        if( adData != null ) ShortTimeList.Add(adData);

        //즉시 완료 데이터 추가
        var normalData = GetShorTimeNow();
        if( normalData != null ) ShortTimeList.Add(normalData);

        //티켓 데이터
        // 게임 설정 키 "sng_time_reduction1~3_id"에 단축 티켓 아이템 ID가 들어 있다 (변수명으로 보아 10분 / 30분 / 60분 티켓)
        var ticket10 = GetShortTimeTicket("sng_time_reduction1_id");
        if( ticket10 != null ) ShortTimeList.Add(ticket10);
        
        var ticket30 = GetShortTimeTicket("sng_time_reduction2_id");
        if( ticket30 != null ) ShortTimeList.Add(ticket30);

        var ticket60 = GetShortTimeTicket("sng_time_reduction3_id");
        if( ticket60 != null ) ShortTimeList.Add(ticket60);
    }


    /// <summary>
    /// 단축 수단 리스트에서 index번째 항목을 반환한다. 범위를 넘으면 null.
    /// ObjLevelLinkInstantcomplete_Bottom의 스크롤 아이템 생성 콜백에서 호출된다.
    /// </summary>
    public LevelLinkSlot_ShortTime GetShortTimeData(int index)
    {
        if( ShortTimeList.Count <= index) return null;

        return ShortTimeList[index];
    }
    

    /// <summary>
    /// 광고 단축 항목을 만든다. LevelLink 슬롯용 광고 데이터가 없거나, 사용 불가이거나, 남은 시청 횟수가 0이면 null.
    /// </summary>
    private LevelLinkSlot_ShortTime GetShortTimeAD()
    {
        // 광고 테이블에서 LevelLink 슬롯 광고 타입의 데이터
        var adData = CAdvertiseDataManager.Instance.GetAdvertiseData(AD_TYPE.AD_TYPE_LEVELLINK_SLOT);

        if( adData == null ) return null;
        // 테이블에서 사용하지 않도록 설정된 광고
        if( adData.ad_useable == false) return null;
        // 남은 시청 가능 횟수가 없음 (AdsManager의 서버 광고 목록은 팝업 직전에 호출한 ADList API 응답으로 갱신된다)
        if( AdsManager.Instance.GetAdsRemainCnt(adData.ad_id) <= 0) return null;

        LevelLinkSlot_ShortTime data = new LevelLinkSlot_ShortTime();

        data.UIType = SHORT_TIME_UI_TYPE.AD;
        data.AdData = adData;
        // (이 타이머 이름은 ObjLevelLinkReslotoption의 광고 UI 설정에는 전달되지 않는다)
        data.STR_TIMER_NAME = LEVELLINK_SLOT_AD;

        return data;
    }

    /// <summary>
    /// 보석 즉시 완료 항목을 만든다. 게임 설정 "levellink_cash_shorten_payment"가 없으면 null.
    /// 설정 값은 ObjLevelLinkReslotoption_Button에서 "몇 초당 보석 1개"인지(CostToTime)로 사용되어, 남은 시간에 비례한 보석 비용을 계산한다.
    /// </summary>
    private LevelLinkSlot_ShortTime GetShorTimeNow()
    {
        GameConfigData configData = CAccountConfigDataManager.Instance.GetGameConfigData("levellink_cash_shorten_payment");

        if( configData == null) return null;

        LevelLinkSlot_ShortTime data = new LevelLinkSlot_ShortTime();

        data.UIType = SHORT_TIME_UI_TYPE.NORMAL;
        data.ConfigData = configData;
        // 현재 LevelLink의 쿨타임 TimeStream 키. 버튼 UI가 이 키로 남은 시간을 읽어 비용을 갱신한다
        data.STR_TIMER_NAME = string.Format(LEVELLINK_SLOT_TIME_KEY_FORMAT, CurrentGDID);

        return data;
    }

    /// <summary>
    /// 시간 단축 티켓 항목을 만든다.
    /// strKey — 티켓 아이템 ID가 들어 있는 게임 설정 키. 설정이 없거나, 인벤토리에 티켓이 없거나 0개면 null.
    /// </summary>
    private LevelLinkSlot_ShortTime GetShortTimeTicket(string strKey)
    {
        GameConfigData configData = CAccountConfigDataManager.Instance.GetGameConfigData(strKey);
        if( configData == null ) return null;

        // 설정 값 = 티켓 아이템 ID → 인벤토리 보유 정보 조회
        int itemID = configData.ValueToInt;
        var itemData = ItemInventoryManager.Instance.GetItemData(itemID);

        //아이템 갯수가 없으면 리스트에 넣지 않는다
        if( itemData == null ) return null;
        if( itemData.count <= 0) return null;

        LevelLinkSlot_ShortTime data = new LevelLinkSlot_ShortTime();

        data.UIType = SHORT_TIME_UI_TYPE.TICKET;
        data.TicketData = itemData;

        return data;
    }
    

    /// <summary>
    /// 단축 수단 리스트를 비운다. InitShortTimeList() 시작 시와 매니저 Dispose()에서 호출된다.
    /// </summary>
    public void DisposeShortTimeList()
    {
        ShortTimeList.Clear();
    }

    #endregion

    #region  common popup
    
    /// <summary>
    /// 보석 사용 공통 흐름. 결과(CashShopResult)를 1회 발행하는 Observable을 반환한다.
    /// 첫 팝업(보석 사용 확인 또는 재화 부족 알림)은 이 메서드를 호출하는 즉시 열리고, 반환된 Observable은 흐름이 끝날 때 결과를 발행한다.
    /// - 보석이 충분하면: 보석 사용 확인 팝업 → 확인이면 IsOK = true, 취소면 IsOK = false
    /// - 보석이 부족하면: OpenNoticeOpenGemShop()으로 넘어가 "재화 부족" 안내 → (확인 시) 보석 상점. 이 경로는 항상 IsOK = false
    /// 호출: PopupLevelLinkInstantcomplete(보석 즉시 완료), PopupLevelLinkUnlock(해금 재료 구매)
    /// 호출부는 IsOK가 true일 때만 서버 요청으로 이어간다.
    /// </summary>
    public IObservable<CashShopResult> OpenPopupUseCash(int cost)
    {
        // 보유 보석이 비용보다 적으면 보석 부족 안내 흐름으로 분기
        if( CPlayer.Instance.Gem.Value < cost )
        {
            return OpenNoticeOpenGemShop(cost);
        }

        CashShopResult cashResult = new CashShopResult();
        cashResult.EnoughGem = true;

        // 보석 사용 확인 팝업은 이 시점에 바로 열린다 (반환된 Observable을 구독하기 전)
        var popupService = CCoreServices.GetCoreService<CPopupService>();
        var popup = popupService.OpenPopupUseCash(cost);

        // ShowAsObservable: 팝업이 닫힐 때 결과를 발행
        // SelectMany: 팝업 결과(IsSucess)를 CashShopResult 하나짜리 Observable로 바꿔 이어 붙인다 (두 분기 모두 같은 객체를 반환하고 IsOK만 다르다)
        return popup.ShowAsObservable()
        .SelectMany(result=>
        {
            if( result.IsSucess )
            {
                cashResult.IsOK = true;
                return Observable.Return(cashResult);
            }

            cashResult.IsOK = false;
            return Observable.Return(cashResult);
        });
    }

    /// <summary>
    /// 보석이 부족할 때의 안내 흐름. "재화가 부족합니다. 구매하러 가시겠습니까?"(UI_common_pop_dontbuy) 2버튼 알림을 띄운다.
    /// - 확인: OpenGemShop()으로 보석 상점을 열고, 상점이 닫힌 뒤의 결과를 발행
    /// - 취소: OpenGemShop = true, IsOK = false 결과를 바로 발행
    /// </summary>
    private IObservable<CashShopResult> OpenNoticeOpenGemShop(int cost)
    {
        CashShopResult cashResult = new CashShopResult();
        cashResult.OpenGemShop = true;
        cashResult.IsOK = false;

        var popupService = CCoreServices.GetCoreService<CPopupService>();
        var popup = popupService.NoticeAlertByKey("UI_common_pop_dontbuy", PopupAlert.BUTTON_TYPE.BTN_TWO);

        // 알림 팝업 결과에 따라 "보석 상점 Observable" 또는 "즉시 결과 Observable"로 이어 붙인다
        return popup.ShowAsObservable()
        .SelectMany(result=>
        {
            if( result.IsSucess )
            {
                return OpenGemShop(cost);
            }

            return Observable.Return(cashResult);
        });
    }
    
    /// <summary>
    /// 보석 상점(HUD 상점의 보석 카테고리)을 열고, 상점이 닫히면 그 시점의 보유 보석으로 EnoughGem을 채운 결과를 발행한다.
    /// 상점에서 보석을 사 와도 IsOK는 false이므로 호출부의 서버 요청은 진행되지 않는다 (사용자가 다시 시도해야 함).
    /// </summary>
    private IObservable<CashShopResult> OpenGemShop(int cost)
    {
        CashShopResult cashResult = new CashShopResult();
        cashResult.OpenGemShop = true;
        cashResult.IsOK = false;

        var popupService = CCoreServices.GetCoreService<CPopupService>();
        var popup = popupService.OpenPopupHudShop(GOODS_CATEGORY_TYPE.SHOP_CATEGORY_GEM);
        // 상점 팝업이 닫힐 때 결과를 받아 CashShopResult로 변환 (Select: 값 1:1 변환)
        return popup.ShowAsObservable()
        .Select(result=>
        {
            // 상점을 다녀온 뒤에도 보석이 부족한지 확인
            if( CPlayer.Instance.Gem.Value < cost )
            {
                cashResult.EnoughGem = false;
                return cashResult;
            }

            cashResult.EnoughGem = true;
            return cashResult;
        });
    }


    #endregion


    /// <summary>
    /// Singleton.Dispose() 재정의. 덱 편집 필터 상태와 단축 수단 리스트를 비운다.
    /// (서버 데이터 SLevelLinkList / SMissionList와 현재 페이지 ID는 비우지 않는다)
    /// </summary>
    public override void Dispose()
    {
        DisposeSortFilter();
        DisposeShortTimeList();
    }

}
