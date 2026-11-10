using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// LevelLink 덱 카드 한 줄(가로 목록). 프리팹에 미리 배치된 카드 슬롯(ObjLevelLinkCard)들을 데이터 개수만큼 켜서 채운다.
/// - 부모: PageLevelLink_Mid_BottomUI (CardListUI00 = 윗줄, CardListUI01 = 아랫줄)
/// - 자식: CardList 의 ObjLevelLinkCard 들
/// - Setting 은 Mid_BottomUI 의 SetCardListUI_XXX 에서 이 줄에 해당하는 슬롯 구성(GroupList)과 카드 ID 목록(CardIDs)만 잘라서 넘긴다.
/// - 표시할 칸 수는 GroupList 개수로 결정되고, 남는 칸은 숨긴다.
/// </summary>
public class ObjLevelLinkSelectCardList : BaseUILayout<ObjLevelLinkSelectCardList.Setting>
{
    // 프리팹에 배치된 카드 슬롯들 (인덱스 순서 = 이 줄 안에서의 슬롯 순서)
    [SerializeField] private List<ObjLevelLinkCard> CardList;

    /// <summary>
    /// 모든 카드 슬롯 1회성 초기화 (각 카드는 InitUI 에서 일단 숨겨짐). Mid_BottomUI.InitUI 에서 호출.
    /// </summary>
    public override void InitUI()
    {
        CardList.ForEach(x => x.InitUI());
    }

    /// <summary>
    /// 카드 슬롯마다 같은 인덱스의 카드 ID / 슬롯 구성을 찾아 채운다.
    /// 슬롯 구성이 없는 인덱스(이 줄에서 쓰지 않는 칸)는 숨긴다. Mid_BottomUI 에서 SetData 직후 호출.
    /// </summary>
    public override void SetUI()
    {
        // ForEach((인덱스, 요소)): Utility 의 인덱스 포함 ForEach 확장 메서드
        CardList.ForEach((idx, ui)=>
        {
            // 기본값: 빈 칸(카드 0), 슬롯 구성 없음
            int cardID = 0;
            LevelLinkGroupList slotData = null;

            // 같은 인덱스의 카드 ID (범위 밖이면 0 = 빈 칸)
            if( setting.CardIDs != null )
            {
                if( setting.CardIDs.Count > idx )
                    cardID = setting.CardIDs[idx];
            }

            // 같은 인덱스의 슬롯 구성 (범위 밖이면 null)
            if( setting.GroupList != null )
            {
                if( setting.GroupList.Count > idx)
                {
                    slotData = setting.GroupList[idx];
                }
            }

            // 슬롯 구성이 없으면 이 칸은 사용하지 않으므로 숨김 (카드 ID 가 있어도 무시됨)
            if( slotData == null )
            {
                ui.SetEnable( false);
            }
            else
            {
                ui.SetData(new ObjLevelLinkCard.Setting()
                {
                    // 최저 레벨 카드면 강조 이펙트 (빈 칸은 ObjLevelLinkCard.SetFx_MinCard 에서 따로 꺼짐)
                    IsMinCard = cardID == setting.MinCardID,
                    CardID = cardID,
                    // 슬롯의 멤버 조건 → 카드에 멤버 이름 라벨 표시
                    MemberType = slotData.level_link_group_con,
                    // 카드 클릭 → PageLevelLink.OnClickCard (덱 편집 팝업)
                    OnClickCardEvent = setting.OnClickCardEvent,
                });

                ui.SetUI();
            }

        });
    }

    /// <summary>
    /// 카드 한 줄 데이터. PageLevelLink_Mid_BottomUI.SetCardListUI_XXX 에서 생성.
    /// </summary>
    public class Setting
    {
        // 덱 전체에서 최저 레벨 카드 GDID
        public int MinCardID = 0;

        // 이 줄에 해당하는 슬롯 구성 (Count = 이 줄에 표시할 칸 수)
        public List<LevelLinkGroupList> GroupList;
        // 이 줄에 해당하는 카드 GDID 목록 (GroupList 와 같은 인덱스로 매칭, 0 = 빈 칸)
        public List<int> CardIDs;
        // 카드 클릭 콜백
        public Action<int> OnClickCardEvent;
    }
}
