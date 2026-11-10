using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

/// <summary>
/// LevelLink 중단 영역의 가운데 부분 - LevelLink 레벨 텍스트와 초기화 / 자동 배치 버튼.
/// - 부모: PageLevelLink_Mid (MidMidUI 필드). 자식 레이아웃 없음.
/// - Setting(struct) 은 PageLevelLink_Mid.SetUI 에서 LevelLink 레벨과 두 버튼 콜백을 담아 채운다.
/// </summary>
public class PageLevelLink_Mid_MidUI : BaseUILayout<PageLevelLink_Mid_MidUI.Setting>
{
    // 초기화 버튼. 공용 ButtonSetting 컴포넌트로 None/Disable 상태를 가지며, Disable 이어도 클릭 콜백은 상태값과 함께 호출된다
    [SerializeField] private ButtonSetting BtnReset;
    // 자동 배치 버튼
    [SerializeField] private Button BtnAuto;
    // "LevelLink 레벨 {0}" 텍스트
    [SerializeField] private Text TxtLevel;


    /// <summary>
    /// 버튼 초기화/바인딩 (1회). PageLevelLink_Mid.InitUI 에서 호출.
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( BtnReset != null )
        {
            // ButtonSetting.Init: 내부 버튼 BindToOnClick + 상태(ReactiveProperty) 변경 시 UI 갱신 구독
            BtnReset.Init();
            // 클릭 시 현재 버튼 상태(Button_State)를 인자로 OnClickReset 이 호출된다
            BtnReset.SetOnClickButton = OnClickReset;
        }

        if( BtnAuto != null )
        {
            // 클릭 시 자동 배치 콜백 실행, BUTTONDELAYTIME 동안 연타 방지. AddTo(this) 로 파괴 시 해제
            BtnAuto.BindToOnClick(_=>
            {
                if( setting.OnClickAuto != null )
                {
                    setting.OnClickAuto.Invoke();
                }

                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }
    }

    /// <summary>
    /// LevelLink 레벨 텍스트 표시 + 초기화 버튼 상태 설정. PageLevelLink_Mid.SetUI 에서 SetData 직후 호출.
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();

        // 스트링 테이블 "levellink_level" 포맷에 LevelLink 레벨을 넣어 표시
        if( TxtLevel != null )
        {
            TxtLevel.text = string.Format(CStringDataManager.Instance.GetStringData("levellink_level"), setting.LevelLinkLV.ToString());
        }

        // LevelLink 레벨이 있으면(덱 완성) 초기화 가능 → None, 레벨 0 이면 초기화할 것이 없으므로 Disable 표시
        if( BtnReset != null )
        {
            if( setting.LevelLinkLV > 0 )
            {
                BtnReset.SetState(Button_State.None);
            }
            else
            {
                BtnReset.SetState(Button_State.Disable);
            }
        }
    }

    /// <summary>
    /// 초기화 버튼 클릭 콜백 (ButtonSetting 에서 호출).
    /// 버튼이 Disable 상태여도 클릭은 들어오므로, None(활성) 상태일 때만 상위 콜백(PageLevelLink.OnClickReset)을 호출한다.
    /// </summary>
    private void OnClickReset(Button_State state)
    {
        if( state == Button_State.None)
        {
            if( setting.OnClickReset != null )
            {
                setting.OnClickReset.Invoke();
            }
        }
    }


    /// <summary>
    /// 중단-중단 데이터 (값 타입 struct). PageLevelLink_Mid.SetUI 에서 생성.
    /// struct 라서 SetData 전에는 기본값(레벨 0, 콜백 null)이며, 버튼 핸들러는 콜백 null 체크 후 호출한다.
    /// </summary>
    public struct Setting
    {
        // LevelLink 레벨 (텍스트 표시 + 초기화 버튼 활성 조건)
        public int LevelLinkLV;
        // 초기화 클릭 (PageLevelLink.OnClickReset → 초기화 확인 팝업)
        public Action OnClickReset;
        // 자동 배치 클릭 (PageLevelLink.OnClickAuto → 자동 배치 확인 팝업)
        public Action OnClickAuto;
    }
}
