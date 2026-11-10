using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UnityEngine.UI;

/// <summary>
/// LevelLink 페이지의 상단 영역 (뒤로가기 / 도움말 버튼).
/// - 부모: PageLevelLink (TopUI 필드). 자식 레이아웃 없음.
/// - Setting 은 PageLevelLink.SetData_TopUI 에서 채워진다 (뒤로가기 콜백만 전달).
/// - 생명주기: InitUI(버튼 바인딩, PageLevelLink.InItUI 에서 1회) → SetData → SetUI(현재는 할 일 없음)
/// </summary>
public class PageLevelLink_Top : BaseUILayout<PageLevelLink_Top.Setting>
{
    // 뒤로가기 버튼 → setting.OnClickBackEvent (= PageLevelLink.OnClickBack → ClosePage)
    [SerializeField] private Button BtnBack;
    // 도움말 버튼 → LEVELLINK 도움말 웹뷰
    [SerializeField] private Button BtnHelp;

    /// <summary>
    /// 버튼 바인딩. 클릭 후 BUTTONDELAYTIME 동안 버튼을 비활성화해 연타를 막는다. 바인딩은 AddTo(this) 로 파괴 시 해제.
    /// </summary>
    public override void InitUI()
    {
        if( BtnBack != null )
        {
            BtnBack.BindToOnClick(_=>
            {
                // 클릭 시점의 setting 을 참조하므로, InitUI 이후 SetData 로 들어온 콜백이 실행된다
                setting.OnClickBackEvent?.Invoke();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnHelp != null )
        {
            BtnHelp.BindToOnClick(_=>
            {
                //도움말
                // 도움말 테이블에서 "LEVELLINK" 키를 찾아 웹뷰로 연다 (링크가 없으면 HelpManager 가 안내 메시지 표시)
                HelpManager.Instance.ShowHelpWebview(HELP_TAG.LEVELLINK.ToString());
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }
        
    }

    /// <summary>
    /// 상단은 데이터에 따라 바뀌는 표시가 없어서 비어 있다. (PageLevelLink.SetUI 에서 호출됨)
    /// </summary>
    public override void SetUI()
    {
        
    }

    /// <summary>
    /// 상단 영역 데이터. PageLevelLink.SetData_TopUI 에서 생성.
    /// </summary>
    public class Setting
    {
        // 뒤로가기 클릭 콜백 (PageLevelLink.OnClickBack)
        public Action OnClickBackEvent;
    }
    
}
