using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
/// <summary>
/// LevelLink 중단 영역의 하단 부분 - LevelLink 덱 카드 목록 (최대 7칸을 2줄로 배치).
/// - 부모: PageLevelLink_Mid (BottomUI 필드)
/// - 자식: CardListUI00(윗줄), CardListUI01(아랫줄) - 둘 다 ObjLevelLinkSelectCardList (카드 여러 장을 가로로 나열)
/// - Setting 은 PageLevelLink_Mid.SetUI 에서 채워진다 (최저 레벨 카드, 슬롯 구성, 슬롯별 카드, 클릭 콜백).
/// - SetUI 에서 슬롯 개수(GroupList.Count)에 따라 윗줄/아랫줄에 몇 칸씩 나눌지 결정한다. (아래 블록 주석 참고)
/// </summary>
public class PageLevelLink_Mid_BottomUI : BaseUILayout<PageLevelLink_Mid_BottomUI.Setting>
{
    // 윗줄 카드 목록
    [SerializeField] private ObjLevelLinkSelectCardList CardListUI00;
    // 아랫줄 카드 목록 (슬롯 4개 이하면 숨김)
    [SerializeField] private ObjLevelLinkSelectCardList CardListUI01;

    // InitUI 중복 실행 방지
    private bool IsInit = false;

    /*
    1,2,3,4

    1, 2, 3
    4, 5,

    1,2,3,
    4,5,6

    1,2,3,4
    5,6,7
    */
    // ↑ 슬롯 개수별 배치: 4개 이하 = 한 줄 / 5개 = 3 + 2 / 6개 = 3 + 3 / 7개 = 4 + 3 (숫자는 슬롯 순서)

    /// <summary>
    /// 윗줄/아랫줄 카드 목록 1회성 초기화. PageLevelLink_Mid.InitUI 에서 호출.
    /// </summary>
    public override void InitUI()
    {
        if( IsInit ) return;
        IsInit = true;

        if( CardListUI00 != null )
        {
            CardListUI00.InitUI();
        }

        if( CardListUI01 != null )
        {
            CardListUI01.InitUI();
        }
    }

    /// <summary>
    /// 슬롯 개수(SlotCount = GroupList.Count)에 따라 배치 방식을 골라 그린다.
    /// 호출: PageLevelLink_Mid.SetUI (SetData 직후)
    /// </summary>
    public override void SetUI()
    {
        // SetData 전에 호출된 경우 방어
        if( setting == null ) return;
        if( setting.SlotCount <= 4)
        {
            SetCardListUI_4OrLess();
        }
        else if( setting.SlotCount <= 6)
        {
            SetCardListUI_6OrLess();
        }
        else
        {
            SetCardListUI_7OrMore();
        }
    }

    // 슬롯이 4개 이하 일 경우
    // → 아랫줄은 숨기고, 윗줄 하나에 모든 슬롯을 표시
    private void SetCardListUI_4OrLess()
    {
        SetEnableCardListUI01(false);

        if( CardListUI00 != null )
        {
            ObjLevelLinkSelectCardList.Setting cardUISetting = new ObjLevelLinkSelectCardList.Setting();
            // 최저 레벨 카드 GDID (해당 카드에 강조 이펙트)
            cardUISetting.MinCardID = setting.MinCardID;
            // 슬롯 구성 전체 (슬롯별 멤버 조건 → 카드 아래 멤버 이름 표시)
            cardUISetting.GroupList = setting.GroupList;
            // 카드 클릭 → 덱 편집 팝업
            cardUISetting.OnClickCardEvent = setting.OnClickCardEvent;

            //이렇게 하면 안된다 카드 덱 편집에서 이상해짐
            // cardUISetting.CardIDs = setting.SlotList.Values.Where(slot=>slot > 0  ).ToList();
            
            //슬롯 정보(GroupList)만큼만 가져온다
            // (위 LINQ 방식은 0인 빈 칸을 빼버려서 카드가 앞으로 당겨지고 슬롯 위치가 어긋나므로 사용하지 않음)
            // 슬롯 순서 1 ~ GroupList.Count 를 차례로 돌며 카드 GDID 를 담는다. 빈 칸은 0 그대로 들어가 위치가 유지된다.
            List<int> cardIDs = new List<int>();
            for(int i = 0 ; i < setting.GroupList.Count; i++)
            {
                // SlotList 의 키는 1부터 시작하는 슬롯 순서
                int slotKey = i + 1;
                cardIDs.Add(setting.SlotList[slotKey]);
            }

            cardUISetting.CardIDs = cardIDs;


            CardListUI00.SetData(cardUISetting);
            CardListUI00.SetUI();
        }
    }

    //슬롯이 4개 초과 6개 이하일 경우
    // → 윗줄 3칸(슬롯 1~3) + 아랫줄 나머지(슬롯 4~5 또는 4~6)
    private void SetCardListUI_6OrLess()
    {
        SetEnableCardListUI01(true);
        // 윗줄: 슬롯 1~3
        if( CardListUI00 != null )
        {
            ObjLevelLinkSelectCardList.Setting cardUISetting = new ObjLevelLinkSelectCardList.Setting();
            cardUISetting.MinCardID = setting.MinCardID;
            // GetRange(시작 인덱스, 개수): GroupList 의 0~2번(슬롯 1~3)만 잘라서 전달
            cardUISetting.GroupList = setting.GroupList.GetRange(0, 3);
            cardUISetting.OnClickCardEvent = setting.OnClickCardEvent;
            cardUISetting.CardIDs = new List<int>(){    setting.SlotList[1],
                                                        setting.SlotList[2],
                                                        setting.SlotList[3]};

            CardListUI00.SetData(cardUISetting);
            CardListUI00.SetUI();
        }

        if( CardListUI01 != null )
        {
            ObjLevelLinkSelectCardList.Setting cardUISetting = new ObjLevelLinkSelectCardList.Setting();
            cardUISetting.MinCardID = setting.MinCardID;
            // 아랫줄: GroupList 의 3번 인덱스부터 남은 개수(슬롯 5개면 2개, 6개면 3개)
            cardUISetting.GroupList = setting.GroupList.GetRange(3, setting.SlotCount - 3);
            cardUISetting.OnClickCardEvent = setting.OnClickCardEvent;
            // 카드 ID 는 슬롯 4~6 을 항상 3개 넣는다. 슬롯이 5개면 GroupList 가 2개뿐이라
            // ObjLevelLinkSelectCardList 가 3번째 칸을 숨기므로 슬롯 6 값은 표시되지 않는다.
            // (SlotList 에 6 키가 없으면 예외가 나지만, PageLevelLink 에서 1~7 키를 항상 채워서 넘긴다)
            cardUISetting.CardIDs = new List<int>(){    setting.SlotList[4],
                                                        setting.SlotList[5],
                                                        setting.SlotList[6]};

            CardListUI01.SetData(cardUISetting);            
            CardListUI01.SetUI();
        }
    }

    //슬롯이 7개 이상일 경우
    // → 윗줄 4칸(슬롯 1~4) + 아랫줄 3칸(슬롯 5~7)
    // (서버 데이터가 card_1 ~ card_7 필드뿐이라 슬롯은 최대 7개이며, 카드 ID 도 슬롯 7 까지만 넣는다)
    private void SetCardListUI_7OrMore()
    {
        SetEnableCardListUI01(true);

        // 윗줄: 슬롯 1~4
        if( CardListUI00 != null )
        {
            ObjLevelLinkSelectCardList.Setting cardUISetting = new ObjLevelLinkSelectCardList.Setting();
            cardUISetting.MinCardID = setting.MinCardID;
            cardUISetting.GroupList = setting.GroupList.GetRange(0, 4);
            cardUISetting.OnClickCardEvent = setting.OnClickCardEvent;
            cardUISetting.CardIDs = new List<int>(){    setting.SlotList[1],
                                                        setting.SlotList[2],
                                                        setting.SlotList[3],
                                                        setting.SlotList[4]};

            CardListUI00.SetData(cardUISetting);
            CardListUI00.SetUI();
        }

        if( CardListUI01 != null )
        {
            ObjLevelLinkSelectCardList.Setting cardUISetting = new ObjLevelLinkSelectCardList.Setting();
            cardUISetting.MinCardID = setting.MinCardID;
            // 아랫줄: GroupList 의 4번 인덱스부터 남은 전부 (슬롯 5~)
            cardUISetting.GroupList = setting.GroupList.GetRange(4, setting.SlotCount - 4);
            cardUISetting.OnClickCardEvent = setting.OnClickCardEvent;
            cardUISetting.CardIDs = new List<int>(){    setting.SlotList[5],
                                                        setting.SlotList[6],
                                                        setting.SlotList[7]};

            CardListUI01.SetData(cardUISetting);
            CardListUI01.SetUI();
        }
    }

    // 아랫줄 카드 목록 GameObject on/off (BaseUILayout.SetEnable → SetActive)
    private void SetEnableCardListUI01(bool isEnable)
    {
        if( CardListUI01 != null )
        {
            CardListUI01.SetEnable(isEnable);
        }
    }

    /// <summary>
    /// 중단-하단 데이터. PageLevelLink_Mid.SetUI 에서 생성.
    /// </summary>
    public class Setting
    {
        // 덱 카드 중 최저 레벨 카드 GDID (LevelLinkManager.GetSlotMinCard, 없으면 0)
        public int MinCardID = 0;
        // 덱 슬롯 구성 테이블 (순서대로 슬롯 1, 2, ... / 슬롯별 멤버 조건)
        public List<LevelLinkGroupList> GroupList;
        // { 슬롯 순서(1~7) : 카드 GDID }
        public Dictionary<int, int> SlotList = new Dictionary<int, int>();
        // 덱 카드 클릭 → PageLevelLink.OnClickCard (덱 편집 팝업)
        public Action<int> OnClickCardEvent;

        // 이 LevelLink의 덱 슬롯 개수
        public int SlotCount => GroupList.Count;
    }
}
