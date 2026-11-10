using System;
using UnityEngine;
using UnityEngine.UI;
using UniRx;


/// <summary>
/// LevelLink에서 쓰는 카드 1장 UI (카드 슬롯).
/// 사용처(부모):
///  - PageLevelLink_Mid_TopUI.CardUI      : 가운데 큰 LevelLink 카드 (LevelLinkLevel / CardLevelLinkLevel / IsOrigLevel 사용)
///  - ObjLevelLinkSelectCardList.CardList : LevelLink 덱 카드 목록의 한 칸 (MemberType / IsMinCard 사용)
///  - 팝업 쪽: PopupLevelLinkCardChange_MidUI_Card(카드 변경 좌/우), ObjLevelLinkSelectCardListGroup(카드 선택, IsSelect 등)
/// 자식: MemberNameUI(ObjLevelLinkCardMemberName, 슬롯 멤버 조건 이름), CardUI(ObjCardInfo2, 공용 카드 표시)
/// Setting 은 부모가 SetData 로 넘기며, SetUI 에서 각 표시 요소를 Setting 값에 맞춰 켜고 끈다.
/// </summary>
public class ObjLevelLinkCard : BaseUILayout<ObjLevelLinkCard.Setting>
{
    // 카드 본체 루트 (빈 슬롯이면 꺼짐)
    [SerializeField] private GameObject ObjCard;
    // LevelLink 버프 표시 (Setting.IsLevelLinkBuff)
    [SerializeField] private GameObject ObjLevelLinkBuff;
    // 선택 표시 (Setting.IsSelect)
    [SerializeField] private GameObject ObjSelect;

    [Header("배경")]
    // LevelLink 레벨 1 이상일 때 배경 / 0 이하일 때 비활성 배경
    [SerializeField] private GameObject ObjBG;
    [SerializeField] private GameObject ObjBGDisable;

    // 슬롯의 멤버 조건 이름 표시 (특정 멤버 슬롯일 때만 보임)
    [SerializeField] private ObjLevelLinkCardMemberName MemberNameUI;

    // 공용 카드 표시 컴포넌트 (썸네일/등급/레벨/LevelLink 레벨 등)
    [SerializeField] private ObjCardInfo2 CardUI;
    // 카드 클릭 버튼 → setting.OnClickCardEvent(CardID)
    [SerializeField] private Button BtnSelect;

    [Header("선택 할 수 없을때 UI")]
    // 더 이상 선택할 수 없을 때 덮는 딤 (Setting.IsNotSelected, 단 IsSelect 면 항상 꺼짐)
    [SerializeField] private GameObject ObjDim;

    [Header("연출")]
    // 최저 레벨 카드 이펙트를 붙일 부모
    [SerializeField] private GameObject ObjFxParent;

    // 최저 레벨 카드 강조 이펙트(Fx_LevelLink_Standard) 인스턴스. SetFx_MinCard 에서 최초 1회 생성 후 재사용
    private GameObject ObjFxMinCard;

    /// <summary>
    /// 1회성 초기화: 일단 자신을 숨기고(SetUI 에서 다시 켬), 멤버 이름 UI 초기화, 클릭 버튼 바인딩.
    /// 호출: 부모의 InitUI (Mid_TopUI.InitUI / ObjLevelLinkSelectCardList.InitUI 등)
    /// </summary>
    public override void InitUI()
    {
        SetEnable(false);

        if( MemberNameUI != null )
        {
            MemberNameUI.InitUI();
        }

        // 클릭 시 OnClickEvent 실행, BUTTONDELAYTIME 동안 연타 방지. AddTo(this) 로 파괴 시 해제
        if( BtnSelect != null )
        {
            BtnSelect.BindToOnClick(_=>
            {
                OnClickEvent();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);

        }
    }

    /// <summary>
    /// Setting 값에 맞춰 카드 슬롯의 모든 표시 요소를 갱신한다. (부모가 SetData 직후 호출)
    /// </summary>
    public override void SetUI()
    {
        // InitUI 에서 숨겨둔 자신을 켠다
        SetEnable(true);
        // 빈 슬롯(CardID <= 0)이면 카드 본체를 숨김
        SetEnableEmpty(setting.IsEmpty);
        SetEnableObjLevelLinkBuff();
        SetEnableObjSelect();
        SetCardInfo();
        SetMemberName();

        SetBG();
        SetBGDisable();
        SetNotSelectDim();

        SetFx_MinCard();
    }


    // isEnable(= 비어있음)이 true 면 카드 본체 숨김, false 면 표시
    private void SetEnableEmpty(bool isEnable)
    {
        ObjCard.SetActive(!isEnable);
    }

    // LevelLink 버프 표시 on/off
    private void SetEnableObjLevelLinkBuff()
    {
        ObjLevelLinkBuff.SetActive(setting.IsLevelLinkBuff);
    }

    // 선택 표시 on/off
    private void SetEnableObjSelect()
    {
        ObjSelect.SetActive(setting.IsSelect);
    }

    /// <summary>
    /// 공용 카드 UI(ObjCardInfo2)에 카드 정보 표시. 빈 슬롯이면 아무것도 하지 않는다(카드 본체가 숨겨져 있음).
    /// </summary>
    private void SetCardInfo()
    {
        if( setting.CardID == 0 ) return;
        if( CardUI == null ) return;

        // 인벤토리에서 카드 정보를 찾아 썸네일/이름/등급/레벨 등을 세팅
        CardUI.InitCardID(setting.CardID);
        // true: 카드 원래 레벨 오브젝트 표시 / false: LevelLink 레벨 오브젝트 표시
        CardUI.SetEditEnableLevel(setting.IsOrigLevel);

        // LevelLink 레벨 텍스트 값 지정
        CardUI.SetEditLevelLinkLV(setting.CardLevelLinkLevel);

    }

    /// <summary>
    /// 슬롯 멤버 조건 이름 표시. 특정 멤버 조건일 때만 보이고, 그룹 전체/NONE 등은 숨긴다 (ObjLevelLinkCardMemberName.SetUI)
    /// [참고] InitUI 와 달리 MemberNameUI null 체크가 없음
    /// </summary>
    private void SetMemberName()
    {
        MemberNameUI.SetData(new ObjLevelLinkCardMemberName.Setting()
        {
            MemberType = setting.MemberType,
        });
        MemberNameUI.SetUI();
    }

    // LevelLink 레벨 1 이상이면 일반 배경 표시
    private void SetBG()
    {
        if( ObjBG != null )
        {
            ObjBG.SetActive(setting.LevelLinkLevel > 0);
        }
    }

    // LevelLink 레벨 0 이하이면 비활성 배경 표시 (SetBG 와 반대 조건)
    private void SetBGDisable()
    {
        if( ObjBGDisable != null )
        {
            ObjBGDisable.SetActive(setting.LevelLinkLevel <= 0);
        }
    }

    // 선택 불가 딤: 선택된 카드는 항상 딤 off, 그 외에는 IsNotSelected 값대로
    private void SetNotSelectDim()
    {
        if( ObjDim == null) return;

        if( setting.IsSelect )
        {
            ObjDim.SetActive(false);
        }
        else
        {
            ObjDim.SetActive(setting.IsNotSelected);
        }
    }

    /// <summary>
    /// 최저 레벨 카드 강조 이펙트 on/off.
    /// ObjFxParent 가 연결된 카드에서만 동작하며, 이펙트는 최초 1회 에셋번들에서 로드해 생성하고 이후 SetActive 로만 제어한다.
    /// </summary>
    private void SetFx_MinCard()
    {
        if( ObjFxParent == null) return;

        // 지연 생성 (최초 1회)
        if( ObjFxMinCard == null )
        {
            var path = CPREFAB_KEY.Fx_LevelLink_Standard;
            var objFx = CResourceManager.Instance.LoadObject(path, ResourceLoadFrom.ASSETBUNDLE, this) as GameObject;

            ObjFxMinCard = Utility.AddChild( ObjFxParent, objFx);

            // 형제 중 첫 번째로 보내 다른 UI 뒤에 그려지게 함
            var rc = ObjFxMinCard.GetComponent<RectTransform>();
            rc.SetAsFirstSibling();
        }

        // 빈 슬롯이면 무조건 끄고, 카드가 있으면 최저 레벨 카드일 때만 켠다
        // (빈 슬롯은 CardID 0 이고 MinCardID 도 0 일 수 있어 IsMinCard 가 true 가 되므로 이 체크가 필요)
        if( setting.CardID <= 0 )
        {
            ObjFxMinCard.SetActive(false);
        }
        else
        {
            ObjFxMinCard.SetActive(setting.IsMinCard);
        }
    }

    // 카드 클릭 → 부모가 넘긴 콜백에 CardID 전달 (빈 슬롯이면 0)
    private void OnClickEvent()
    {
        setting.OnClickCardEvent?.Invoke(setting.CardID);
    }


    /// <summary>
    /// 카드 슬롯 데이터. 부모(Mid_TopUI / ObjLevelLinkSelectCardList / 팝업들)가 생성해서 SetData 로 넘긴다.
    /// </summary>
    public class Setting
    {
        // 빈 슬롯 여부 (CardID 로 계산)
        public bool IsEmpty => CardID <= 0;
        // LevelLink 버프 표시 여부
        public bool IsLevelLinkBuff = false;
        // 선택 표시 여부 (선택되면 딤도 꺼짐)
        public bool IsSelect = false;
        // 덱에서 최저 레벨 카드인지 (강조 이펙트)
        public bool IsMinCard = false;
        // 카드 GDID (0 이면 빈 슬롯)
        public int CardID = 0;
        // LevelLink 레벨 (1 이상이면 일반 배경, 0 이하면 비활성 배경). 덱 목록에서는 넘기지 않아 기본값 0
        public int LevelLinkLevel = 0;
        // 카드 UI 에 표시할 LevelLink 레벨 값
        public int CardLevelLinkLevel = 0;
        // true: 카드 원래 레벨 표시 / false: LevelLink 레벨 표시
        public bool IsOrigLevel = true;

        public bool IsNotSelected = false; //더이상 선택 할 수 없을 때
        // 슬롯의 멤버 조건 (기본값 = 그룹 전체 → 멤버 이름 숨김)
        public MEMBER_TYPE MemberType = MEMBER_TYPE.BTS;

        // 카드 클릭 콜백 (인자: CardID)
        public Action<int> OnClickCardEvent;
 

    }
}
