using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;
using System.Linq;

/// <summary>
/// 구매 팝업(PopupLevelLinkBuy)에 넘기는 재료 1종의 정보
/// PopupLevelLinkUnlock.GetBuyItemDataList() 에서 슬롯 확장 재료 테이블(level_link_extend_list) 기준으로 만든다
/// </summary>
public class CBuyItemData
{
    //보상 타입 (RW_ITEM = 아이템, RW_PRODUCT = 상품). 아이템 정보 조회 / 보유 수 조회에 사용
    public REWARD_TYPE RWType;
    //아이템(또는 상품) ID
    public int ItemID;
    //슬롯 확장에 필요한 총 개수
    public int NeedCount;
    
    //이미 필요한 만큼 보유하고 있으면 0 이하가 된다 (가격 계산에서 제외됨)
    public int BuyCount; // 구매할 갯수 NeedCount - InventoryItemCount;
    //1개당 가격. 테이블의 비용 타입이 캐시(COST_CASH)가 아니면 0
    public int GemCost; // 젬 구매 가격
}

/// <summary>
/// LevelLink 슬롯 확장 재료 부족분 구매 팝업 (젬으로 구매)
/// - 여는 곳 : PopupLevelLinkUnlock.OnClickBuyButton → 상태가 MATERIAL(재료 부족)일 때 OpenShopItemPopup()
/// - Setting : 재료 목록(BuyList). 각 항목에 필요 개수, 부족 개수(BuyCount), 1개당 젬 가격(GemCost)이 들어 있다
/// - 화면 : 재료 아이콘 / 보유 수 / 필요 수 목록 + 총 젬 가격 구매 버튼
/// - Result : 구매 버튼(젬 충분) → IsSuccess = true, BuyList, TotalCost(총 젬 가격)
///            닫기 / 딤 / 백키 → IsSuccess = false (TotalCost 는 SetBuyBtn 에서 이미 계산된 값이 들어 있음)
/// - 호출측 흐름 : IsSuccess → LevelLinkManager.OpenPopupUseCash(TotalCost) (젬 사용 확인)
///                → 확인 시 RequestItemBuy() (재료 구매 API) → 성공 시 언락 팝업 UI 갱신
/// - 이 팝업 자체는 서버 통신을 하지 않는다. 젬이 부족하면 구매 버튼이 젬 상점을 연다
/// </summary>
public class PopupLevelLinkBuy : CPopupUI<PopupLevelLinkBuy.Setting, PopupLevelLinkBuy.Result>
{
    //구매 버튼 (가격 표시. Disable 상태여도 클릭 콜백이 오며 현재 상태가 인자로 전달된다)
    [SerializeField] private ButtonSetting BtnBuy;
    //닫기 버튼
    [SerializeField] private Button BtnClose;
    //바깥 딤 영역 버튼 (닫기)
    [SerializeField] private Button BtnDim;

    //재료 표시 슬롯 UI 목록 (BuyList 개수보다 많은 슬롯은 숨김)
    [SerializeField] private List<CItemInfoUI> ItemUIList;

    //버튼 바인딩 중복 방지
    private bool IsInit = false;

    /// <summary>
    /// 팝업 생성 시 1회 : 결과 Subject / 기본 결과(실패) 생성, DISABLE_HIDE 설정, 버튼 바인딩
    /// </summary>
    protected override void OnAwake()
    {
        base.OnAwake();

        subject = new Subject<Result>();
        result = new Result {IsSuccess = false};

        //DISABLE_HIDE : 위에 젬 상점 등 다른 팝업이 열려도 숨김 연출 없이 그대로 둔다
        Flags = PopupFlags.DISABLE_HIDE;

        InitUI();

    }

    /// <summary>
    /// 호출측(PopupLevelLinkUnlock)이 Setting 전달 시 호출 → 재료 목록 / 구매 버튼 표시
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);
        SetUI();
    }

    /// <summary>
    /// 재료 목록과 구매 버튼(총 가격, 활성 여부) 갱신
    /// - 호출 : SetData, 젬 상점 팝업이 닫힌 뒤(OpenPopupGemShop)
    /// </summary>
    private void SetUI()
    {
        SetItemListUI();
        SetBuyBtn();

    }

    /// <summary>
    /// 버튼 바인딩 (OnAwake 에서 1회)
    /// </summary>
    private void InitUI()
    {
        if( IsInit) return;
        IsInit = true;

        //닫기 / 딤 : result 기본값(IsSuccess = false)으로 닫는다. BUTTONDELAYTIME 동안 재클릭 방지
        if( BtnClose != null )
        {
            BtnClose.BindToOnClick(_=>
            {
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnDim != null )
        {
            BtnDim.BindToOnClick(_=>
            {
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }
        if( BtnBuy != null )
        {
            BtnBuy.Init();
            //클릭 시 OnClickBuyButton(현재 버튼 상태) 호출
            BtnBuy.SetOnClickButton = OnClickBuyButton;
        }


    }

    /// <summary>
    /// 재료 슬롯 UI 를 BuyList 순서대로 채운다. BuyList 보다 많은 슬롯은 숨긴다
    /// </summary>
    private void SetItemListUI()
    {
        //(인덱스, UI) 형태로 순회하는 확장 ForEach
        ItemUIList.ForEach((idx, ui)=>
        {
            ui.InitUI();

            //표시할 재료가 없는 슬롯은 숨김
            if( setting.BuyList.Count <= idx)
            {
                ui.SetEnable(false);
            }
            else
            {
                var data = setting.BuyList[idx];
                //보상 타입에 맞는 테이블 데이터(ItemData 또는 ProductData)를 object 로 받는다
                object itemData = CRewardTypeManager.GetRewardData(data.RWType, data.ItemID);
                //현재 보유 개수
                int currentCount =  data.RWType.GetRewardCount(data.ItemID);

                ui.SetEnable(true);
                //SetItemData 의 (보유 수, 필요 수)로 개수 텍스트가 세팅된다
                //개수 텍스트 타입을 TWO 로 지정 (CItemInfoUI.TxtrType 정의 주석 기준 '두자리 수')
                ui.SetTextType(CItemInfoUI.TxtrType.TWO);
                
                //타입별로 캐스팅해 아이콘 / 이름 / 보유 수 / 필요 수 세팅 (그 외 타입은 처리하지 않음)
                switch(data.RWType)
                {
                    case REWARD_TYPE.RW_ITEM:
                        ui.SetItemData(itemData as ItemData, currentCount, data.RWType, data.NeedCount);
                    break;
                    case REWARD_TYPE.RW_PRODUCT:
                        ui.SetItemData(itemData as ProductData, currentCount, data.RWType, data.NeedCount);
                    break;  
                }
            }

        });
    }

    /// <summary>
    /// 총 구매 가격 계산 및 구매 버튼 상태 설정
    /// - 총 가격 = 부족분이 있는 재료(BuyCount > 0)의 (부족 개수 x 1개당 젬 가격) 합
    /// - 보유 젬 < 총 가격 → 가격을 빨간색으로 표시하고 버튼 Disable (클릭 시 젬 상점)
    /// - 그 외 → 가격 표시, 버튼 활성
    /// </summary>
    private void SetBuyBtn()
    {
        //totalCount 는 개수가 아니라 총 젬 가격
        int totalCount = 0;
        //이미 충분히 가진 재료(BuyCount <= 0)는 합계에서 제외
        totalCount = setting.BuyList.Where(d => d.BuyCount > 0).Sum(d=>d.BuyCount * d.GemCost);
        //결과에 미리 기록 (호출측이 젬 사용 확인 팝업에 사용)
        result.TotalCost = totalCount;

        //사용되지 않는 변수
        string costText = totalCount.ToString();


        //보유 젬이 부족한 경우
        if( CPlayer.Instance.Gem.Value < totalCount )
        {
            //빨간색 + 천 단위 콤마 형식
            // [참고] 형식 문자열 "{0: #,0}" 의 콜론 뒤 공백도 출력 형식에 포함되어, 숫자 앞에 공백이 하나 붙는다
            string strFormat = "<color=#ff0000>{0: #,0}</color>";
            BtnBuy.SetPrice(string.Format(strFormat, totalCount));
            BtnBuy.SetState(Button_State.Disable);
        }
        else
        {
            BtnBuy.SetPrice(totalCount);
            BtnBuy.SetState(Button_State.None);
        }

    }

    /// <summary>
    /// 구매 버튼 클릭
    /// - 활성(None, 젬 충분) : 결과를 성공으로 채우고 닫는다 → 실제 구매(젬 사용 확인 + API)는 호출측이 진행
    /// - Disable(젬 부족) : 젬 상점 팝업을 연다
    /// </summary>
    private void OnClickBuyButton(Button_State state)
    {
        if( state == Button_State.None)
        {
            //호출측(PopupLevelLinkUnlock.OpenShopItemPopup)은 IsSuccess 와 TotalCost 를 사용한다
            result.IsSuccess = true;
            result.BuyList = setting.BuyList;
            Close();
        }
        else
        {
            //젬 구매 팝업
            OpenPopupGemShop();
        }
    }

    /// <summary>
    /// 젬 상점 팝업(젬 카테고리)을 열고, 닫히면 보유 젬 기준으로 구매 버튼 상태를 다시 계산한다
    /// - 이 구매 팝업은 닫지 않고 그대로 유지된다
    /// </summary>
    private void OpenPopupGemShop()
    {
        //젬 샵 이동
        var popup = CCoreServices.GetCoreService<CPopupService>().OpenPopupHudShop(GOODS_CATEGORY_TYPE.SHOP_CATEGORY_GEM);

        //젬 상점이 닫히며 결과를 한 번 내보내면 UI 갱신 후 직접 Dispose
        //AddTo(this) : 그 전에 이 팝업이 파괴되면 구독도 함께 해제
        SingleAssignmentDisposable dispose = new SingleAssignmentDisposable();
        dispose.Disposable = popup.ShowAsObservable()
        .Subscribe(res=>
        {
            //UI 갱신
            SetUI();
            dispose.Dispose();

        }).AddTo(this);
    }


    /// <summary>
    /// 구매 팝업 입력 데이터 (PopupLevelLinkUnlock.OpenShopItemPopup 에서 생성)
    /// </summary>
    public class Setting
    {
        //슬롯 확장 재료 목록 (부족하지 않은 재료도 포함, BuyCount <= 0 이면 가격 계산에서 제외)
        public List<CBuyItemData> BuyList;
    }

    /// <summary>
    /// 구매 팝업 결과
    /// </summary>
    public class Result
    {
        //구매 진행 여부 (젬 충분 상태에서 구매 버튼을 누른 경우 true)
        public bool IsSuccess;
        //구매 대상 재료 목록 (setting.BuyList 그대로. 호출측에서는 현재 사용하지 않음)
        public List<CBuyItemData> BuyList;
        //총 젬 가격 → 호출측이 OpenPopupUseCash(TotalCost) 에 사용
        public int TotalCost = 0;

    }

}
