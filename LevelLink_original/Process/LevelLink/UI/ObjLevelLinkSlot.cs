using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;



/// <summary>
/// 하단 LevelLink 슬롯 목록(PageLevelLink_Bottom 스크롤)의 아이템 1개 = LevelLink 1개.
/// - 부모: PageLevelLink_Bottom (LoopListView2 아이템, OnGetItemByIndex 에서 생성/재활용)
/// - 자식: TimerUI(ObjLevelLinkSlot_Timer, 쿨타임일 때만 표시되는 미니 타이머)
/// - Setting 은 PageLevelLink_Bottom.OnGetItemByIndex 에서 테이블 정보 + 서버 남은 시간 + 상태 + 선택 여부로 채워진다.
/// - 상태별 표시: EMPTY → ObjEmpty / LOCK → ObjLock / COOLTIME → TimerUI / SETTING → 셋 다 꺼짐
/// </summary>
public class ObjLevelLinkSlot : BaseUILayout<ObjLevelLinkSlot.Setting>
{
    // 슬롯 클릭 버튼 → setting.OnClickSlotEvent → PageLevelLink_Bottom → PageLevelLink.OnClickSlot (해당 LevelLink로 이동)
    [SerializeField] private Button BtnSlot;

    [SerializeField] private GameObject ObjSelect; //현재 선택된 슬롯
    // 빈 슬롯 표시 (EMPTY)
    [SerializeField] private GameObject ObjEmpty;
    // 잠금 표시 (LOCK)
    [SerializeField] private GameObject ObjLock;
    // 쿨타임 미니 타이머 (COOLTIME)
    [SerializeField] private ObjLevelLinkSlot_Timer TimerUI;

    // InitUI 중복 실행 방지 (스크롤 아이템 재활용 시에도 PageLevelLink_Bottom 이 IsInitHandlerCalled 로 1회만 호출)
    private bool IsInit = false;

    /// <summary>
    /// 슬롯 버튼 바인딩 (1회). BUTTONDELAYTIME 연타 방지, AddTo(this) 로 아이템 파괴 시 해제.
    /// </summary>
    public override void InitUI()
    {
        if( IsInit ) return;
        IsInit = true;

        if( BtnSlot != null )
        {
            BtnSlot.BindToOnClick(_=>
            {
                OnClickSlotEvent();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }
    }

    /// <summary>
    /// 선택 표시 + 상태별 표시 갱신. PageLevelLink_Bottom.OnGetItemByIndex 에서 SetData 직후 호출
    /// (최초 SetListItemCount 때와 RefreshAllShownItem 갱신 때 모두).
    /// </summary>
    public override void SetUI()
    {
        SetSelect();
        SetButtonState();
    }

    // 슬롯 클릭 → 테이블 정보와 현재 상태를 상위 콜백으로 전달
    private void OnClickSlotEvent()
    {
        setting.OnClickSlotEvent?.Invoke(setting.TableInfo, setting.CurrentState);
    }

    // 현재 보고 있는 LevelLink이면 선택 표시
    private void SetSelect()
    {
        if( ObjSelect != null )
        {
            ObjSelect.SetActive(setting.IsSelect);
        }
    }

    /// <summary>
    /// 상태에 따라 빈칸/잠금/타이머 표시를 켜고 끈다. 쿨타임이면 타이머 Setting 을 만들어 시작한다.
    /// </summary>
    private void SetButtonState()
    {
        if( ObjEmpty != null )
        {
            ObjEmpty.SetActive(setting.CurrentState == LevelLink_Slot_State.EMPTY);
        }

        if( ObjLock != null )
        {
            ObjLock.SetActive(setting.CurrentState == LevelLink_Slot_State.LOCK);
        }

        if( TimerUI != null )
        {
            // 쿨타임일 때만 타이머 GameObject 표시
            // [참고] 쿨타임이 아닐 때는 GameObject 만 끄고 TimerUI.Dispose 를 호출하지 않아 이전 TimeStream 구독이 남아 있을 수 있음
            //        (Mid_TopUI 는 비쿨타임 시 Dispose 를 호출함). 구독은 TimeStream 완료 또는 아이템 파괴(AddTo) 시 정리된다.
            TimerUI.gameObject.SetActive(setting.CurrentState == LevelLink_Slot_State.COOLTIME);

            if( setting.CurrentState == LevelLink_Slot_State.COOLTIME )
            {
                TimerUI.SetData(new ObjLevelLinkSlot_Timer.Setting()
                {
                    // 항상 기존 타이머 구독을 먼저 끊는다 (스크롤 아이템은 다른 LevelLink로 재활용될 수 있음)
                    IsReset = true,
                    RemainTime = setting.LevelLink_CoolTime,
                    TotalTime = setting.TableInfo.level_link_cooldown,
                    // 중단 쿨타임 타이머(Mid_TopUI)와 같은 키 → 현재 LevelLink이면 같은 TimeStream 을 함께 구독
                    STR_TIMER_NAME = string.Format(LevelLinkManager.LEVELLINK_SLOT_TIME_KEY_FORMAT, setting.TableInfo.level_link_id),
                });

                TimerUI.SetUI();
            }
        }
    }

    /// <summary>
    /// 슬롯 아이템 데이터. PageLevelLink_Bottom.OnGetItemByIndex 에서 생성.
    /// </summary>
    public class Setting
    {
        // 현재 보고 있는 LevelLink인지
        public bool IsSelect = false;
        // LevelLink 테이블 정보 (level_link_id, level_link_cooldown 등)
        public LevelLinkList TableInfo;
        // 남은 쿨타임(초, 서버 remain_time)
        public int LevelLink_CoolTime = 0;

        // 슬롯 클릭 콜백 (테이블 정보, 상태) → PageLevelLink_Bottom.OnClickSlotEvent
        public Action<LevelLinkList, LevelLink_Slot_State> OnClickSlotEvent;

        // 서버 데이터 기준 상태 (EMPTY / SETTING / COOLTIME / LOCK)
        public LevelLink_Slot_State CurrentState = LevelLink_Slot_State.EMPTY;
    }
}
