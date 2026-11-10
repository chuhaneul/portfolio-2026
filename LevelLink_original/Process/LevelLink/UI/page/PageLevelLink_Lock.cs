using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UniRx;

/// <summary>
/// LevelLink 페이지의 잠금 덮개 UI - 현재 LevelLink가 잠겨(LOCK) 있을 때 중단 영역 위를 덮고, 해금 연출을 재생한다.
/// - 부모: PageLevelLink (LockUI 필드). 자식 레이아웃 없음.
/// - Setting 은 PageLevelLink.SetData_LockUI 에서 채워진다 (상태, 해금 연출 재생 여부, 클릭/연출 종료 콜백).
/// - 해금 흐름: 클릭 → 슬롯 추가 팝업 → API 성공 → LevelLinkUnLockSlot → SetData_LockUI(true) + SetUI → PlayUnLock
///   → 애니메이션 클립 이벤트가 OnCompleteUnclock 호출 → PageLevelLink.OnCompleteUnlockAni → 전체 갱신(이때 LOCK 이 아니므로 숨김)
/// </summary>
public class PageLevelLink_Lock : BaseUILayout<PageLevelLink_Lock.Setting>
{
    // 잠금 영역 전체 버튼 → setting.OnClickEvent (PageLevelLink.OnClickLock)
    [SerializeField] private Button Btn;

    [Header("락/언락 연출")]
    // 해금 애니메이션 (클립의 애니메이션 이벤트로 OnCompleteUnclock 을 호출하도록 세팅되어 있음)
    [SerializeField] private Animation Anim_Unlock;
    // 자물쇠 아이콘 (대기 상태에서 켜짐)
    [SerializeField] private GameObject ObjIcon;
    // 해금 이펙트 (코드에서는 SetUnlock 에서 끄기만 한다)
    [SerializeField] private GameObject ObjFxUnlock;

    
    // InitUI 중복 실행 방지
    private bool IsInit = false;

    /// <summary>
    /// 잠금 버튼 바인딩 (1회). PageLevelLink.InItUI 에서 호출. BUTTONDELAYTIME 연타 방지, AddTo(this) 로 파괴 시 해제.
    /// </summary>
    public override void InitUI()
    {
        if( IsInit ) return;
        IsInit = true;

        if( Btn != null )
        {
            Btn.BindToOnClick(_=>
            {
                if( setting.OnClickEvent != null )
                {
                    setting.OnClickEvent.Invoke();
                }
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }
    }

    /// <summary>
    /// 잠금 UI 표시 갱신.
    /// 1) 연출 상태 초기화(SetUnlock) 2) LOCK 일 때만 자신을 켬 3) IsUnlockPlay 면 해금 연출 재생
    /// 호출: PageLevelLink.SetUI / LevelLinkSelectGDID·LevelLinkUnLockSlot 구독
    /// </summary>
    public override void SetUI()
    {
        // 재생 중이던 애니메이션을 멈추고 아이콘 on / 이펙트 off 의 대기 상태로 되돌린다
        SetUnlock();

        // LOCK 이면 덮개 표시, 그 외 상태는 GameObject 비활성화
        // (IsUnlockPlay 일 때는 PageLevelLink 이 상태를 LOCK 으로 강제해서 넘기므로 켜진 상태로 연출이 재생된다)
        switch(setting.CurrentState)
        {
            case LevelLink_Slot_State.LOCK:
                SetEnable(true);
                break;
            default:
                SetEnable( false);
                break;
        }

        if( setting.IsUnlockPlay)
        {
            PlayUnLock();
        }
    }

    /// <summary>
    /// 해금 연출을 대기 상태로 초기화한다. (애니메이션 정지, 자물쇠 아이콘 on, 해금 이펙트 off)
    /// 이름은 SetUnlock 이지만 "해금 연출 리셋" 역할이다.
    /// </summary>
    private void SetUnlock()
    {
        if( Anim_Unlock == null ) return;

        Anim_Unlock.Stop();

        if( ObjIcon != null ) ObjIcon.SetActive(true);
        if( ObjFxUnlock != null ) ObjFxUnlock.SetActive(false);
    }

    /// <summary>
    /// 해금 애니메이션 + 해금 효과음 재생. 애니메이션이 끝나면 클립 이벤트로 OnCompleteUnclock 이 호출된다.
    /// [참고] Anim_Unlock 이 없으면 바로 return 하므로 OnCompleteUnclock 이 호출되지 않아 해금 후 화면 갱신이 일어나지 않는다
    /// </summary>
    private void PlayUnLock()
    {
        if( Anim_Unlock == null ) return;
        Anim_Unlock.Play();

        CSoundControl.Instance.FxPlay(SOUND_ID.SFX_LEVELLINK_SLOT_UNLOCK);

    }

    /// <summary>
    /// 애니메이션 clip event에 세팅
    /// (해금 애니메이션 종료 시점) → setting.OnCompleteUnlock = PageLevelLink.OnCompleteUnlockAni → LevelLinkSelectGDID 발행
    /// 코드에서 직접 호출하는 곳은 없고, 애니메이션 이벤트에서 함수 이름으로 호출되므로 public 이다.
    /// </summary>
    public void OnCompleteUnclock()
    {
        if( setting.OnCompleteUnlock != null )
        {
            setting.OnCompleteUnlock.Invoke();
        }
    }

    /// <summary>
    /// 잠금 UI 데이터. PageLevelLink.SetData_LockUI 에서 생성.
    /// </summary>
    public class Setting
    {
        // 현재 LevelLink 상태 (LOCK 일 때만 표시). 해금 연출 시에는 LOCK 으로 강제된 값
        public LevelLink_Slot_State CurrentState;
        // 잠금 영역 클릭 → PageLevelLink.OnClickLock
        public Action OnClickEvent;
        // 해금 애니메이션 종료 → PageLevelLink.OnCompleteUnlockAni
        public Action OnCompleteUnlock;

        // true 면 SetUI 에서 해금 애니메이션을 재생 (슬롯 추가 API 성공 직후에만 true)
        public bool IsUnlockPlay = false;

    }
}
