using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;


/// <summary>
/// 쿨타임 단축 팝업의 중간(남은 시간) 영역용으로 만들어진 클래스.
/// 현재는 내용이 비어 있는 뼈대이며, 프로젝트 코드에서 이 타입을 참조하는 곳이 없다.
/// 실제 팝업(PopupLevelLinkInstantcomplete)의 MidUI 필드는 공용 타이머 UI인 ObjLevelLinkSlot_Timer를 사용한다.
/// </summary>
public class ObjLevelLinkInstantcomplete_Mid : BaseUILayout<ObjLevelLinkInstantcomplete_Mid.Setting>
{
    // 남은 시간 진행 바 (코드에서 사용하지 않음)
    [SerializeField] private Slider SliderUI;
    // 남은 시간 텍스트 (코드에서 사용하지 않음)
    [SerializeField] private Text TxtTime;

    
    /// <summary>
    /// base 호출만 있다.
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();
    }

    /// <summary>
    /// base 호출만 있다.
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();
    }

    /// <summary>
    /// 비어 있는 입력 데이터 클래스.
    /// </summary>
    public class Setting
    {

    }
}
