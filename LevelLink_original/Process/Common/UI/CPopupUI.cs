using System;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using UniRx;
using SNG;
using Game.RestAPI;

/// <summary>
/// 팝업 동작 옵션 비트 플래그. ([Flags] 이므로 | 로 조합 가능, 판정은 BasePopup<T>.HasFlag)
/// BasePopup<T>.Awake 에서 NORMAL 로 초기화되고, 하위 클래스가 OnAwake 에서 Flags = ... 로 덮어쓴다.
/// (예: LevelLink 팝업들은 OnAwake 에서 Flags = PopupFlags.DISABLE_HIDE 로 설정한다. 대입이므로 NORMAL 비트는 사라진다)
/// </summary>
[Flags]
public enum PopupFlags
{
    /// <summary>기본값. 이 파일 안에서 NORMAL 비트로 분기하는 코드는 없다.</summary>
    NORMAL = 1,
    /// <summary>백키(ESC/안드로이드 뒤로가기)로 닫히지 않음. OnBackPress 에서 처리하지 않고 false 를 돌려준다.</summary>
    DISABLE_BACK_PRESS = 2,
    /// <summary>이 팝업이 열리는 연출이 끝난 뒤, 아래(Prev 방향)에 쌓여 있는 다른 팝업들을 모두 CloseInHide 로 닫는다.</summary>
    CLOSE_OTHERS = 4,
    /// <summary>
    /// 위에 다른 팝업이 열려도 숨김 연출(OutToHide)을 하지 않고, 다시 돌아올 때도 InFromHide 연출을 하지 않는다.
    /// → 아래 팝업이 화면에 그대로 남아 보인다. (Status 는 그래도 HIDE / SHOW 로 바뀐다)
    /// </summary>
    DISABLE_HIDE = 8,
}

/// <summary>
/// 팝업을 어느 층에 띄울지 지정하는 값. 이 파일 안에서는 사용하지 않고,
/// 다른 팝업의 Setting(예: 아이템 정보/조건부 상품 팝업의 PopupLayerType)에서 분기용으로 쓰인다.
/// </summary>
public enum POPUP_LAYER
{
    /// <summary>일반 층</summary>
    NORMAL = 0,
    /// <summary>마지막(최상위) 층</summary>
    LAST = 1,
}

/// <summary>
/// 팝업의 현재 상태. BasePopup.Status(일반 팝업) / PagePopup.pageStatus(페이지) 에 기록된다.
/// </summary>
public enum PopupStatus
{
    /// <summary>생성 직후(Awake). 아직 ShowAsObservable 이 진행되지 않음.</summary>
    NONE,
    /// <summary>열리는 중(등장 연출 In() 직전에 설정) 또는 숨김에서 다시 보여진 상태(ShowFromHideAsObservable).</summary>
    SHOW,
    /// <summary>등장 연출과 후처리(OnAfterShow, CLOSE_OTHERS 처리, 터치 잠금 해제)가 모두 끝난 상태.</summary>
    AFTER_SHOW,
    /// <summary>이 파일 안에서는 설정되지 않는 값.</summary>
    ON_HIDE,
    /// <summary>위에 다른 팝업이 열려 숨겨진 상태(HideAsObservable). 이 상태에서 Close() 하면 연출 없이 CloseInHide 로 닫힌다.</summary>
    HIDE,
    /// <summary>이 파일 안에서는 설정되지 않는 값.</summary>
    ON_CLOSE,
    /// <summary>닫힘 연출(Out)이 끝난 상태. 직후 GameObject 가 파괴된다.</summary>
    CLOSE
}
/// <summary>
/// HUD 프로필 표시 상태. 이 파일 안에서는 사용하지 않는다.
/// </summary>
public enum HUDProfileStatus
{
    /// <summary>표시</summary>
    SHOW,
    /// <summary>숨김</summary>
    HIDE,
}

/// <summary>
/// 일반 팝업(BasePopup<T>)이 구현하는 인터페이스.
/// 열린 팝업들은 Prev/Next 로 연결된 "이중 연결 리스트" 로 관리되며, Last 는 가장 위(마지막에 열린) 팝업이다.
/// CPopupService 의 열린 팝업 목록(openPopupList)도 IPopup 타입을 키/값으로 사용한다.
/// (PagePopup 은 이 인터페이스를 구현하지 않으므로 이 리스트에 들어가지 않는다)
/// </summary>
public interface IPopup
{
    /// <summary>현재 가장 위에 있는 팝업. (BasePopup<T> 구현에서는 모든 팝업이 공유하는 static 값)</summary>
    IPopup Last { get; set; }
    /// <summary>이 팝업 바로 아래(먼저 열린) 팝업.</summary>
    IPopup Prev { get; set; }
    /// <summary>이 팝업 바로 위(나중에 열린) 팝업.</summary>
    IPopup Next { get; set; }

    /// <summary>팝업 프리팹 경로. CPopupService.Load 에서 생성 직후 설정되며, 닫을 때 목록 제거/에셋 언로드의 키로 쓰인다.</summary>
    string PrefabPath { get; set; }

    /// <summary>이 팝업을 닫는다.</summary>
    void Close();
    /// <summary>모든 팝업을 닫는다. (BasePopup<T> 구현은 실제로 Last.Close() 한 번만 호출한다)</summary>
    void CloseAllPopups();

    /// <summary>메시지를 아래(Prev) 방향 팝업들에게 전달한다.</summary>
    void DeliverMessageToPrev(IDeliverMessage message);

    /// <summary>메시지를 위(Next) 방향 팝업에게 전달한다.</summary>
    void DeliverMessageToNext(IDeliverMessage message);
}

/// <summary>
/// 팝업끼리 주고받는 메시지 형식. (IPopup.DeliverMessageToPrev / DeliverMessageToNext 로 전달)
/// </summary>
public interface IDeliverMessage
{
    /// <summary>보낸 팝업. 자기 자신이 보낸 메시지는 처리/전달하지 않는 판정에 쓰인다.</summary>
    IPopup Sender { get; }
    /// <summary>받을 팝업 타입. 이 파일 안에서는 직접 사용하지 않으며, 받는 쪽 HandleDeliveredMessage 에서 판단용으로 쓸 수 있다.</summary>
    Type ReceiverType { get; }
    /// <summary>전달할 값.</summary>
    object Value { get; }
}

/// <summary>
/// 팝업 등장/퇴장/숨김 연출 인터페이스. BasePopup<T>.transition 에 [Injection] 으로 주입된다.
/// 각 메서드는 연출이 끝나면 값을 내보내는 IObservable 을 반환하고, 팝업은 그 뒤에 다음 단계로 진행한다.
/// </summary>
public interface IPopupTransition
{
    /// <summary>팝업 GameObject 에 연출 준비(컴포넌트/초기값 등)를 한다. BasePopup<T>.Awake 에서 호출.</summary>
    void InitializeTransitionOnPopupGameObject(GameObject go);
    /// <summary>처음 열릴 때의 등장 연출. (ShowAsObservable)</summary>
    IObservable<Unit> In();
    /// <summary>닫힐 때의 퇴장 연출. (CloseAsObservable)</summary>
    IObservable<Unit> Out();
    /// <summary>숨김 상태에서 다시 보여질 때의 연출. (ShowFromHideAsObservable)</summary>
    IObservable<Unit> InFromHide();
    /// <summary>위에 새 팝업이 열려 숨겨질 때의 연출. (HideAsObservable)</summary>
    IObservable<Unit> OutToHide();
}

/// <summary>
/// 모든 팝업/페이지의 최상위 추상 베이스. CUIPopupManager(HUD/BarManager 처리) 를 상속한다.
/// - 일반 팝업: BasePopup<T> → CPopupUI<TSetting, TResult>
/// - 전체 화면 페이지: PagePopup
/// 여기에는 모든 팝업이 공유하는 static 상태(lastPopup, isInTransition)와
/// 다른 팝업이 "아래 팝업" 을 숨기고/다시 보이고/조용히 닫을 때 호출하는 추상 메서드만 정의되어 있다.
/// </summary>
public abstract class BasePopup : CUIPopupManager
{
    /// <summary>
    /// 가장 위(마지막에 열린) 일반 팝업. static 이므로 모든 BasePopup<T> 가 하나의 값을 공유한다.
    /// BasePopup<T>.Last 프로퍼티가 이 값을 읽고 쓴다.
    /// </summary>
    protected static IPopup lastPopup = null;
    /// <summary>등장/퇴장 연출 객체. BasePopup<T> 는 [Injection] 으로 주입받고, PagePopup 은 접근 시 예외를 던진다.</summary>
    protected abstract IPopupTransition transition { get; set; }

    /// <summary>
    /// 지금 어떤 팝업이 열리거나 닫히는 연출 중인지 여부. static 이므로 전역 하나다.
    /// 열기/닫기 시작 시 true, 끝나면 false. 다른 팝업의 열기/닫기는 이 값이 false 가 될 때까지 기다리며,
    /// COnBackPressService 는 이 값이 true 인 동안 백키 입력을 무시한다.
    /// </summary>
    public static bool isInTransition = false;

    /// <summary>현재 팝업 상태. (PopupStatus 참고)</summary>
    public PopupStatus Status { get; protected set; }

    /// <summary>위 팝업이 닫혀 이 팝업이 다시 보여질 때 호출된다. (CloseAsObservable 에서 새 Last 에 대해 호출)</summary>
    protected internal abstract IObservable<Unit> ShowFromHideAsObservable();

    /// <summary>위에 새 팝업이 열려 이 팝업이 숨겨질 때 호출된다. (ShowAsObservable 에서 Prev 에 대해 호출)</summary>
    protected internal abstract IObservable<Unit> HideAsObservable();

    /// <summary>숨겨진 상태에서 연출 없이 즉시 닫는다. (Close() 시 HIDE 상태이거나, CLOSE_OTHERS 처리 시 호출)</summary>
    protected internal abstract void CloseInHide();
}

/// <summary>
/// 일반 팝업의 공통 동작을 구현한 베이스. T 는 ShowAsObservable() 이 내보내는 값의 타입이다.
///
/// [전체 흐름 (CPopupUI 하위 팝업 기준)]
///  1) 생성 : CPopupService.Load<T>(PopupServiceHelper 의 OpenPopupXXX 확장 메서드가 호출) 가 프리팹을 PopupLayer 아래에
///           생성 → 이 시점에 Awake → OnAwake 가 즉시 실행된다. (하위 팝업은 여기서 subject/result/Flags 를 만든다)
///           생성 직후 PrefabPath 가 설정되고, 이어서 확장 메서드가 SetData(new Setting{...}) 를 호출한 뒤 팝업을 반환한다.
///  2) 열기 : 호출측이 popup.ShowAsObservable().Subscribe(...) 로 구독하는 순간 열기 절차가 시작된다. (구독 전에는 아무 일도 없음)
///           연결 리스트 등록 → 아래 팝업 숨김 → 백키 스택 등록 → 등장 연출 → OnShow() 가 반환한 스트림을 이어서 내보낸다.
///  3) Start: Unity 가 다음 프레임 즈음 Start 를 호출 → UI 타입 설정, OnStart, HUD(BarManager) 처리.
///  4) 닫기 : Close() 또는 백키(OnBackPress). 연출 후 GameObject 파괴, 마지막에 OnClose() → CPopupUI 가 결과를 내보낸다.
///  5) 파괴 : OnDestroy 에서 HUD 처리, 프리팹 에셋 언로드.
///
/// [공유 상태] lastPopup / isInTransition 은 static 이라 모든 팝업이 함께 쓴다.
/// </summary>
public class BasePopup<T> : BasePopup, IPopup, IOnBackPress
{
    /// <summary>가장 위 팝업. static 필드 lastPopup 을 그대로 읽고 쓴다. (어느 팝업 인스턴스에서 접근해도 같은 값)</summary>
    public IPopup Last
    {
        get => lastPopup;
        set => lastPopup = value;
    }
    /// <summary>바로 아래(먼저 열린) 팝업. AddToList 에서 설정된다.</summary>
    public IPopup Prev { get; set; }
    /// <summary>바로 위(나중에 열린) 팝업.</summary>
    public IPopup Next { get; set; }
    /// <summary>프리팹 경로. CPopupService.Load 가 생성 직후 넣어준다. Start(UI 타입), OnDestroy(언로드), 닫기(목록 제거)에서 사용.</summary>
    public string PrefabPath { get; set; }

    /// <summary>팝업 옵션 플래그. Awake 에서 NORMAL 로 초기화되며, 하위 클래스가 OnAwake 에서 바꾼다.</summary>
    public PopupFlags Flags { get; protected set; }

    /// <summary>DISABLE_BACK_PRESS 플래그 여부. true 면 OnBackPress 에서 닫지 않는다.</summary>
    protected bool IsDisableBackPress
    {
        get
        {
            return HasFlag(PopupFlags.DISABLE_BACK_PRESS);
        }
    }

    /// <summary>CLOSE_OTHERS 플래그 여부. true 면 열기 연출 후 아래 팝업들을 모두 닫는다.</summary>
    protected bool IsCloseOthers
    {
        get
        {
            return HasFlag(PopupFlags.CLOSE_OTHERS);
        }
    }

    /// <summary>
    /// 등장/퇴장 연출. Awake 의 IoCContext.Inject(this) 로 주입된다.
    /// </summary>
    [Injection]
    protected override IPopupTransition transition { get; set; }

    /// <summary>백키 스택 서비스. 열릴 때 Add, 닫힐 때 Remove.</summary>
    protected COnBackPressService backPressService;
    /// <summary>팝업 서비스. 터치 잠금(TouchLock), 열린 팝업 목록 제거(RemovePopup), 에셋 언로드(Unload)에 사용.</summary>
    protected CPopupService popupService;

    /// <summary>이 팝업의 RectTransform 캐시 (Awake 에서 GetComponent). 이 파일 안에서는 사용하지 않으며 하위 클래스용이다.</summary>
    protected RectTransform cachedTransform;
    /// <summary>static canvas 의 CanvasScaler 기준 해상도(referenceResolution). 하위 클래스용.</summary>
    protected Vector2 screenResolution;

    /// <summary>
    /// 생성 직후 닫기를 막는 최소 시간(ms). 생성(Awake) 후 이 시간이 지나기 전에는
    /// CloseAsObservable 이 아무 것도 하지 않고, IsShowState 도 false 를 반환한다. (연타 방지 목적으로 보임)
    /// </summary>
    protected double _IntervalTime = 70;         // 0.07초
    /// <summary>Awake 시각. _IntervalTime 판정의 기준.</summary>
    private DateTime _OpenTime;

    /// <summary>
    /// Unity Awake. 프리팹이 생성되는 즉시(CPopupService.Load 안에서) 실행된다.
    /// 의존성 주입 → 연출 초기화 → 서비스 캐시 → Flags/Status 초기화 → 기준 해상도 캐시 → 생성 시각 기록 → OnAwake 순서.
    /// </summary>
    private void Awake()
    {
        // [Injection] 이 붙은 멤버(transition) 주입
        IoCContext.Inject(this);
        // 연출 객체가 있으면 이 GameObject 에 연출 준비
        transition?.InitializeTransitionOnPopupGameObject(gameObject);

        // 코어 서비스 캐시
        backPressService = CCoreServices.GetCoreService<COnBackPressService>();
        popupService = CCoreServices.GetCoreService<CPopupService>();

        // 기본값. 하위 클래스는 아래 OnAwake 에서 Flags 를 덮어쓴다.
        Flags = PopupFlags.NORMAL;
        Status = PopupStatus.NONE;

        cachedTransform = GetComponent<RectTransform>();

        // static canvas 의 기준 해상도 저장
        var canvasScaler = CStaticCanvas.Instance.GetComponent<CanvasScaler>();
        screenResolution = canvasScaler.referenceResolution;

        // 닫기 최소 간격(_IntervalTime) 판정용 시각
        _OpenTime = DateTime.Now;
        // 하위 클래스 초기화 훅 (CPopupUI 하위 팝업은 여기서 subject / result 를 생성한다)
        OnAwake();
    }

    /// <summary>
    /// Unity Start. 생성 후 첫 프레임 업데이트 전에 실행된다. (보통 ShowAsObservable 구독은 이보다 먼저 이뤄진다)
    /// UI 타입을 POPUP 으로 설정 → OnStart → BarManager(HUD) 처리.
    /// </summary>
    protected void Start()
    {
        // UI Type 설정
        if (PrefabPath != null)
        {
            // CUIPopupManager 에 프리팹 키와 UI 타입(POPUP) 기록
            SetUIType(PrefabPath, UIPOPUP_TYPE.POPUP);
        }

        // 하위 클래스 시작 훅
        OnStart();

        // 현재 UI 스택 개수 기준으로 HUD 표시 상태 조정 (CUIPopupManager.StartBarManager)
        StartBarManager(UIManager.Instance.uiStack.Count());
    }

    /// <summary>
    /// Unity OnDestroy. GameObject 가 실제로 파괴될 때 HUD 상태를 되돌리고 프리팹 에셋을 언로드한다.
    /// </summary>
    protected void OnDestroy()
    {
        // 현재 UI 스택 개수 기준으로 HUD 복구 (CUIPopupManager.CloseBarManager)
        CloseBarManager(UIManager.Instance.uiStack.Count());
        if(PrefabPath != null)
        {
            // CAssetService 리소스 테이블에서 이 프리팹 제거
            popupService.Unload(PrefabPath);
        }
    }

    /// <summary>Awake 마지막에 호출되는 하위 클래스 초기화 훅. (subject/result/Flags 설정 위치)</summary>
    protected virtual void OnAwake() { }
    /// <summary>Start 에서 호출되는 하위 클래스 시작 훅.</summary>
    protected virtual void OnStart() { }

    #region IDeliverMessage
    /// <summary>
    /// 메시지를 아래(Prev) 방향으로 전달한다.
    /// 이 팝업이 보낸 사람이 아니고, HandleDeliveredMessage 가 false(처리 안 함)를 반환하면 Prev 에게 넘긴다.
    /// 누군가 true 를 반환하면 거기서 전달이 멈춘다.
    /// ※ 메시지의 Sender 가 자기 자신이면 처리도 전달도 하지 않는다. 그래서 보내는 쪽은 자기 자신이 아니라 Prev 에 대해 호출해야 전달이 시작된다.
    /// </summary>
    public void DeliverMessageToPrev(IDeliverMessage message)
    {
        if (message.Sender.Equals(this) == false && HandleDeliveredMessage(message) == false)
        {
            Prev?.DeliverMessageToPrev(message);
        }
    }

    /// <summary>
    /// 메시지를 위(Next) 방향으로 전달한다. 판정 조건은 DeliverMessageToPrev 와 같다.
    /// </summary>
    public void DeliverMessageToNext(IDeliverMessage message)
    {
        if (message.Sender.Equals(this) == false && HandleDeliveredMessage(message) == false)
        {
            // [참고] Next 의 DeliverMessageToNext 가 아니라 DeliverMessageToPrev 를 호출한다.
            //        그래서 Next 가 처리하지 않으면 메시지는 다시 Prev 방향(이 팝업 쪽)으로 전달되고, Next 의 그 위 팝업들까지는 가지 않는다.
            Next?.DeliverMessageToPrev(message);
        }
    }

    /// <summary>
    /// 전달받은 메시지를 처리하는 훅. 처리했으면 true(전달 중단), 아니면 false(다음 팝업으로 계속 전달).
    /// 베이스 구현은 항상 false.
    /// </summary>
    protected virtual bool HandleDeliveredMessage(IDeliverMessage message)
    {
        return false;
    }
    #endregion


    #region Popup linked list

    /// <summary>
    /// 이 팝업을 연결 리스트의 맨 위(Last)에 붙인다. ShowAsObservable 에서 호출.
    /// 기존 Last.Next = this, this.Prev = 기존 Last, this.Next = null, Last = this.
    /// 반환값은 새 Last(= this). 호출측은 .Prev 로 "바로 아래 팝업" 을 얻어 숨김 처리한다.
    /// </summary>
    private IPopup AddToList()
    {
        if (Last != null)
        {
            Last.Next = this;
        }

        Prev = Last;
        Next = null;
        Last = this;

        return Last;
    }

    /// <summary>
    /// 이 팝업이 맨 위(Last)라고 가정하고 리스트에서 뗀다. CloseAsObservable 에서 호출.
    /// Prev.Next = this.Next, Last = Prev. 반환값은 새 Last(= 바로 아래 팝업, 없으면 null) 로, 닫힌 뒤 다시 보여줄 대상이 된다.
    /// </summary>
    private IPopup RemoveLastFromList()
    {
        if (Prev != null)
        {
            Prev.Next = Next;
        }

        // [참고] Next.Prev 는 갱신하지 않으며 Last 를 무조건 Prev 로 바꾼다.
        //        이 팝업 위에 다른 팝업(Next)이 남아 있는 상태에서 이 경로로 닫히면 리스트 연결이 어긋날 수 있다.
        //        (보통 위에 팝업이 있으면 이 팝업은 HIDE 상태라 Close() 가 CloseInHide 경로로 간다)
        Last = Prev;
        return Last;
    }

    /// <summary>
    /// 리스트 중간에서 이 팝업을 뗀다(앞뒤를 서로 이어 붙임). Last 는 건드리지 않는다. CloseInHide 에서 호출.
    /// </summary>
    private void RemoveFromList()
    {
        if (Prev != null)
        {
            Prev.Next = Next;
        }

        if (Next != null)
        {
            Next.Prev = Prev;
        }
    }

    #endregion

    #region Popup control

    /// <summary>
    /// 다른 팝업의 열기/닫기 연출(isInTransition)이 끝날 때까지 기다리는 Observable.
    /// - 지금 연출 중이 아니면 즉시 값 하나를 내보낸다.
    /// - 연출 중이면 FixedUpdate 마다 isInTransition 을 확인하다가 false 가 되는 순간 값 하나를 내보내고 완료된다.
    ///   (DistinctUntilChanged + TakeWhile(true 인 동안) + LastOrDefault → true 구간이 끝날 때 1회 발행)
    /// </summary>
    private IObservable<Unit> WaitForFinishTransition()
    {
        if (isInTransition == false)
        {
            return Observable.ReturnUnit();
        }

        return Observable.EveryFixedUpdate()
            .Select(_ => isInTransition)
            .DistinctUntilChanged()
            .TakeWhile(isIn => isIn)
            .LastOrDefault()
            .AsUnitObservable();
    }
    /// <summary>
    /// 팝업을 여는 Observable. 반환만 해서는 아무 일도 일어나지 않고, 구독(Subscribe)하는 순간 아래 절차가 순서대로 진행된다.
    ///  1) 다른 팝업의 연출이 끝날 때까지 대기 (WaitForFinishTransition)
    ///  2) isInTransition = true, 화면 터치 잠금 (TouchLock(true))
    ///  3) 연결 리스트 맨 위에 등록 (AddToList) 하고 바로 아래 팝업을 얻는다
    ///  4) 아래 팝업이 있으면 그 팝업의 HideAsObservable (Status = HIDE, DISABLE_HIDE 가 아니면 숨김 연출) 이 끝날 때까지 대기
    ///  5) 백키 스택에 등록 → 이 팝업이 백키를 받는 최상단이 된다
    ///  6) Status = SHOW → OnBeforeShow() → 등장 연출 transition.In() 대기
    ///  7) OnAfterShow() → CLOSE_OTHERS 플래그면 아래 팝업들을 모두 CloseInHide
    ///  8) isInTransition = false, 터치 잠금 해제 → Status = AFTER_SHOW
    ///  9) OnShow() 가 반환한 Observable 로 이어진다. 구독자가 받는 값은 이 스트림의 값이다.
    ///     - BasePopup<T> 기본: default(T) 를 즉시 1회 내보내고 완료.
    ///     - CPopupUI: subject 를 반환하므로, 팝업이 닫힐 때(OnClose) result 가 1회 발행되고 완료된다.
    /// </summary>
    public IObservable<T> ShowAsObservable()
    {
        // StartWith(Unit.Default): 구독 시 시작 신호 1개를 만들어 아래 체인을 한 번 흘려보내기 위한 관용구
        return Observable.Empty<Unit>().StartWith(Unit.Default)
            // 1) 다른 팝업 연출 종료 대기
            .SelectMany(_ => WaitForFinishTransition())
            // 2) 연출 시작 표시 + 터치 잠금
            .Do(_ =>
            {
                BasePopup.isInTransition = true;
                popupService.TouchLock(true);
            })
            // 3) 리스트 맨 위에 등록하고 바로 아래 팝업(Prev)을 다음 단계로 넘김
            .Select(_ => AddToList().Prev as BasePopup)
            // 4) 아래 팝업 숨김 (없으면 바로 통과)
            .SelectMany(prev =>
            {
                if (prev == null)
                    return Observable.ReturnUnit();

                return prev.HideAsObservable();
            })
            // 5) 백키 스택 등록
            .Do(_ => backPressService.Add(this))
            // 6) 상태 SHOW → 열기 직전 훅 → 등장 연출
            .Do(_ => Status = PopupStatus.SHOW)
            .Do(_=> OnBeforeShow())
            // [참고] transition 이 null 이면 SelectMany 에 null 이 전달된다. (닫기 쪽 CloseAsObservable 은 null 을 따로 검사한다)
            .SelectMany(_ => transition?.In())
            //.Delay(TimeSpan.FromSeconds(0.25))
            // 7) 열린 직후 훅
            .Do(_ => OnAfterShow())
            // 7) CLOSE_OTHERS: 아래 팝업 전부 정리
            .Do(_ =>
            {
                if (IsCloseOthers == true)
                {
                    CloseOthers(this);
                }
            })
            // 8) 연출 종료 표시 + 터치 잠금 해제
            .Do(_ =>
            {
                BasePopup.isInTransition = false;
                popupService.TouchLock(false);
            })
            .Do(_ => Status = PopupStatus.AFTER_SHOW)
            // 9) 결과 스트림으로 전환 (CPopupUI 는 subject → 닫힐 때 결과 1회)
            .SelectMany(_ => OnShow());
    }

    /// <summary>
    /// 위 팝업이 닫혀 이 팝업이 다시 맨 위가 될 때 호출된다. (위 팝업의 CloseAsObservable 에서 호출)
    /// DISABLE_HIDE 면 연출 없이, 아니면 transition.InFromHide() 연출 후 Status = SHOW.
    /// ※ 백키 스택에는 열 때 Add 된 상태 그대로 남아 있으므로 여기서 다시 등록하지 않는다.
    /// </summary>
    protected internal override IObservable<Unit> ShowFromHideAsObservable()
    {
        return Observable.Empty<Unit>().StartWith(Unit.Default)
            .SelectMany(_ =>
            {
                // 숨김 연출을 하지 않는 팝업은 복귀 연출도 없음
                if (HasFlag(PopupFlags.DISABLE_HIDE) == true) return Observable.ReturnUnit();

                return transition?.InFromHide();
            })
            .Do(_ => Status = PopupStatus.SHOW);
    }

    /// <summary>
    /// 위에 새 팝업이 열려 이 팝업이 가려질 때 호출된다. (새 팝업의 ShowAsObservable 에서 호출)
    /// 먼저 Status = HIDE 로 바꾸고, DISABLE_HIDE 가 아니면 transition.OutToHide() 연출을 한다.
    /// DISABLE_HIDE 면 연출 없이 화면에 그대로 남는다. (상태만 HIDE)
    /// </summary>
    protected internal override IObservable<Unit> HideAsObservable()
    {
        return Observable.Empty<Unit>().StartWith(Unit.Default)
            .Do(_ => Status = PopupStatus.HIDE)
            .SelectMany(_ =>
            {
                if (HasFlag(PopupFlags.DISABLE_HIDE) == true) return Observable.ReturnUnit();

                return transition?.OutToHide();
            });
    }

    /// <summary>
    /// 닫기 절차가 이미 시작되었는지 여부. CloseAsObservable / CloseInHide 진입 시 true 가 되며 다시 false 로 돌아가지 않는다.
    /// → 중복 닫기(버튼 연타 + 백키 등)를 막는 용도.
    /// </summary>
    protected bool isInClose = false;
    /// <summary>
    /// 맨 위 팝업을 연출과 함께 닫는 Observable. Close() 와 OnBackPress() 가 사용한다.
    /// [즉시 무시되는 경우] 아래 두 경우는 닫지 않고 값 하나만 바로 내보낸다. (OnClose 도 호출되지 않으므로 결과도 발행되지 않음)
    ///  - 이미 닫는 중(isInClose)
    ///  - 생성 후 _IntervalTime(70ms)이 지나지 않음 → 이 경우 isInClose 는 false 로 남으므로 나중에 다시 닫을 수 있다.
    /// [닫기 절차] (구독 시 진행)
    ///  1) 다른 팝업 연출 종료 대기 → isInTransition = true, 터치 잠금
    ///  2) 백키 스택에서 제거 → 리스트에서 떼고(RemoveLastFromList) 새 Last(아래 팝업)를 얻음
    ///  3) OnBeforeClose() → 퇴장 연출 transition.Out() (transition 이 없으면 생략)
    ///  4) Status = CLOSE → 아래 팝업이 있으면 ShowFromHideAsObservable 로 다시 보여줌
    ///  5) DestroyPopup() (부모에서 떼고 GameObject 파괴 요청)
    ///  6) isInTransition = false, 터치 잠금 해제, CPopupService 열린 목록에서 PrefabPath 로 제거
    ///  7) OnClose() → CPopupUI 는 여기서 result 를 발행하고 subject 를 완료한다
    /// ※ DestroyPopup 은 GameObjectExtensions.Destroy 를 쓴다. 빌드에서는 GameObject.Destroy(프레임 끝에 파괴)이고,
    ///   에디터에서는 Animator 가 없으면 DestroyImmediate(즉시 파괴, OnDestroy 도 즉시 실행)다.
    ///   6), 7) 은 C# 필드(popupService, PrefabPath, subject 등)만 사용하므로 어느 쪽이든 이어서 실행된다.
    /// </summary>
    protected IObservable<Unit> CloseAsObservable()
    {
        // 이미 닫는 중이면 무시
        if (isInClose)
        {
            CDebug.Log("CloseAsObservable return");
            return Observable.ReturnUnit();
        }

        // 생성 직후(70ms 이내) 닫기 요청은 무시
        // [참고] 이 경우 Close() 를 호출해도 팝업이 닫히지 않고 결과도 발행되지 않는다. 다시 호출해 주는 곳이 없으면 팝업이 남는다.
        if((DateTime.Now - _OpenTime).TotalMilliseconds < _IntervalTime)
        {
            CDebug.Log("CloseIntervalTime return");
            return Observable.ReturnUnit();
        }

        // 여기서부터 닫기 확정. 이후 Close/백키 재호출은 위에서 걸러진다.
        // [참고] 반환된 Observable 을 구독하지 않으면 isInClose 만 true 가 되고 실제 닫기는 진행되지 않는다.
        isInClose = true;

        return Observable.Empty<Unit>().StartWith(Unit.Default)
            // 1) 다른 팝업 연출 종료 대기 → 연출 시작 표시 + 터치 잠금
            .SelectMany(_ => WaitForFinishTransition())
            .Do(_ =>
            {
                BasePopup.isInTransition = true;
                popupService.TouchLock(true);
            })
            // 2) 백키 스택 제거 → 리스트에서 떼고 새 Last(아래 팝업)를 다음 단계로 넘김
            .Do(_ => backPressService.Remove(this))
            .Select(_ => RemoveLastFromList() as BasePopup)
            // 3) 닫기 직전 훅 → 퇴장 연출 (아래 팝업 참조는 그대로 다음 단계로 전달)
            .Do(_ => OnBeforeClose())
            .SelectMany(popup =>
			{
				if(transition == null) return Observable.Return(popup as BasePopup);

				return transition.Out().Select(_ => popup as BasePopup);
			})
            // 4) 상태 CLOSE → 아래 팝업이 있으면 다시 보여주기 (숨김 복귀 연출 대기)
            .Do(_ => Status = PopupStatus.CLOSE)
            .SelectMany(last => last == null ? Observable.ReturnUnit() : last.ShowFromHideAsObservable())
            // 5) GameObject 파괴 요청
            .Do(_ => DestroyPopup())
            // 6) 연출 종료 표시 + 터치 잠금 해제 + 열린 팝업 목록에서 제거
            .Do(_ =>
            {
                BasePopup.isInTransition = false;
                popupService.TouchLock(false);
                popupService.RemovePopup(PrefabPath);
            })
            // 7) 닫힘 훅 (CPopupUI: result 발행 + subject 완료)
            .Do(_ => OnClose());
    }

    /// <summary>
    /// 팝업인데 빽키 사용안함
    /// </summary>
    // (보충) 백키 스택에서 이 팝업만 제거한다. 연결 리스트/상태는 그대로이며, 이후 백키가 이 팝업으로 오지 않는다.
    protected virtual void BackPressRemove()
    {
        backPressService.Remove(this);
    }
    /// <summary>열기 절차 중 Status = SHOW 직후, 등장 연출(In) 시작 전에 호출되는 훅.</summary>
    protected virtual void OnBeforeShow()
    {
        
    }

    /// <summary>열기 절차 중 등장 연출(In)이 끝난 직후, CLOSE_OTHERS 처리/터치 잠금 해제 전에 호출되는 훅.</summary>
    protected virtual void OnAfterShow()
    {

    }

    /// <summary>
    /// 닫히기 직전 훅. CloseAsObservable(연출 닫기)에서는 리스트 제거 후 퇴장 연출 전에,
    /// CloseInHide(숨김 상태 즉시 닫기)에서는 리스트 제거 직후에 호출된다. (두 경로 모두 호출됨)
    /// </summary>
    protected virtual void OnBeforeClose()
    {

    }

    /// <summary>
    /// 열기 절차의 마지막에 이어 붙일 "결과 스트림" 을 반환한다. ShowAsObservable 구독자가 받는 값은 이 스트림의 값이다.
    /// 기본 구현: default(T) 를 즉시 1회 내보내고 완료 → 열기 연출이 끝나자마자 구독자에게 값이 전달된다.
    /// CPopupUI 는 이를 재정의해 subject 를 반환한다. (닫힐 때 결과 발행)
    /// </summary>
    internal virtual IObservable<T> OnShow()
    {
        return Observable.Return<T>(default);
    }

    /// <summary>
    /// 연출 닫기(CloseAsObservable)의 맨 마지막 단계에서 호출되는 훅. 기본 구현은 비어 있다.
    /// ※ CloseInHide(숨김 상태 즉시 닫기) 경로에서는 호출되지 않는다.
    /// </summary>
    internal virtual void OnClose()
    {

    }

    /// <summary>
    /// 숨김 상태(위에 다른 팝업이 있음)의 팝업을 연출 없이 즉시 닫는다.
    /// 호출: Close() 에서 Status == HIDE 일 때 / 다른 팝업의 CLOSE_OTHERS 처리(CloseOthers).
    /// 리스트 중간에서 떼기 → OnBeforeClose → 백키 스택 제거 → GameObject 파괴 요청 → 열린 팝업 목록에서 제거.
    /// ※ 연출 대기/터치 잠금/아래 팝업 복귀가 없고, OnClose() 도 호출되지 않는다.
    /// </summary>
    protected internal override void CloseInHide()
    {
        // 중복 닫기 방지
        if (isInClose)
        {
            return;
        }
        isInClose = true;

        // 앞뒤 팝업을 서로 이어 붙이고 이 팝업만 리스트에서 뺀다
        RemoveFromList();
        OnBeforeClose();
        backPressService.Remove(this);
        DestroyPopup();
        popupService.RemovePopup(PrefabPath);
    }

    /// <summary>
    /// 팝업을 닫는다. (IPopup.Close) 팝업 내부의 닫기/확인 버튼 등에서 호출한다.
    /// - HIDE 상태면 CloseInHide 로 즉시 닫는다.
    /// - 그 외에는 CloseAsObservable 을 직접 구독해서 연출 닫기를 진행하고, 값이 나오면 구독을 정리한다.
    /// CPopupUI 하위 팝업은 보통 result 값을 채운 뒤 Close() 를 호출한다. (OnClose 시점에 그 result 가 발행됨)
    /// </summary>
    public void Close()
    {
        if (Status == PopupStatus.HIDE)
        {
            CloseInHide();
            return;
        }

        // SingleAssignmentDisposable: 구독 핸들을 담아 두었다가 첫 값이 나오면 스스로 해제하는 1회성 구독 패턴
        var close = new SingleAssignmentDisposable();
        close.Disposable = CloseAsObservable()
            .Do(_ => close.Dispose())
            .Subscribe();
    }

    /// <summary>
    /// 모든 팝업 닫기. (IPopup.CloseAllPopups)
    /// 실제 동작은 맨 위 팝업(Last)의 Close() 1회뿐이다. (CloseOthers 호출은 주석 처리되어 있음)
    /// </summary>
    public void CloseAllPopups()
    {
        // [참고] 이름과 달리 맨 위 팝업 하나만 닫힌다. Last 가 null 이면 NullReferenceException 이 난다.
  //      CloseOthers(Last);
        Last.Close();
    }

    /// <summary>
    /// popup 아래(Prev 방향)의 팝업들을 모두 CloseInHide 로 닫는다. CLOSE_OTHERS 플래그일 때 ShowAsObservable 에서 호출.
    /// CloseInHide → RemoveFromList 가 popup.Prev 를 한 칸 아래로 다시 이어 주므로, Prev 가 null 이 될 때까지 반복된다.
    /// </summary>
    private void CloseOthers(IPopup popup)
    {
        while (popup.Prev != null)
        {
            // [참고] Prev 가 이미 isInClose 상태면 CloseInHide 가 바로 return 해서 popup.Prev 가 바뀌지 않으므로 이 루프가 끝나지 않을 수 있다.
            //        Prev 가 BasePopup 이 아니어도(as 결과 null) 마찬가지다.
            (popup.Prev as BasePopup)?.CloseInHide();
        }

    }

    /// <summary>
    /// GameObject 를 부모(PopupLayer)에서 떼어낸 뒤 파괴한다.
    /// 부모에서 먼저 떼므로 PopupLayer 의 자식 수(HUD 처리에서 참조)가 바로 줄어든다.
    /// </summary>
    private void DestroyPopup()
    {
        transform.SetParent(null);
            
        // 시작 신호 1개로 즉시(동기) 파괴를 실행하고 구독을 스스로 정리한다. (Delay 는 주석 처리되어 있어 지연 없음)
        // gameObject.Destroy() 는 확장 메서드: 빌드는 GameObject.Destroy, 에디터는 Animator 가 없으면 DestroyImmediate.
        var destoryObject = new SingleAssignmentDisposable();
        destoryObject.Disposable = Observable.Empty<Unit>()
            .StartWith(Unit.Default)
            //.Delay(TimeSpan.FromSeconds(0.25))
            .Do(_ =>
            {
                gameObject.Destroy();
                destoryObject.Dispose();
            })
            .Subscribe();
    }
    #endregion

    /// <summary>Flags 에 flag 비트가 모두 켜져 있는지 검사한다.</summary>
    protected bool HasFlag(PopupFlags flag)
    {
        return (Flags & flag) == flag;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    // (보충) IOnBackPress 구현. 백키(ESC/안드로이드 뒤로가기) 입력 시 COnBackPressService 가 백키 스택의 "마지막" 항목에 대해서만 호출하고
    //        반환된 Observable 을 구독한다. 팝업과 페이지(PagePopup)가 같은 스택을 쓰므로, 맨 위에 열린 것이 백키를 받는다.
    //        반환값 true = 처리함(웹뷰 닫기 또는 팝업 닫기), false = 처리하지 않음.
    //        처리 순서: 웹뷰 열림 → 웹뷰만 닫음 / 연출 중·DISABLE_BACK_PRESS·튜토리얼 중 → 무시(false) / 그 외 → OnBackEventProcess() 후 CloseAsObservable.
    public virtual IObservable<bool> OnBackPress()
    {
        CDebug.Log("[BasePopup : OnBackPress]");
        // 웹뷰 먼저 닫고
        if (TGP_WebAPI.GetInstance() != null)
        {
            if (TGP_WebAPI.GetInstance().GetWebViewState())
            {
                TGP_WebAPI.GetInstance().WebViewClose();
                return Observable.Return(true);
            }
        }
        // StartWith(false) 로 시작 값 false 를 만들고, 아래 조건에 걸리면 그 false 를 그대로 반환한다.
        return Observable.Empty<bool>().StartWith(false)
            .SelectMany(canNotHandle =>
            {
                // Transition 중
                if (isInTransition)
                    return Observable.Return(canNotHandle);

                // back key event off
                if (IsDisableBackPress)
                    return Observable.Return(canNotHandle);

                // tutorial
                if(TutorialManager.Instance.IsPlay)
                    return Observable.Return(canNotHandle);

                // back key
                // 하위 클래스 훅(OnBackEventProcess) → 그 다음 연출 닫기. 닫기가 끝나면 true 를 내보낸다.
                // CloseAsObservable 이 isInClose / 70ms 조건으로 무시된 경우에도 Select 에 의해 true 가 나온다.
                OnBackEventProcess();

                return CloseAsObservable().Select(_ => true);
            });
    }

    /// <summary>
    /// 팝업이 "보이는 상태로 안정되어" 입력을 받아도 되는지 검사한다. (하위 클래스의 버튼 처리 등에서 사용)
    /// 연출 중이거나, 생성 후 _IntervalTime 이 안 지났으면 false. 그 외에는 Status 가 SHOW 또는 AFTER_SHOW 일 때 true.
    /// </summary>
    protected bool IsShowState()
    {
        if (isInTransition)
            return false;
        if ((DateTime.Now - _OpenTime).TotalMilliseconds < _IntervalTime)
            return false;

        return Status == PopupStatus.SHOW || Status == PopupStatus.AFTER_SHOW;
    }

    /// <summary>
    /// 백키로 닫히기 직전에 호출되는 하위 클래스 훅. (OnBackPress 에서 CloseAsObservable 전에 호출) 기본 구현은 비어 있다.
    /// ※ 팝업 내 버튼에서 직접 Close() 를 호출하는 경로에서는 호출되지 않는다.
    /// </summary>
    protected virtual void OnBackEventProcess() { }
}

/// <summary>
/// "입력(Setting)을 받아 열리고, 닫힐 때 결과(Result)를 돌려주는" 일반 팝업 베이스. LevelLink의 Popup* 클래스들이 상속한다.
///
/// [하위 클래스 사용 패턴] (예: PopupLevelLinkDecksave)
///  - 중첩 클래스로 Setting / Result 를 정의하고 CPopupUI<Setting, Result> 를 상속한다.
///  - OnAwake 에서 subject = new Subject<Result>(), result = 기본값(보통 실패/취소), Flags 를 설정한다.
///    (subject 는 이 클래스가 만들어 주지 않으므로 하위 클래스가 반드시 생성해야 한다)
///  - 여는 쪽(PopupServiceHelper 의 OpenPopupXXX)이 SetData(new Setting{...}) 로 입력값을 넘긴다.
///  - 확인 버튼 등에서 result 를 채운 뒤 Close() 를 호출한다.
///
/// [결과 전달 흐름]
///  - 호출측: popup.ShowAsObservable().Subscribe(res => ...)
///  - 열기 연출이 끝나면 ShowAsObservable 의 마지막 단계가 OnShow() = subject 로 이어지므로, 구독자는 subject 를 구독한 상태가 된다.
///  - 연출 닫기(Close() / 백키) 의 마지막 단계 OnClose() 에서 subject.OnNext(result) 1회 → OnCompleted → Dispose.
///    → 구독자는 결과를 정확히 1번 받고, 스트림이 완료된다.
///  - 숨김 상태에서 닫히는 경우(CloseInHide)는 OnNext 없이 OnCompleted 만 발생한다. (구독자의 onNext 콜백은 실행되지 않음)
/// </summary>
public class CPopupUI<TSetting, TResult> : BasePopup<TResult>
{
    /// <summary>
    /// 결과를 구독자에게 보내는 Subject. 하위 클래스가 OnAwake 에서 생성한다.
    /// OnShow 에서 ShowAsObservable 체인의 마지막으로 반환되고, OnClose 에서 result 를 1회 발행한 뒤 완료/해제된다.
    /// </summary>
    protected Subject<TResult> subject;
    /// <summary>
    /// 닫힐 때 발행할 결과 값. 하위 클래스가 OnAwake 에서 기본값을 넣고, 사용자 조작에 따라 채운 뒤 Close() 한다.
    /// 발행 시점(OnClose)의 값이 그대로 전달된다.
    /// </summary>
    protected TResult result;
    /// <summary>
    /// 여는 쪽이 SetData 로 넘긴 입력값. 하위 클래스가 OnStart/UI 구성에서 읽는다.
    /// </summary>
    protected TSetting setting;

    /// <summary>
    /// 팝업 입력값 저장. PopupServiceHelper 의 OpenPopupXXX 가 생성 직후(Awake/OnAwake 이후, Start 이전) 호출한다.
    /// 베이스 구현은 setting 필드에 저장만 한다. 하위 클래스가 재정의해 추가 처리를 할 수 있다.
    /// </summary>
    public virtual void SetData(TSetting data)
    {
        setting = data;
    }

    /// <summary>
    /// 열기 연출이 끝난 뒤 ShowAsObservable 이 이어 붙일 스트림으로 subject 를 반환한다.
    /// 현재 씬이 타이틀 씬보다 뒤(씬 ID 값이 TITLE_SCENE 보다 큼)일 때만 공용 팝업 효과음을 재생한다.
    /// </summary>
    internal override IObservable<TResult> OnShow()
    {
        // 타이틀 이후 씬에서만 팝업 열림 효과음
        if (CDirector.Instance?.GetCurrentSceneID() > CSceneId.TITLE_SCENE)
            CSoundControl.Instance.EffectPlay(SOUND_ID.SFX_COMMON_POPUP);

        // [참고] 하위 클래스가 OnAwake 에서 subject 를 만들지 않으면 null 이 반환되어 SelectMany 에 null 이 전달된다.
        return subject;
    }

    /// <summary>
    /// 연출 닫기(CloseAsObservable)의 마지막 단계에서 호출된다.
    /// 구독자가 있으면 현재 result 를 1회 발행(OnNext)하고, 이어서 완료(OnCompleted) + 해제(Dispose) 한다.
    /// </summary>
    internal override void OnClose()
    {
        // 구독자가 있을 때만 결과 발행
        if (subject.HasObservers)
        {
            subject.OnNext(result);
        }
        // 완료 + 해제 (구독자가 있을 때만)
        OnCompleteSubject();
    }

    /// <summary>
    /// 숨김 상태 즉시 닫기. 베이스 처리(리스트 제거/파괴 등) 후 subject 를 완료한다.
    /// ※ OnNext(result) 는 하지 않는다. → 구독자는 결과 값 없이 완료만 받는다.
    /// </summary>
    protected internal override void CloseInHide()
    {
        // [참고] base.CloseInHide 가 isInClose 로 중복 호출을 걸러 return 해도, 아래 OnCompleteSubject 는 그대로 실행된다.
        //        (첫 호출에서 Dispose 되었다면 그 뒤로는 HasObservers 가 false 라 추가 동작은 없다)
        base.CloseInHide();
        OnCompleteSubject();
    }

    /// <summary>
    /// 구독자가 있으면 subject 를 완료(OnCompleted)하고 Dispose 한다.
    /// [참고] 구독자가 없으면 아무 것도 하지 않으므로 subject 가 완료/해제되지 않은 채 남는다.
    ///        호출측이 onNext 콜백 안에서 자기 구독을 Dispose 하는 패턴(LevelLink 호출부의 disposable.Dispose())이면
    ///        OnNext 직후 구독이 끊겨 이 시점에 HasObservers 가 false 가 되므로, 이 경우에도 OnCompleted/Dispose 는 실행되지 않는다.
    /// </summary>
    private void OnCompleteSubject()
    {
        if (subject.HasObservers)
        {
            subject.OnCompleted();
            subject.Dispose();
        }
    }
}

/// <summary>
/// 전체 화면 "페이지" 의 베이스. (예: PageLevelLink)
/// 일반 팝업(BasePopup<T>)과 달리 아래 특징이 있다.
///  - IPopup 을 구현하지 않는다 → 팝업 연결 리스트(Last/Prev/Next)와 CPopupService 열린 팝업 목록에 들어가지 않는다.
///  - 생성: CResourceManager.LoadPagePopup<T>(프리팹 키) 가 프리팹을 SafeArea 오브젝트 아래에 생성하고 SetPrefabPath 를 호출한다.
///  - 등장/퇴장 연출(transition)과 결과(Result) 스트림이 없다. 닫기는 OnClosePopup() 으로 GameObject 를 파괴하는 방식이다.
///  - 백키 스택(COnBackPressService)의 일반 스택과 페이지 스택(page_stack) 양쪽에 등록된다. (Start 에서 등록, OnDisable 에서 제거)
///  - IStaticCavasLayer 를 구현해 static canvas 레이어 정렬 정보(Layer = PagePopup, Order, Name, GameObject)를 가진다.
///
/// [수명주기]
///  Awake → OnAwake  /  Start → SetType(PAGE) → ShowAsObservable(백키 스택 등록) → OnStart → StartBarManager → OnAfterShow
///  닫기: OnClosePopup()(직접 호출 또는 백키 기본 처리) → Destroy → OnDisable(백키 스택 제거) → OnDestroy(메뉴 연출 해제, 에셋 언로드)
/// </summary>
public class PagePopup : BasePopup, IStaticCavasLayer, IOnBackPress
{

    #region IStaticCanvasLayer implements

    /// <summary>레이어 이름 상수. Awake 에서 Name 에 들어간다.</summary>
    public const string NAME = "PagePopup";
    /// <summary>원래 소속 레이어. Awake 에서 EStaticCavasLayer.PagePopup 으로 설정.</summary>
    public EStaticCavasLayer Layer { get; protected set; }
    /// <summary>정렬 순서 값. 기본은 (int)Layer. CStaticCanvas.RefreshOrder 가 Order → Name 순으로 정렬해 형제 순서를 정한다.</summary>
    public int Order { get; protected set; }
    /// <summary>레이어 이름 (= NAME). 같은 Order 끼리의 정렬 기준.</summary>
    public string Name { get; protected set; }
    /// <summary>이 페이지의 GameObject. RefreshOrder 에서 SetSiblingIndex 대상.</summary>
    public GameObject GameObject { get; protected set; }

    /// <summary>IStaticCavasLayer 초기화 콜백. 베이스 구현은 비어 있다.</summary>
    public virtual void OnInitialize()
    {

    }

    /// <summary>
    /// 지정한 레이어 바로 위로 올린다. Order = target + 1 로 바꾸고 CStaticCanvas.RefreshOrder 로 재정렬한다.
    /// ※ RefreshOrder 는 CStaticCanvas 에 등록된 레이어 목록만 정렬한다. 이 파일 안에는 페이지를 그 목록에 등록하는 코드가 없다.
    /// </summary>
    public void AboveTargetLayer(EStaticCavasLayer target)
    {
        var targetint = (int)target;
        Order = targetint + 1;
        CStaticCanvas.Instance.RefreshOrder();
    }

    /// <summary>
    /// Order 를 원래 레이어 값((int)Layer)으로 되돌리고 재정렬한다.
    /// </summary>
    public void ReturnOriginalLayer()
    {
        Order = (int)Layer;
        CStaticCanvas.Instance.RefreshOrder();
    }

    #endregion 



    /// <summary>프리팹 경로. CResourceManager.LoadPagePopup 이 SetPrefabPath 로 넣어 준다. OnDestroy 에서 에셋 언로드 키로 사용.</summary>
    public string PrefabPath { get; set; }
    /// <summary>팝업 플래그. 이 클래스 안에서는 값을 설정하거나 읽는 코드가 없다.</summary>
    public PopupFlags Flags { get; protected set; }
    /// <summary>페이지 상태. Show/ShowFromHide/Hide 모두 SHOW 로만 설정한다.</summary>
    public PopupStatus pageStatus { get; protected set; }
    /// <summary>페이지는 연출 객체를 쓰지 않는다. 읽기/쓰기 모두 NotImplementedException 을 던진다.</summary>
    protected override IPopupTransition transition { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    /// <summary>페이지는 팝업 리스트에 없으므로 숨김 상태 닫기가 필요 없다. 아무 것도 하지 않는다.</summary>
    protected internal override void CloseInHide() { }

    /// <summary>백키 서비스. 일반 스택 + 페이지 스택 등록/제거, 페이지 스택 개수 조회에 사용.</summary>
    protected COnBackPressService backPressService;
    /// <summary>팝업 서비스. 에셋 언로드(Unload)에 사용.</summary>
    protected CPopupService popupService;
    /// <summary>에셋 서비스. SetType 에서 마지막으로 로드한 에셋 경로(PrefabPath)를 읽는다.</summary>
    protected CAssetService assetService;

    /// <summary>하위 클래스용 RectTransform 필드. 이 클래스 안에서는 설정하지 않는다.</summary>
    protected RectTransform rectTransform;

    /// <summary>SetType 으로 지정된 UI 타입. Start 에서 PAGE 로 설정되며 OnDestroy 에서 분기에 쓰인다.</summary>
    private UIPOPUP_TYPE popup_type;

    /// <summary>OnClosePopup/OnClosePopupAll 에서 Destroy 를 이미 요청했는지 여부. 중복 Destroy 방지.</summary>
    private bool _isDestroy = false;
    /// <summary>
    /// 백키 기본 처리(BackEventProcess → OnClosePopup) 를 할지 여부. 기본 true.
    /// 하위 클래스가 false 로 두면 백키 시 기본 닫기 없이 OnBackEventProcess 만 호출된다.
    /// </summary>
    protected bool _isBasicBackEvent = true;

    /// <summary>
    /// Unity Awake. 서비스 캐시 → IStaticCavasLayer 값(Layer/Order/Name/GameObject) 설정 → OnAwake.
    /// </summary>
    private void Awake()
    {
        popupService = CCoreServices.GetCoreService<CPopupService>();
        backPressService = CCoreServices.GetCoreService<COnBackPressService>();
        assetService = CCoreServices.GetCoreService<CAssetService>();

        // static canvas 레이어 정보: 페이지 레이어, 기본 순서
        Layer = EStaticCavasLayer.PagePopup;
        Order = (int)Layer;
        Name = NAME;
        GameObject = gameObject;

        // 하위 클래스 초기화 훅
        OnAwake();
    }

    /// <summary>
    /// Unity Start. UI 타입을 PAGE 로 설정한 뒤 ShowAsObservable 을 구독해서
    /// 백키 스택 등록 → OnStart → StartBarManager(HUD 처리) → OnAfterShow 를 순서대로 실행한다.
    /// ShowAsObservable 은 대기 없이 동기적으로 값을 내보내므로 이 모든 과정이 Start 안에서 바로 끝난다.
    /// </summary>
    private void Start()
    {
        // UI Type 기본 설정
        // (기본 인자: disable_hud = false, disableBackEvent = true)
        SetType(UIPOPUP_TYPE.PAGE);

        // 1회성 구독: 값이 나오면 스스로 Dispose. AddTo(this) 로 페이지 파괴 시에도 해제된다.
        var dispos = new SingleAssignmentDisposable();
        dispos.Disposable = ShowAsObservable()
        .Do(result =>
        {
            // result 는 Unit(구조체)이므로 이 null 비교는 항상 참이다.
            if (result != null)
            {
                // 여기서 타입 변경
                // 하위 클래스 시작 훅 (PageLevelLink 은 여기서 초기화/데이터 세팅/SetUI 를 한다)
                OnStart();

                // 여기서 스텍
                // 페이지 스택 개수(이 페이지 포함) 기준으로 HUD 표시 상태 조정
                StartBarManager(backPressService.GetStackCount());
            }
        })
	.Do(_ => OnAfterShow())
        .Subscribe(_ => dispos.Dispose())
        .AddTo(this);
    }

    /// <summary>
    /// 페이지를 닫는다. (뒤로가기 버튼/백키 기본 처리/하위 클래스에서 직접 호출)
    /// HUD 상태를 되돌린 뒤(CloseBarManager), 아직 파괴 요청 전이면 GameObject 를 Destroy 한다.
    /// 백키 스택 제거는 여기서 하지 않고, 파괴 과정의 OnDisable 에서 이뤄진다.
    /// </summary>
    public void OnClosePopup()
    {
        // 여기서 팝업이 닫을때 처리 해야 하는것들
        // (지역 변수 popupService 가 같은 이름의 필드를 가린다. 둘 다 같은 CPopupService 코어 서비스)
        var popupService = CCoreServices.GetCoreService<CPopupService>();
        // HUD 처리 기준 개수 = 페이지 스택 개수(아직 이 페이지 포함) + 열린 일반 팝업 개수
        // [참고] CloseBarManager 는 아래 _isDestroy 검사보다 먼저 실행되므로, OnClosePopup 이 두 번 호출되면 HUD 처리도 두 번 실행된다.
        //        (예: _isBasicBackEvent 가 true 인 채로 하위 클래스의 OnBackEventProcess 에서도 OnClosePopup 을 부르는 경우)
        int stackCount = backPressService.GetStackCount() + popupService.GetPopupCount; 
        CloseBarManager( stackCount );

        if (_isDestroy)
            return;
        
        if (this.gameObject != null)
        {
            Destroy(this.gameObject);
            _isDestroy = true;
        }
    }

    /// <summary>
    /// 에러 등으로 모든 팝업/페이지를 한꺼번에 닫을 때 쓰는 닫기.
    /// OnClosePopup 과 같지만 HUD 처리 기준 개수에 일반 팝업 개수를 더하지 않는다. (페이지 스택 개수만 사용)
    /// </summary>
    public void OnClosePopupAll()
    {
        // 여기서 팝업이 닫을때 처리 해야 하는것들
        //에러로 인해 모든 팝업, 페이지 팝업 닫아야 할때
        // HUD 처리 기준 개수 = 페이지 스택 개수만
        int stackCount = backPressService.GetStackCount();
        CloseBarManager( stackCount );

        if (_isDestroy)
            return;
        
        if (this.gameObject != null)
        {
            Destroy(this.gameObject);
            _isDestroy = true;
        }

    }


    /// <summary>
    /// Unity OnDestroy. PAGE 타입이고 메뉴 전환 연출(LoadingLayer 메뉴)이 켜져 있으면 끄고, 프리팹 에셋을 언로드한다.
    /// </summary>
    protected virtual void OnDestroy()
    {        
        // 페이지 타입이면 켜져 있는 메뉴 전환 연출을 끈다
        if (popup_type == UIPOPUP_TYPE.PAGE)
        {
            if(LoadingLayer.Instance.IsMenu)
                LoadingLayer.Instance.DisableMenu();
        }

        // CAssetService 리소스 테이블에서 이 프리팹 제거
        if (PrefabPath != null)
        {
            popupService.Unload(PrefabPath);
        }
    }

    /// <summary>프리팹 경로 설정. CResourceManager.LoadPagePopup 이 생성 직후 호출한다.</summary>
    public void SetPrefabPath(string path)
    {
        PrefabPath = path;
    }

    /// <summary>Awake 마지막에 호출되는 하위 클래스 초기화 훅.</summary>
    protected virtual void OnAwake() { }
    /// <summary>Start 에서 백키 스택 등록 직후 호출되는 하위 클래스 시작 훅. 페이지 화면 구성은 보통 여기서 한다.</summary>
    protected virtual void OnStart() { }
    /// <summary>Start 의 마지막(OnStart, StartBarManager 이후)에 호출되는 훅.</summary>
    protected virtual void OnAfterShow() { }
    /// <summary>백키 처리 훅. OnBackPress 에서 기본 처리(BackEventProcess) 뒤에 호출된다. (PageLevelLink 은 여기서 ClosePage 를 호출)</summary>
    protected virtual void OnBackEventProcess() { }
    /// <summary>백키 선처리 훅. true 를 반환하면 백키 처리를 여기서 멈춘다(닫지 않음, OnBackPress 는 false 반환). 기본 false.</summary>
    protected virtual bool OnPreBackEventProcess() { return false; }

    /// <summary>
    /// IOnBackPress 구현. 이 페이지가 백키 스택 맨 위일 때 COnBackPressService 가 호출한다.
    /// 처리 순서:
    ///  1) 웹뷰가 열려 있으면 웹뷰만 닫고 true
    ///  2) 튜토리얼 중 / 네트워크 로딩 중이면 false (무시)
    ///  3) OnPreBackEventProcess() 가 true 면 false (하위 클래스가 선처리, 페이지는 닫지 않음)
    ///  4) _isBasicBackEvent 가 true 면 BackEventProcess() → (조건 만족 시) OnClosePopup()
    ///  5) OnBackEventProcess() 하위 클래스 훅
    ///  6) true 반환
    /// ※ BasePopup<T>.OnBackPress 와 달리 isInTransition 은 검사하지 않는다. (서비스 쪽에서 연출 중 입력을 이미 거른다)
    /// </summary>
    public IObservable<bool> OnBackPress()
    {
        CDebug.Log("[Page : OnBackPress]");

        // 웹뷰 먼저 닫고
        if (TGP_WebAPI.GetInstance() != null)
        {
            if (TGP_WebAPI.GetInstance().GetWebViewState())
            {
                TGP_WebAPI.GetInstance().WebViewClose();
                return Observable.Return(true);
            }
        }

        // tutorial
        if (TutorialManager.Instance.IsPlay)
            return Observable.Return(false);

        // 네트워크 로딩 중에는 무시
        if (NetworkLoadingProgress.IsNetworkLoading())
            return Observable.Return(false);

        //팝업 끄기 전에 해야 할 프로세스가 있을 경우
        if(OnPreBackEventProcess())
            return Observable.Return(false);

        // 기본 닫기 처리
        if (_isBasicBackEvent)
            BackEventProcess();

        // 하위 클래스 추가 처리 (기본 닫기 이후에 호출된다)
        OnBackEventProcess();

        return Observable.Return(true);
    }

    /// <summary>
    ///  허드 OFF disable_hud
    ///  빽키 OFF disableBackEvent
    ///  페이지 연출 OFF pageloading
    /// </summary>
    /// <param name="type"></param>
    /// <param name="disable_hud"></param>
    /// <param name="disableBackEvent"></param>
    /// <param name="pageloading"></param>
    // (보충) 현재 시그니처 기준 동작:
    //   - popup_type 을 기록하고, CUIPopupManager 의 static 필드 bDisableBackEvent 에 disableBackEvent 를 넣는다.
    //     bDisableBackEvent 는 이름과 반대로 true 일 때 "백키로 페이지를 닫음", false 일 때 "닫지 않음" 으로 쓰인다. (SetEventProcess 참고)
    //   - CUIPopupManager.SetUIType 으로 프리팹 키/UI 타입/HUD 끔 여부를 기록한다. (pageloading 파라미터는 현재 시그니처에 없음)
    protected void SetType(UIPOPUP_TYPE type, bool disable_hud = false, bool disableBackEvent = true)
    {
        popup_type = type;
        // [참고] bDisableBackEvent 는 static 이라 모든 페이지/팝업이 공유한다. 나중에 Start 된 페이지의 값이 앞 페이지의 백키 동작에도 영향을 준다.
        bDisableBackEvent = disableBackEvent;

        if (assetService != null)
        {
            if (assetService.PrefabPath != null)
            {
                // [참고] 이 페이지의 PrefabPath 가 아니라 CAssetService 가 "마지막으로 LoadAsset 한 경로" 를 키로 쓴다.
                //        생성(LoadPagePopup)과 Start 사이에 다른 에셋이 로드되면 다른 경로가 들어갈 수 있다.
                SetUIType(assetService.PrefabPath, type, disable_hud);
            }
        }
    }

    /// <summary>
    ///  페이지 
    /// </summary>
    // (보충) 백키 기본 처리. OnBackPress 에서 _isBasicBackEvent 가 true 일 때 호출되며 SetEventProcess 로 넘긴다.
    public void BackEventProcess()
    {
        SetEventProcess();
    }

    /// <summary>
    /// 백키 기본 처리 본체. static bDisableBackEvent 가 true 이고 아직 파괴 요청 전이면 OnClosePopup() 으로 페이지를 닫는다.
    /// (예전의 페이지 전환 연출 분기는 아래에 주석으로 남아 있다)
    /// </summary>
    private void SetEventProcess()
    {
        // 빽키
        // bDisableBackEvent == false → 백키로 닫지 않음
        if (bDisableBackEvent == false)
        {
            return;
        }

        // 이미 닫기(Destroy) 요청된 페이지면 무시
        if (_isDestroy)
            return;

        OnClosePopup();
        //// 페이지 전환 연출
        //if (bPageloading && popup_type == UIPOPUP_TYPE.PAGE && backPressService.GetStackCount() <= 1)
        //{
        //    LoadingLayer.Instance.MenuChange(() =>
        //    {

        //        OnClosePopup();

        //        LoadingLayer.Instance.DisableMenu();

        //    }, null, false);
        //}
        //else
        //{

        //    OnClosePopup();
        //}
    }

    /// <summary>
    /// Unity OnDisable. 백키 일반 스택과 페이지 스택에서 이 페이지를 제거한다.
    /// 페이지가 파괴될 때(Destroy) 호출되므로 닫기 시 백키 스택 정리는 여기서 이뤄진다.
    /// [참고] GameObject 를 SetActive(false) 로 끄기만 해도 호출되어 스택에서 빠지며, 다시 켤 때 재등록하는 코드(OnEnable)는 없다.
    /// </summary>
    protected virtual void OnDisable()
    {
        backPressService.Remove(this);
        backPressService.PageRemove(this);
    }

    /// <summary>
    /// 페이지 "열기". Start 에서 구독된다. 연출/대기 없이 즉시
    /// 백키 일반 스택(Add) + 페이지 스택(PageAdd)에 등록하고 pageStatus = SHOW 로 바꾼 뒤 값 하나를 내보낸다.
    /// (BasePopup<T>.ShowAsObservable 과 이름만 같고 별개의 메서드다)
    /// </summary>
    public IObservable<Unit> ShowAsObservable()
    {
        return Observable.Empty<Unit>().StartWith(Unit.Default)
        .SelectMany(_ =>
        {
            // 백키를 받을 수 있도록 스택 등록. 페이지 스택 개수는 HUD 처리 기준으로 쓰인다.
            backPressService.Add(this);
            backPressService.PageAdd(this);
            return Observable.ReturnUnit();
        })
        .Do(_ => pageStatus = PopupStatus.SHOW);
    }

    /// <summary>BasePopup 추상 메서드 구현. 연출 없이 pageStatus = SHOW 로만 바꾼다. (페이지는 팝업 리스트에 없으므로 이 파일 안에서 호출되는 경로는 없다)</summary>
    protected internal override IObservable<Unit> ShowFromHideAsObservable()
    {
        return Observable.Empty<Unit>().StartWith(Unit.Default)
        .SelectMany(_ =>
        {
            return Observable.ReturnUnit();
        })
        .Do(_ => pageStatus = PopupStatus.SHOW);
    }

    /// <summary>
    /// BasePopup 추상 메서드 구현. 연출 없이 값 하나를 내보낸다.
    /// ※ 이름은 Hide 지만 pageStatus 를 HIDE 가 아니라 SHOW 로 설정한다. (코드 그대로)
    /// </summary>
    protected internal override IObservable<Unit> HideAsObservable()
    {
        return Observable.Empty<Unit>().StartWith(Unit.Default)
        .SelectMany(_ =>
        {
            return Observable.ReturnUnit();
        })
        .Do(_ => pageStatus = PopupStatus.SHOW);
    }
}