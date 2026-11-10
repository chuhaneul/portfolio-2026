using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 페이지/팝업 안의 "하위 영역(서브 레이아웃)" 이 공통으로 상속하는 베이스 클래스.
/// (예: PageLevelLink_Top / PageLevelLink_Mid / PageLevelLink_Bottom, 팝업의 TopUI / MidUI 등)
///
/// [TSetting]
///  - 각 서브 레이아웃이 자기 안에 중첩 클래스로 정의하는 "Setting" 타입. (예: PageLevelLink_Mid.Setting)
///  - 부모(페이지/팝업/상위 레이아웃)가 new Setting { ... } 으로 값을 채워 SetData 로 넘겨준다.
///  - 화면에 표시할 값(데이터)뿐 아니라, 버튼 클릭 등 이벤트를 부모에게 돌려보내기 위한 Action 콜백도 함께 담는다.
///    → 서브 레이아웃은 부모를 직접 참조하지 않고, Setting 안의 Action 을 호출해서 부모에게 알린다.
///
/// [수명주기 계약 (부모가 실제로 호출하는 순서)]
///  1) InitUI()            : 최초 1회. 버튼 바인딩, 자식 레이아웃의 InitUI 호출 등 1회성 초기화.
///  2) SetData(new Setting{...}) : 표시할 값과 콜백을 전달. 이 베이스 구현은 setting 필드에 저장만 한다.
///  3) SetUI()             : 저장된 setting 을 화면에 그린다. (상위 레이아웃이면 여기서 자식용 Setting 을 만들어 자식 SetData/SetUI 를 호출하기도 한다)
///  4) 갱신 시 UpdateUI() / UpdateData(...) : 일부만 다시 그리거나 데이터를 부분 갱신할 때 하위 클래스가 재정의해서 사용한다.
///     (예: LevelLink 하단 스크롤은 처음 SetUI 로 아이템 개수를 잡고, 이후에는 UpdateUI 로 보이는 아이템만 새로고침한다)
///  5) SetEnable(bool)     : GameObject 표시/숨김.
///  6) Dispose()           : 정리가 필요한 하위 클래스가 재정의한다. 베이스 구현은 아무 것도 하지 않는다.
///  ※ 이 메서드들은 Unity 메시지(Awake/Start)가 아니므로 자동으로 호출되지 않는다. 반드시 부모가 직접 호출한다.
///  ※ 베이스의 InitUI / SetUI / UpdateUI / UpdateData / Dispose 는 모두 빈 virtual 메서드다.
/// </summary>
public class BaseUILayout<TSetting> : MonoBehaviour
{
    /// <summary>
    /// 부모가 SetData 로 넘겨준 최신 Setting. 하위 클래스는 SetUI 등에서 이 값을 읽어 화면을 그린다.
    /// SetData 가 호출되기 전에는 TSetting 의 기본값(클래스면 null)이다.
    /// </summary>
    protected TSetting setting;
    /// <summary>
    /// setting 의 읽기 전용 공개 접근자. 외부(부모 등)에서 현재 레이아웃이 들고 있는 Setting 을 조회할 때 사용한다.
    /// </summary>
    public TSetting SettingData => setting;

    /// <summary>
    /// 마지막으로 SetEnable 에 전달된 값. (SetEnable 을 통해서만 바뀌며, 다른 경로로 SetActive 한 경우는 반영되지 않는다)
    /// SetEnable 이 한 번도 호출되지 않았다면 false 다.
    /// </summary>
    public bool IsEnable {get; private set;}

    /// <summary>
    /// 1회성 초기화 지점. 부모가 자기 초기화 시점에 한 번 호출한다. (베이스 구현: 비어 있음)
    /// </summary>
    public virtual void InitUI(){}

    /// <summary>
    /// 현재 setting 값으로 화면을 그린다. 보통 SetData 직후 부모가 호출한다. (베이스 구현: 비어 있음)
    /// </summary>
    public virtual void SetUI(){}

    /// <summary>
    /// 화면 갱신용 훅. 전체를 다시 그리는 SetUI 와 구분해서, 부분 새로고침이 필요할 때 하위 클래스가 재정의한다. (베이스 구현: 비어 있음)
    /// </summary>
    public virtual void UpdateUI()
    {

    }

    /// <summary>
    /// 부모로부터 Setting 을 받아 setting 필드에 저장한다. 그리기는 하지 않는다. (그리기는 SetUI 에서)
    /// 하위 클래스가 재정의할 때는 base.SetData(data) 를 호출해야 setting 이 갱신된다.
    /// </summary>
    public virtual void SetData(TSetting data)
    {
        setting = data;
    }

    /// <summary>
    /// 데이터 부분 갱신용 훅. 베이스 구현은 비어 있으며 setting 도 바꾸지 않는다.
    /// (SetData 와 달리 저장 동작이 없으므로, 필요하면 하위 클래스가 직접 처리한다)
    /// </summary>
    public virtual void UpdateData(TSetting data)
    {

    }

    /// <summary>
    /// 레이아웃 표시/숨김. IsEnable 값을 기록하고 이 GameObject 의 활성 상태를 같은 값으로 바꾼다.
    /// </summary>
    public virtual void SetEnable(bool isEnable)
    {
        // 상태 기록
        IsEnable = isEnable;
        // 실제 GameObject 활성/비활성 (비활성화되면 이 오브젝트의 OnDisable 이 호출된다)
        this.gameObject.SetActive(isEnable);
    }


    /// <summary>
    /// 정리용 훅. 타이머/구독 등 해제할 것이 있는 하위 클래스가 재정의한다. (베이스 구현: 비어 있음)
    /// </summary>
    public virtual void Dispose(){}

}

 
