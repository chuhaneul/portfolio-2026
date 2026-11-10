using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

/// <summary>
/// 덱 편집 팝업 중단의 정렬 영역
/// - 부모 : ObjLevelLinkDeck_MidUI
/// - 정렬 드롭다운 : 선택한 인덱스를 LevelLinkManager.CurrentDeckSortType(static ReactiveProperty)에 넣는다
///   → PopupLevelLinkDeckManage 가 이 값을 구독하고 있다가 카드 리스트를 다시 정렬해 갱신한다
/// - 필터 버튼(BtnSort) : Setting.OnClickEventSort 로 부모에 알려 정렬 필터 패널을 연다
/// </summary>
public class ObjLevelLinkDeck_MidUI_Sort : BaseUILayout<ObjLevelLinkDeck_MidUI_Sort.Setting>
{
    //정렬 필터 패널 열기 버튼 (이름은 Sort 지만 필터 패널을 연다)
    [SerializeField] private Button BtnSort;
    //정렬 기준 드롭다운 (레벨 / 등급 / 전투력 / 이름 / 최근 획득)
    [SerializeField] private Dropdown dropdown_menu;


    /// <summary>
    /// 버튼 바인딩 및 드롭다운 구성 (부모 InitUI 에서 1회)
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( BtnSort != null )
        {
            //클릭 → 부모 콜백(ObjLevelLinkDeck_MidUI.OnClickEventSort) 후 BUTTONDELAYTIME 동안 재클릭 방지
            BtnSort.BindToOnClick(_=>
            {
                if( setting.OnClickEventSort != null )
                {
                    setting.OnClickEventSort.Invoke();
                }
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsSingleUnitObservable();

            }).AddTo(this);
        }

        InitDropDown();
    }

    public override void SetUI()
    {
        base.SetUI();
    }

    /// <summary>
    /// 드롭다운 옵션을 LevelLinkDeckSort.List 순서대로 만들고 선택 변경을 구독한다
    /// </summary>
    // [참고] options 리스트를 직접 Clear / Add 한 뒤 RefreshShownValue() 를 호출하지 않아, 첫 선택 전까지 프리팹에 저장된 캡션 텍스트가 보일 수 있다
    private void InitDropDown()
    {
        // dropdown_rect.rotation = Quaternion.Euler(0, 0, 0);
        //프리팹에 들어 있던 옵션 제거
        dropdown_menu.options.Clear();

        //정렬 목록 순서대로 드롭다운 구성 (인덱스 = CurrentDeckSortType 값)
        foreach (var sort in LevelLinkDeckSort.List)
        {
            Dropdown.OptionData op = new Dropdown.OptionData();
            //정렬 전략의 문자열 키(StrKey)로 표시 이름을 가져온다
            op.text = CStringDataManager.Instance.GetStringData(sort.StrKey);
            dropdown_menu.options.Add(op);
        }

        //드롭다운 선택이 바뀔 때마다 선택 인덱스(res)가 흘러온다
        //→ CurrentDeckSortType 에 대입 → PopupLevelLinkDeckManage 의 구독(DistinctUntilChanged.Skip(1))이 받아 리스트 재정렬
        //AddTo(this) : 이 오브젝트 파괴 시 구독 해제
        dropdown_menu.onValueChanged.AsObservable().
        Subscribe(res=>
        {
            CDebug.Log($"LevelLink 드롭다운 메뉴 선택 - {res}");
            LevelLinkManager.CurrentDeckSortType.Value = res;
            
        }).AddTo(this);        
    }
    /// <summary>
    /// 정렬 영역 입력 데이터
    /// </summary>
    public class Setting
    {
        //필터 버튼 클릭 콜백 (ObjLevelLinkDeck_MidUI.OnClickEventSort → 필터 패널 열기)
        public Action OnClickEventSort;

    }
}
