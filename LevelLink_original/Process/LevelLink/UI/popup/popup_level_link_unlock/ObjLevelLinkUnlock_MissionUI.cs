using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LevelLink 슬롯 해금 팝업의 미션 한 줄 UI.
/// - 부모: ObjLevelLinkUnlock_MissionListUI (SetData → SetUI → SetEnable(true) 순으로 호출)
/// - 표시 내용: 미션 제목, 진행도 "현재/목표", 달성 여부(ObjOn/ObjOff 토글)
/// </summary>
public class ObjLevelLinkUnlock_MissionUI : BaseUILayout<ObjLevelLinkUnlock_MissionUI.Setting>
{
    // 미션 제목 텍스트
    [SerializeField] private Text TxtTitle;
    // 달성 시 켜지는 오브젝트 (예: 체크 표시)
    [SerializeField] private GameObject ObjOn;
    // 미달성 시 켜지는 오브젝트
    [SerializeField] private GameObject ObjOff;

    // 진행도 텍스트 ("현재/목표")
    [SerializeField] private Text TxtCnt;

    /// <summary>
    /// 제목 → 달성 표시 → 진행도 순으로 화면을 갱신한다.
    /// </summary>
    public override void SetUI()
    {
        SetTitle();
        SetOnOff();
        SetCount();
    }

    /// <summary>
    /// 문자열 테이블에서 TitleKey에 해당하는 미션 제목을 가져와 표시한다.
    /// </summary>
    private void SetTitle()
    {
        if( TxtTitle == null ) return;

        TxtTitle.text = CStringDataManager.Instance.GetStringData(setting.TitleKey);
    }

    /// <summary>
    /// TitleParam([0] 현재 진행도, [1] 목표치)을 "{0}/{1}" 형식으로 표시한다.
    /// string.Format의 params object[] 인자로 배열을 넘기므로 원소가 2개 미만이면 FormatException이 난다.
    /// </summary>
    private void SetCount()
    {
        if( TxtCnt == null) return;

        TxtCnt.text = string.Format("{0}/{1}", setting.TitleParam.ToArray());
    }

    /// <summary>
    /// 달성 여부에 따라 ObjOn / ObjOff 중 하나만 켠다.
    /// </summary>
    private void SetOnOff()
    {
        if( ObjOn != null )
        {
            ObjOn.SetActive(setting.IsClear);
        }

        if( ObjOff != null )
        {
            ObjOff.SetActive(!setting.IsClear);

        }
    }

    /// <summary>
    /// 미션 한 줄 입력 데이터. ObjLevelLinkUnlock_MissionListUI.SetUI에서 생성된다.
    /// </summary>
    public class Setting
    {
        // 미션 달성 여부 (서버 진행도 cur_point >= end_point)
        public bool IsClear = false;
        // 미션 제목 문자열 키 (퀘스트 테이블 quest_title_str)
        public string TitleKey;
        // 진행도 표시 값 목록: [0] 현재 진행도, [1] 목표치
        public List<string> TitleParam;

    }

}
