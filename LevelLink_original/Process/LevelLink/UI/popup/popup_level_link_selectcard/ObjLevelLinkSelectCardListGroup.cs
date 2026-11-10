using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Game.RestAPI;
using System.Linq;

/// <summary>
/// 카드 선택 스크롤의 한 줄(카드 최대 4장) UI. 두 곳에서 공용으로 쓰인다.
/// - PopupLevelLinkSelectcard: 슬롯에 등록할 카드 1장 선택 (선택 표시만 사용, IsCheckNotSelect = false)
/// - ObjLevelLinkDeck_MidUI_Scroll(LevelLink 덱 편집 팝업): 덱 슬롯에 넣을 카드 선택.
///   IsCheckNotSelect = true 와 함께 GroupList/SlotList를 넘겨, 더 이상 넣을 수 없는 카드에 "선택 불가" 표시를 한다.
/// - 자식: 프리팹에 배치된 ObjLevelLinkCard 슬롯들. 카드 수만큼 켜고 나머지는 끈다.
/// - 카드 클릭 이벤트는 Setting.OnClickCardEvent로 상위(팝업/스크롤)에 그대로 전달한다.
/// </summary>
public class ObjLevelLinkSelectCardListGroup : BaseUILayout<ObjLevelLinkSelectCardListGroup.Setting>
{
    // 한 줄에 배치된 카드 UI 슬롯 목록
    [SerializeField] private List<ObjLevelLinkCard> CardList;

    /// <summary>
    /// 스크롤 아이템이 처음 생성될 때 1회 호출(상위에서 IsInitHandlerCalled로 보장). 카드 슬롯들을 초기화한다.
    /// </summary>
    public override void InitUI()
    {
        CardList.ForEach(x => x.InitUI());
    }

    /// <summary>
    /// 이 줄의 카드 ID 목록으로 각 카드 슬롯을 세팅한다. 카드가 없는 뒤쪽 슬롯은 끈다.
    /// 스크롤 RefreshAllShownItem 때마다 다시 호출되어 선택/선택 불가 표시가 갱신된다.
    /// </summary>
    public override void SetUI()
    {
        // ForEach((idx, ui)): 프로젝트 확장 메서드 — 인덱스와 요소를 함께 넘겨준다
        CardList.ForEach((idx, ui)=>
        {

            // 이 슬롯 인덱스에 카드가 있으면 표시
            if( setting.CardIDs.Count > idx )
            {
                int cardID = setting.CardIDs[idx];

                ui.SetEnable(true);
                ObjLevelLinkCard.Setting cardUISetting = new ObjLevelLinkCard.Setting();
                cardUISetting.CardID = cardID;
                // 선택 목록(SelectCardIDs)에 포함되면 선택 표시
                cardUISetting.IsSelect = setting.IsSelectCard(cardID);
                // 슬롯 멤버 조건 표시는 사용하지 않음 (ObjLevelLinkCard.Setting의 기본값 대신 NONE 지정)
                cardUISetting.MemberType = MEMBER_TYPE.NONE;
                // 카드 클릭 시 이 클래스의 OnClickEvent → 상위 콜백
                cardUISetting.OnClickCardEvent = OnClickEvent;

                // 덱 편집에서만: 더 넣을 수 없는 카드면 "선택 불가" 표시
                if( setting.IsCheckNotSelect )
                {
                    cardUISetting.IsNotSelected = setting.IsNotSelect(cardID);
                }

                ui.SetData(cardUISetting);
                ui.SetUI();

            }
            // 카드가 없는 슬롯(마지막 줄의 빈 칸)은 숨김
            else
            {
                ui.SetEnable(false);
            }
        });
    }

    /// <summary>
    /// 카드 UI 클릭 → 상위(PopupLevelLinkSelectcard.OnClickCardEvent 또는 ObjLevelLinkDeck_MidUI_Scroll.OnClickCardEvent)로 카드 gdid 전달.
    /// </summary>
    public void OnClickEvent(int cardID)
    {
        if( setting.OnClickCardEvent != null )
        {
            setting.OnClickCardEvent.Invoke(cardID);
        }
    }

    /// <summary>
    /// 카드 한 줄 입력 데이터.
    /// </summary>
    public class Setting
    {
        // 이 줄에 표시할 카드 gdid 목록 (최대 4개, 전체 목록에서 GetRange로 잘라낸 구간)
        public List<int> CardIDs;
        // 선택 표시할 카드 gdid 목록 (카드 선택 팝업: 1장 / 덱 편집: 현재 덱 슬롯에 들어간 카드들)
        public List<int> SelectCardIDs;
        // 카드 클릭 시 호출할 상위 콜백
        public Action<int> OnClickCardEvent;


        //LevelLink 덱 세팅시 선택 할 수 있는지 여부 체크
        public bool IsCheckNotSelect = false;
        // LevelLink 그룹 슬롯 테이블 목록 (슬롯 순서 level_link_group_order, 멤버 조건 level_link_group_con)
        public List<LevelLinkGroupList> GroupList;
        // 현재 덱 슬롯 상태: key = 슬롯 순서(level_link_group_order), value = 들어간 카드 gdid (0이면 빈 슬롯)
        public Dictionary<int, int> SlotList;
        // 이 클래스 안에서는 사용하지 않는다
        public LevelLinkList SData; //LevelLink 서버 데이터


        /// <summary>
        /// cardID가 선택 목록(SelectCardIDs)에 있는지 여부. 목록이 null/비어 있으면 false.
        /// </summary>
        public bool IsSelectCard(int cardID)
        {
            if( SelectCardIDs == null ) return false;
            if( SelectCardIDs.Count <= 0) return false;

            return SelectCardIDs.Contains(cardID);
        }

        /// <summary>
        /// 덱 편집 시 이 카드에 "선택 불가" 표시를 할지 판단한다. true = 선택 불가 표시.
        /// 판단 순서:
        /// 1) 이미 덱에 들어간 카드 → false (선택 해제할 수 있어야 하므로)
        /// 2) 빈 슬롯이 하나도 없음 → true
        /// 3) 카드 멤버 전용 슬롯이 테이블에 있으면 → 그 중 빈 슬롯이 있으면 false, 없으면 true
        /// 4) 멤버 전용 슬롯이 없으면 → 그룹 전체 조건 슬롯을 검사하지만, 결과와 상관없이 true
        /// </summary>
        public bool IsNotSelect(int cardID)
        {
            //이미 선택한 카드이면 선택 불가 UI 표시 하지 않는다
            if( IsSelectCard(cardID)) return false;

            //빈슬롯이 있는지 여부, 없을 경우 활성화
            // LINQ: 값(카드 gdid)이 0인 항목만 걸러 새 Dictionary로 만든다 = 빈 슬롯 목록
            var emptyList = SlotList.Where(d=>d.Value == 0 ).ToDictionary(x=>x.Key, x=>x.Value);
            if( emptyList.Count <= 0 ) return true;

            //멤버 슬롯이 있는지 확인
            // 카드 테이블에서 이 카드의 멤버 타입 조회
            var cardData = CCardDataManager.Instance.GetCardListData(cardID);
            MEMBER_TYPE cardMemType = cardData.member;

            // 슬롯 조건이 이 카드의 멤버와 같은 그룹 슬롯들 (= 멤버 전용 슬롯)
            var memSlotList = GroupList.Where(d=>d.level_link_group_con == cardMemType ).ToList();

            //멤버 특정 슬롯이 비어 있는지 확인한다
            if( 0 < memSlotList.Count)
            {
                // 변수명과 달리 true = "비어 있는 멤버 전용 슬롯이 없음"(선택 불가) 의미로 쓰인다
                bool isEmptyMemberSlot = true;
                for(int i = 0 ; i < memSlotList.Count(); i++)
                {
                    int slotID = memSlotList[i].level_link_group_order;
                    if( SlotList.ContainsKey(slotID))
                    {
                        //비어 있으므로
                        if( SlotList[slotID] == 0)
                        {
                            // 빈 멤버 전용 슬롯 발견 → 선택 가능(false)
                            isEmptyMemberSlot = false;
                            break;
                        }
                    }
                }
                //멤버 슬롯 비어 있으므로 선택 가능한 상태이다
                // 멤버 전용 슬롯이 모두 차 있으면 그룹 전체 조건 슬롯이 비어 있어도 true(선택 불가)가 반환된다
                return  isEmptyMemberSlot;
            }


            //멤버가 지정되어 있지 않는 슬롯 체크
            // 슬롯 조건이 그룹 전체(멤버 무관)인 슬롯들의 순서 번호 목록 (지연 실행 IEnumerable)
            var normalSlotIDs = GroupList
            .Where(d=>d.level_link_group_con == MEMBER_TYPE.BTS)
            .Select(d=>d.level_link_group_order);

            if( 0 < normalSlotIDs.Count() )
            {
                //비어 있는 슬롯이 있다.
                // 빈 슬롯 중 그룹 전체 조건 슬롯에 해당하는 것
                var emptySlot = emptyList.Where(d=>normalSlotIDs.Contains(d.Key));
                // [참고] 빈 그룹 전체 슬롯이 있을 때도 true(선택 불가)를 반환하고, 아래 기본 반환도 true라
                //        멤버 전용 슬롯이 없는 카드는 항상 선택 불가로 표시된다. 주석("비어 있는 슬롯이 있다")과 반환값이 반대로 보인다
                if( 0 < emptySlot.Count() ) return true;
            }


            return true;
        }
    }
}
