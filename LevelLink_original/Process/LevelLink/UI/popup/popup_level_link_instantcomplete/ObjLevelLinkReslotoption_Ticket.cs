using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using Game.RestAPI;
using UniRx;

/// <summary>
/// 쿨타임 단축 옵션 아이템의 "단축 티켓 사용" 하위 UI.
/// - 부모: ObjLevelLinkReslotoption (옵션 타입이 TICKET일 때만 SetUI + 켜짐, 아니면 꺼짐)
/// - 티켓 이름/아이콘/보유 개수를 표시하고, 버튼 클릭 시 확인 팝업 없이 바로 부모 콜백 → 팝업에서 티켓 단축 API 호출.
/// - 티켓 종류는 게임 설정 "sng_time_reduction1_id" / "2_id" / "3_id"에 지정된 아이템 3종이며,
///   보유 개수가 1개 이상인 티켓만 목록에 들어간다(LevelLinkManager.GetShortTimeTicket).
/// </summary>
public class ObjLevelLinkReslotoption_Ticket : BaseUILayout<ObjLevelLinkReslotoption_Ticket.Setting>
{
    // 티켓 이름
    [SerializeField] private Text TxtTitle;
    // 보유 개수 텍스트
    [SerializeField] private Text TxtCnt;
    // 티켓 아이콘
    [SerializeField] private Image ImgIcon;

    // 티켓 사용 버튼
    [SerializeField] private Button BtnClick; //OnClickInstantComplete

    /// <summary>
    /// 부모 InitUI에서 호출. 사용 버튼 클릭 바인딩 (BUTTONDELAYTIME 동안 연타 방지).
    /// IsInit 가드가 없지만, 스크롤 아이템 생성 시 1회만 InitUI가 불리도록 상위에서 보장한다.
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( BtnClick != null )
        {
            BtnClick.BindToOnClick(_=>
            {
                OnClickEvent();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }
    }

    /// <summary>
    /// 보유 티켓 데이터가 없으면 숨기고, 있으면 이름/아이콘/개수를 표시한다.
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();

        if( setting.STicketData == null )
        {
            SetEnable(false);
            return;
        }

        SetEnable(true);

        SetTitle();
        SetImage();
        SetCount();
    }

    /// <summary>
    /// 아이템 테이블의 이름 문자열 키(item_name_string)로 티켓 이름 표시.
    /// </summary>
    private void SetTitle()
    {
        if( TxtTitle == null) return;
        TxtTitle.text = CStringDataManager.Instance.GetStringData(setting.TData.item_name_string);
    }

    /// <summary>
    /// 아이템 테이블의 리소스 경로(item_resource_image)로 아이콘 로드.
    /// </summary>
    private void SetImage()
    {
        if( ImgIcon == null) return;
        CResourceManager.Instance.LoadImage(setting.TData.item_resource_image, ImgIcon);
    }

    /// <summary>
    /// "보유 개수" 문자열(UI_common_stock_count)에 서버 인벤토리 보유 수량을 넣어 표시.
    /// </summary>
    private void SetCount()
    {
        if( TxtCnt == null) return;

        TxtCnt.text = string.Format(CStringDataManager.Instance.GetStringData("UI_common_stock_count"), setting.TicketCount);
    }

    /// <summary>
    /// 사용 버튼 클릭 → 부모 콜백(ObjLevelLinkReslotoption.OnClickEvent()) 호출.
    /// </summary>
    private void OnClickEvent()
    {
        if( setting.OnClickCompete != null )
        {
            setting.OnClickCompete.Invoke();
        }
    }


    /// <summary>
    /// 티켓 옵션 입력 데이터. ObjLevelLinkReslotoption.SetUIDataTicket에서 생성된다.
    /// </summary>
    public class Setting
    {
        // 서버 인벤토리의 티켓 아이템 데이터 (gdid = 아이템 ID, count = 보유 수량)
        public ItemList STicketData{get; private set;}
        // 티켓 아이템 테이블 데이터 (이름 키, 아이콘 경로) — 생성자에서 STicketData.gdid로 조회
        public ItemData TData {get; private set;}
        // 보유 수량
        public int TicketCount => STicketData.count;

        // 사용 버튼 클릭 시 호출할 콜백 (이름의 Compete는 Complete의 오타로 보임)
        public Action OnClickCompete;

        public Setting(ItemList ticketData, Action onClickCompete)
        {
            STicketData = ticketData;
            OnClickCompete = onClickCompete;

            // 서버 데이터가 있을 때만 아이템 테이블 데이터 조회
            if( STicketData != null )
            {
                TData = CItemDataManager.Instance.GetItemData(STicketData.gdid);
            }

        }


    }
}
