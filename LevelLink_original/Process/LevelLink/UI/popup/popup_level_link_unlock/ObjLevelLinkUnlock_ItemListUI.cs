using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LevelLink 슬롯 해금 팝업(PopupLevelLinkUnlock)의 "필요 재료 목록" 영역.
/// - 부모: PopupLevelLinkUnlock (InitUI / SetData / SetUI / SetEnable을 부모가 호출)
/// - 자식: 프리팹에 미리 배치된 CItemInfoUI 슬롯들. 재료 개수만큼 앞에서부터 켜고 나머지는 끈다.
/// - Setting: 테이블의 재료 리스트(LevelLinkExtend: 타입/ID/필요 수량)
/// - 필요 재료가 0개면 부모가 이 영역 자체를 SetEnable(false)로 숨긴다.
/// </summary>
public class ObjLevelLinkUnlock_ItemListUI : BaseUILayout<ObjLevelLinkUnlock_ItemListUI.Setting>
{
    // 재료 아이콘/수량을 표시하는 고정 슬롯 UI 목록 (재료 수가 슬롯 수보다 많으면 초과분은 표시되지 않음)
    [SerializeField] private List<CItemInfoUI> ItemInfoUIs;

    // InitUI 중복 실행 방지
    private bool IsInit = false;

    /// <summary>
    /// 부모 팝업의 InitUI(OnAwake 시점)에서 1회 호출. 모든 슬롯을 초기화하고 꺼둔다.
    /// </summary>
    public override void InitUI()
    {
        if( IsInit ) return;
        IsInit = true;

        ItemInfoUIs.ForEach(ui=>
        {
            ui.InitUI();
            ui.SetEnable(false);
        });

    }

    /// <summary>
    /// 재료 목록을 슬롯에 표시한다. 부모 팝업의 SetUI에서 호출(SetData 이후, 상태 변경/재료 구매 성공 시에도 재호출).
    /// 슬롯마다 "보유 수량 / 필요 수량" 형식(TxtrType.TWO)으로 표시한다.
    /// </summary>
    public override void SetUI()
    {
        // ForEach((idx, ui)): 프로젝트 확장 메서드(Utility.ForEach) — 인덱스와 요소를 함께 넘겨준다
        ItemInfoUIs.ForEach((idx, ui)=>
        {
            // 이 슬롯 인덱스에 해당하는 재료가 있으면 표시
            if( setting.ItemList.Count > idx )
            {
                var itemData = setting.ItemList[idx];

                // 테이블 재료 타입 → 보상 타입 변환
                REWARD_TYPE rwType = itemData.level_link_extend_type.ConvertToRewardType();

                // 아이콘/이름용 테이블 데이터와 현재 인벤토리 보유 수량
                object rwData = CRewardTypeManager.GetRewardData(rwType, itemData.level_link_extend_id);
                int invenCount = rwType.GetRewardCount(itemData.level_link_extend_id);

                // 보상 타입에 따라 ItemData / ProductData로 캐스팅해 넘긴다 (마지막 인자는 필요 수량)
                // 아이템/상품 외의 타입이면 데이터 세팅 없이 슬롯만 켜진다
                switch(rwType)
                {
                    case REWARD_TYPE.RW_ITEM:
                    {
                        ui.SetTextType(CItemInfoUI.TxtrType.TWO);
                        ui.SetItemData(rwData as ItemData, invenCount, rwType, itemData.level_link_extend_value);
                    }
                    break;
                    case REWARD_TYPE.RW_PRODUCT:
                    {
                        ui.SetTextType(CItemInfoUI.TxtrType.TWO);
                        ui.SetItemData(rwData as ProductData, invenCount, rwType, itemData.level_link_extend_value);
                    }
                    break;
                }

                ui.SetEnable(true);
            }
            // 재료 수보다 뒤에 있는 슬롯은 끈다
            else
            {
                ui.SetEnable(false);
            }

        });

    }

    /// <summary>
    /// 재료 목록 영역 입력 데이터.
    /// </summary>
    public class Setting
    {
        // 해금에 필요한 재료 리스트 (PopupLevelLinkUnlock이 setting.TLevelLinkData.level_link_extend_list를 그대로 넘김)
        public List<LevelLinkExtend> ItemList;

    }
}
