using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LevelLink 슬롯 해금 팝업(PopupLevelLinkUnlock)의 "해금 조건 미션 목록" 영역.
/// - 부모: PopupLevelLinkUnlock (InitUI / SetData / SetUI를 부모가 호출)
/// - 자식: 프리팹에 미리 배치된 ObjLevelLinkUnlock_MissionUI 슬롯들. 미션 개수만큼 앞에서부터 데이터를 넣고 켠다.
/// - Setting: LevelLink ID + 해금 조건 퀘스트 테이블 목록. 진행도는 서버 미션 데이터(LevelLinkManager)에서 직접 조회한다.
/// </summary>
public class ObjLevelLinkUnlock_MissionListUI : BaseUILayout<ObjLevelLinkUnlock_MissionListUI.Setting>
{
    // 미션 한 줄씩 표시하는 고정 슬롯 UI 목록
    [SerializeField] private List<ObjLevelLinkUnlock_MissionUI> MissionUIList;

    // InitUI 중복 실행 방지
    private bool IsInit = false;

    /// <summary>
    /// 부모 팝업의 InitUI(OnAwake 시점)에서 1회 호출. 모든 미션 슬롯을 꺼둔다.
    /// </summary>
    public override void InitUI()
    {
        if( IsInit ) return;
        IsInit = true;

        MissionUIList.ForEach(ui=>ui.SetEnable(false));
    }

    /// <summary>
    /// 퀘스트 목록을 슬롯에 표시한다. 부모 팝업의 SetUI에서 호출된다.
    /// 각 미션의 제목, "현재/목표" 진행도, 달성 여부(On/Off 표시)를 세팅한다.
    /// </summary>
    public override void SetUI()
    {
        // ForEach((idx, ui)): 프로젝트 확장 메서드 — 인덱스와 요소를 함께 넘겨준다
        MissionUIList.ForEach((idx,ui)=>
        {
            // 퀘스트가 있는 인덱스의 슬롯만 처리 (else가 없으므로 남는 슬롯은 InitUI에서 꺼둔 상태 그대로 유지)
            if( setting.QuestList.Count > idx )
            {
                var questData = setting.QuestList[idx];

                // 서버에서 받은 미션 진행 데이터 조회: SMissionList[LevelLinkID][퀘스트ID] (없으면 null)
                var sMissionData = LevelLinkManager.Instance.GetLevelLinkMissionListData(setting.LevelLinkID, questData.quest_id.ToInt32());

                // 진행도 표시용 파라미터: [0] 현재 진행도, [1] 목표치 → MissionUI에서 "{0}/{1}"로 출력
                List<string> valueList = new List<string>();
                // valueList.Add(questData.quest_condition_value.ToString());
                valueList.Add(sMissionData.cur_point.ToString());
                valueList.Add(sMissionData.end_point.ToString());

                ui.SetData(new ObjLevelLinkUnlock_MissionUI.Setting()
                {
                    // 미션 제목 문자열 키 (MissionUI에서 문자열 테이블로 변환)
                    TitleKey = questData.quest_title_str,
                    TitleParam = valueList,
                    // 현재 진행도가 목표치 이상이면 달성
                    IsClear = sMissionData.end_point <= sMissionData.cur_point, //서버값과 비교한다

                });

                ui.SetUI();
                ui.SetEnable(true);
            }
        });
    }

    /// <summary>
    /// 미션 목록 영역 입력 데이터. PopupLevelLinkUnlock.SetUIData에서 생성된다.
    /// </summary>
    public class Setting
    {
        // LevelLink 테이블의 level_link_id — 서버 미션 데이터 조회의 1차 키
        public int LevelLinkID;
        // 해금 조건 퀘스트 테이블 목록 (level_link_condition_index 그룹으로 조회한 결과)
        public List<QuestListData> QuestList;
    }

}
