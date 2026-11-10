using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UniRx;
using UnityEngine.UI;
using SuperScrollView;
using System.Linq;

/// <summary>
/// LevelLink 슬롯(하단 카드 칸)에 등록할 카드 1장을 고르는 팝업.
/// - 여는 곳: PageLevelLink.OnClickLevelLinkCard() → OpenPopupLevelLinkSelectcard()
///   (LevelLink 덱 편집이 완료(lv > 0)되어 있고, 등록된 카드가 없고(slot == 0), 쿨타임도 없을 때)
/// - Setting : 등록 가능한 카드 gdid 목록(LevelLinkManager.GetLevelLinkCardIDList: 다른 LevelLink에 등록되지 않았고
///   level_link_lv == 0 인 보유 카드, 등급 높은 순 → 레벨 → gdid 순 정렬), 현재 등록된 카드(sData.slot), LevelLink 레벨(sData.lv)
/// - 카드는 한 줄에 4장씩(ObjLevelLinkSelectCardListGroup) 재사용 스크롤로 보여주며, 한 번에 1장만 선택된다.
/// - 확인 → 선택 카드가 원래 카드와 다르면 등록 안내 팝업(PopupLevelLinkCardChange, IsLevelLinkUnregister=false)을 띄우고,
///   그 팝업에서 확인해야 결과 IsSuccess=true로 닫힌다.
/// - Result  : IsSuccess, SelectCardID. 호출부는 IsSuccess면 LevelLinkManager.RequestLevelLinkCardSave(SelectCardID)로
///   카드 등록 API를 호출하고, 응답 후 LevelLinkSelectGDID 메시지로 페이지가 갱신된다.
/// </summary>
public class PopupLevelLinkSelectcard : CPopupUI<PopupLevelLinkSelectcard.Setting, PopupLevelLinkSelectcard.Result>
{
    // 카드 목록 재사용 스크롤 (아이템 1개 = 카드 4장짜리 한 줄)
    [SerializeField] private LoopListView2 ScrollUI;
    // 확인 버튼 — 선택 카드 등록 안내 팝업으로 진행
    [SerializeField] private Button BtnOK;
    // 닫기 버튼 — 결과 IsSuccess=false로 닫는다
    [SerializeField] private Button BtnClose;

    // InitUI 중복 실행 방지
    private bool IsInit = false;
    // 스크롤 한 줄에 들어가는 카드 수 (이름의 Gropu는 Group의 오타로 보임)
    private readonly int CardGropuCount = 4;
    // 스크롤 줄 수 = 카드 수 / 4 를 올림 (예: 9장 → 3줄). setting이 없으면 0
    private int ScrollCount
    {
        get
        {
            if( setting != null )
            {
                // 몫 = 꽉 찬 줄 수, 나머지가 있으면 마지막 한 줄 추가
                int q = setting.CardIDs.Count / CardGropuCount;
                int r = setting.CardIDs.Count % CardGropuCount;
                
                if( r > 0 ) q++;
                return q;
            }
            
            return 0;

        }
    }

    /// <summary>
    /// 팝업 생성 직후(BasePopup.Awake) 호출. SetData보다 먼저 실행된다.
    /// </summary>
    protected override void OnAwake()
    {
        base.OnAwake();

        // 닫힐 때 result를 호출부에 1회 전달하는 Subject
        subject = new Subject<Result>();
        // 기본 결과는 실패. 등록 안내 팝업에서 확인했을 때만 true
        result = new Result {IsSuccess = false};

        // 위에 등록 안내 팝업이 떠도 이 팝업은 숨기지 않는다
        Flags = PopupFlags.DISABLE_HIDE;
        
        InitUI();

    }

    /// <summary>
    /// 팝업 데이터 세팅. PageLevelLink.OpenPopupLevelLinkSelectcard()에서 팝업 생성 직후 호출된다.
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);

        SetUI();
    }

    /// <summary>
    /// 스크롤 초기화(개수 0으로 먼저 생성)와 버튼 바인딩을 한 번만 수행한다.
    /// </summary>
    private void InitUI()
    {
        if( IsInit) return;
        IsInit = true;

        if( ScrollUI != null )
        {
            // 아직 setting이 없으므로 0개로 초기화하고, 실제 개수는 SetUI에서 SetListItemCount로 지정
            ScrollUI.InitListView(0, OnGetItemByIndex);
        }

        if( BtnClose != null )
        {
            // 클릭 시 닫고 BUTTONDELAYTIME 동안 연타 방지
            BtnClose.BindToOnClick(_=>
            {
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnOK != null )
        {
            BtnOK.BindToOnClick(_=>
            {
                // 선택한 카드가 원래 등록 카드와 다를 때만 등록 진행
                // (호출부는 slot == 0 일 때만 이 팝업을 열기 때문에 OriginalCardSelect는 사실상 0이다.
                //  등록 가능한 카드가 하나도 없으면 CardSelectID도 0이라 그냥 닫힌다)
                if( setting.CardSelectID !=  setting.OriginalCardSelect )
                {
                    // result.IsSuccess = true;
                    // 선택 카드 ID를 결과에 먼저 기록해 두고, 성공 여부는 등록 안내 팝업 결과로 결정
                    result.SelectCardID = setting.CardSelectID;

                    OpenPopupLevelLinkCardChange();
                }
                else
                {
                    // 변경 사항 없음 → 결과 false로 닫기
                    Close();
                }

                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);

        }
    }

    /// <summary>
    /// 카드 수에 맞게 스크롤 줄 수를 설정하고, 보이는 줄들을 다시 그린다.
    /// </summary>
    private void SetUI()
    {
        if( ScrollUI != null )
        {
            ScrollUI.SetListItemCount(ScrollCount);
            // 화면에 보이는 아이템마다 OnGetItemByIndex를 다시 호출해 내용 갱신
            ScrollUI.RefreshAllShownItem();
        }
    }

    /// <summary>
    /// LoopListView2가 index번째 줄이 필요할 때 호출하는 콜백.
    /// 카드 목록에서 해당 줄의 카드 4장(마지막 줄은 남은 수만큼)을 잘라 ObjLevelLinkSelectCardListGroup에 넘긴다.
    /// </summary>
    private LoopListViewItem2 OnGetItemByIndex(LoopListView2 listView, int index)
    {
        // 범위 밖이면 null → 리스트 끝
        if (index < 0 ) return null;
        if( index >= ScrollCount ) return null;

        // 프리팹이 하나뿐이므로 첫 번째 프리팹 이름으로 아이템 생성/재활용
        var strPrefabName = listView.ItemPrefabDataList.First().mItemPrefab.name;
        var scrollItem = listView.NewListViewItem(strPrefabName);
        var item = scrollItem.GetComponent<ObjLevelLinkSelectCardListGroup>();

        // 새로 생성된 아이템만 1회 초기화
        if (scrollItem.IsInitHandlerCalled == false)
        {
            scrollItem.IsInitHandlerCalled = true;
            // 줄 프리팹의 HorizontalLayoutGroup을 꺼서 이후 자동 재배치가 일어나지 않게 한다 (카드 위치는 현재 배치로 고정)
            scrollItem.GetComponent<HorizontalLayoutGroup>().enabled = false;
            item.InitUI();
        }

        // 이 줄의 시작 인덱스와 카드 수
        int startID = index * CardGropuCount;
        int count = CardGropuCount;

        // 마지막 줄이면 나머지 카드 수만큼만 (나머지가 0이면 꽉 찬 4장)
        if( index == ScrollCount - 1)
        {
            int r = setting.CardIDs.Count % CardGropuCount;
            if( r != 0 )
            {
                count = r;
            }
        }
        
        // 선택 표시할 카드 목록 — 이 팝업은 단일 선택이므로 현재 선택 카드 1장만 담는다
        List<int> list = new List<int>();
        list.Add(setting.CardSelectID);

        item.SetData(new ObjLevelLinkSelectCardListGroup.Setting()
        {
            // GetRange(start, count): 전체 목록에서 이 줄에 해당하는 구간만 복사
            CardIDs = setting.CardIDs.GetRange(startID, count),
            SelectCardIDs = list,
            OnClickCardEvent = OnClickCardEvent,

        });
        
        item.SetUI();

        return scrollItem;
    }

    /// <summary>
    /// 카드 클릭 시 호출(ObjLevelLinkCard → ObjLevelLinkSelectCardListGroup → 여기).
    /// 선택 카드를 바꾸고 보이는 줄들을 다시 그려 선택 표시를 갱신한다.
    /// </summary>
    private void OnClickCardEvent(int cardID)
    {
        setting.SetCardSelectID(cardID);
        
        if( ScrollUI != null)
        {
            // OnGetItemByIndex가 다시 불리며 새 CardSelectID로 SelectCardIDs가 만들어진다
            ScrollUI.RefreshAllShownItem();
        }
    }

    /// <summary>
    /// 선택 카드 등록 안내 팝업(PopupLevelLinkCardChange)을 연다.
    /// IsLevelLinkUnregister=false → "안내" 제목으로 등록 시 적용될 LevelLink 레벨/카드 정보를 보여주는 모드.
    /// 그 팝업에서 확인(IsSuccess)하면 이 팝업도 결과 IsSuccess=true로 닫는다. 취소하면 이 팝업은 유지된다.
    /// </summary>
    private void OpenPopupLevelLinkCardChange()
    {
        var popup = CCoreServices.GetCoreService<CPopupService>().OpenPopupLevelLinkCardChange();

        // 선택 카드 gdid와 현재 슬롯 LevelLink 레벨 전달
        popup.SetData(new PopupLevelLinkCardChange.Setting( setting.CardSelectID, setting.LevelLinkLV)
        {
            IsLevelLinkUnregister = false,
        });

        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        // 안내 팝업이 닫힐 때 결과 1회 수신 후 직접 Dispose
        disposable.Disposable = popup.ShowAsObservable()
        .Subscribe(res =>
        {
            if( res.IsSuccess )
            {
                // 이 시점에 result.SelectCardID는 BtnOK에서 이미 기록되어 있다
                result.IsSuccess = true;
                Close();
            }

            disposable.Dispose();
        });
        
    }


    /// <summary>
    /// 팝업 입력 데이터. 생성자로만 초기 선택 상태를 정한다.
    /// </summary>
    public class Setting
    {
        // 선택 가능한 카드 gdid 목록 (이미 정렬된 상태로 전달됨)
        public List<int> CardIDs;
        // 현재 선택된 카드 gdid (카드 클릭 시 SetCardSelectID로 변경)
        public int CardSelectID{get; private set;}

        // 팝업을 열 때 슬롯에 등록되어 있던 카드 gdid (0이면 비어 있음) — 확인 시 변경 여부 비교용
        public int OriginalCardSelect {get; private set;}

        // 현재 슬롯의 LevelLink 레벨 (서버 sData.lv) — 등록 안내 팝업에 전달
        public int LevelLinkLV {get; private set;}
        
        /// <summary>
        /// list: 카드 목록, cardID: 현재 등록 카드(sData.slot), levelLinkLV: LevelLink 레벨(sData.lv)
        /// </summary>
        public Setting(List<int> list, int cardID, int levelLinkLV)
        {
            CardIDs = list;
            OriginalCardSelect = cardID;
            LevelLinkLV = levelLinkLV;

            // 등록된 카드가 없으면 목록의 첫 카드를 기본 선택 (목록이 비어 있으면 FirstOrDefault → 0)
            if( cardID == 0 )
            {
                CardSelectID = list.FirstOrDefault();
            }
            // 등록된 카드가 있으면 그 카드를 선택 상태로 시작
            else
            {
                CardSelectID = cardID;
            }
        }

        /// <summary>
        /// 선택 카드 변경 (OnClickCardEvent에서 호출).
        /// </summary>
        public void SetCardSelectID(int cardID)
        {
            CardSelectID = cardID;
        }
    }

    /// <summary>
    /// 팝업 결과. 닫힐 때 호출부(PageLevelLink.OpenPopupLevelLinkSelectcard)로 1회 전달된다.
    /// </summary>
    public class Result
    {
        // true: 등록 안내 팝업에서 확인까지 완료 → 호출부가 카드 등록 API 호출
        public bool IsSuccess;
        // 등록할 카드 gdid (IsSuccess가 false여도 확인 버튼을 눌렀다면 값이 들어 있을 수 있음)
        public int SelectCardID = 0;


    }
}
