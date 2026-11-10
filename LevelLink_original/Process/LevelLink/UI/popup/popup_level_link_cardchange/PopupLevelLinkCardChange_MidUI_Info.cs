using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 등록 / 해제 확인 팝업(PopupLevelLinkCardChange)의 정보 영역
/// - 레벨 변화 문구(TxtLevelInfo), 안내 메시지(TxtMessage), 레벨 변화 정보 오브젝트(ObjLevelInfo) 표시 여부를 담당
/// - 부모 : PopupLevelLinkCardChange (SetData / SetUI 는 부모 SetUI 에서 호출, InitUI 는 오버라이드하지 않음)
/// </summary>
public class PopupLevelLinkCardChange_MidUI_Info : BaseUILayout<PopupLevelLinkCardChange_MidUI_Info.Setting>
{
    //레벨 변화 정보 묶음 오브젝트 (카드 레벨 < LevelLink 레벨 일 때만 표시)
    [SerializeField] private GameObject ObjLevelInfo;
    //레벨 변화 문구 (levellink_level_change)
    [SerializeField] private Text TxtLevelInfo;
    //안내 메시지 (등록 / 초과 / 해제)
    [SerializeField] private Text TxtMessage;

    /// <summary>
    /// 문구 / 메시지 / 레벨 정보 표시 여부 갱신 (base.SetUI 는 호출하지 않음 - 비어 있는 메서드)
    /// </summary>
    public override void SetUI()
    {
        SetTextLevelInfo();
        SetTextMessage();
        SetEnableObjLevelInfo();
    }

    /// <summary>
    /// 레벨 변화 문구 : levellink_level_change 포맷에 (최대 카드 레벨, 변경 전 레벨, 변경 후 레벨) 순서로 넣는다
    /// - 해제 : LevelLink 레벨 → 카드 레벨, 등록 : 카드 레벨 → LevelLink 레벨
    /// </summary>
    private void SetTextLevelInfo()
    {
        if( TxtLevelInfo == null) return;
        string strMsg = CStringDataManager.Instance.GetStringData("levellink_level_change");

        if(setting.IsLevelLinkUnregister )
        {
            TxtLevelInfo.text = string.Format(strMsg, setting.MaxCardLevel, setting.LevelLinkLevel, setting.OriginLevel);
        }
        else
        {
            TxtLevelInfo.text = string.Format(strMsg, setting.MaxCardLevel, setting.OriginLevel, setting.LevelLinkLevel);
        }
    }

    /// <summary>
    /// 안내 메시지
    /// - 해제 모드 : levellink_popup_desc_05 에 쿨타임 시간 문자열을 넣어 표시
    /// - 등록 모드 : 카드 레벨 < LevelLink 레벨 → desc_03(등록), 카드 레벨 >= LevelLink 레벨 → desc_04(초과)
    /// </summary>
    private void SetTextMessage()
    {
        if( TxtMessage == null ) return;

        //등록 해제 메세지
        if( setting.IsLevelLinkUnregister )
        {
            //쿨타임 값을 시간 표시 문자열로 변환
            string strTime = Utility.GetTimeFormatStringDataMail(setting.CoolTime);
            string strMsg = CStringDataManager.Instance.GetStringData("levellink_popup_desc_05");
            TxtMessage.text = string.Format(strMsg, strTime);
            return;
        }

        //카드 자체 레벨이 LevelLink 레벨보다 낮으면 등록 시 레벨이 올라간다
        if( setting.OriginLevel < setting.LevelLinkLevel )
        {
            //등록 메세지
            TxtMessage.text = CStringDataManager.Instance.GetStringData("levellink_popup_desc_03");
        }
        else
        {
            //초과 메세지
            TxtMessage.text = CStringDataManager.Instance.GetStringData("levellink_popup_desc_04");
        }
    }
    /// <summary>
    /// 레벨 변화 정보 오브젝트 표시 여부 : 카드 레벨 < LevelLink 레벨(레벨이 실제로 바뀌는 경우)일 때만 표시
    /// - 해제 모드도 같은 기준을 쓴다 (해제 모드에서 항상 표시하던 코드는 주석 처리되어 있음)
    /// </summary>
    private void SetEnableObjLevelInfo()
    {
        if( ObjLevelInfo == null ) return;

        // if( setting.IsLevelLinkUnregister )
        // {
        //     ObjLevelInfo.SetActive(true);
        //     return;
        // }

        if( setting.OriginLevel >= setting.LevelLinkLevel)
        {
            ObjLevelInfo.SetActive(false);
        }
        else
        {
            ObjLevelInfo.SetActive(true);
        }
        
    }

    /// <summary>
    /// 정보 영역 입력 데이터 (PopupLevelLinkCardChange.SetUI 에서 생성)
    /// </summary>
    public class Setting
    {
        //카드 자체 레벨 (PopupLevelLinkCardChange.Setting.CardLevel)
        public int OriginLevel;
        //LevelLink 레벨 (최대 레벨로 조정된 값)
        public int LevelLinkLevel;
        //카드 등급 최대 레벨
        public int MaxCardLevel;

        //true = 등록 해제 모드
        public bool IsLevelLinkUnregister = false;
        //해제 모드 안내 문구에 들어갈 쿨타임
        public long CoolTime = 0;



    }
}
