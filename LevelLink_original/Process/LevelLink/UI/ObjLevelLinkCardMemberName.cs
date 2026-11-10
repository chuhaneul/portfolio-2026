using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LevelLink 카드 슬롯 아래(또는 위)에 표시되는 "멤버 조건 이름" 라벨.
/// - 부모: ObjLevelLinkCard (MemberNameUI 필드). 자식 레이아웃 없음.
/// - Setting 은 ObjLevelLinkCard.SetMemberName 에서 슬롯의 MemberType(테이블 level_link_group_con)으로 채워진다.
/// - 특정 멤버만 배치할 수 있는 슬롯일 때만 멤버 이름을 보여주고, 그룹 전체/조건 없음 슬롯이면 숨긴다.
/// </summary>
public class ObjLevelLinkCardMemberName : BaseUILayout<ObjLevelLinkCardMemberName.Setting>
{
    // 멤버 이름 텍스트
    [SerializeField] private Text TxtMemberName;

    /// <summary>
    /// 처음에는 숨겨둔다. (ObjLevelLinkCard.InitUI 에서 호출)
    /// </summary>
    public override void InitUI()
    {
        SetEnable(false);
    }

    /// <summary>
    /// MemberType 에 따라 표시 여부 결정. (ObjLevelLinkCard.SetMemberName 에서 SetData 직후 호출)
    /// </summary>
    public override void SetUI()
    {
        // 그룹 전체 / NONE / CNT / CNT_ALL(개수용 값) → 특정 멤버 조건이 아니므로 숨김
        // 그 외(개별 멤버 값) → 멤버 이름 문자열을 넣고 표시
        switch(setting.MemberType)
        {
            case MEMBER_TYPE.BTS:
            case MEMBER_TYPE.NONE:
            case MEMBER_TYPE.CNT:
            case MEMBER_TYPE.CNT_ALL:
                SetEnable(false);
            break;
            default:
                SetText();
                SetEnable(true);
            break;

        }
    }

    // MEMBER_TYPE → 스트링 테이블의 멤버 이름 문자열 (ToMemberString 확장 메서드)
    private void SetText()
    {
        if( TxtMemberName != null )
        {
            TxtMemberName.text = setting.MemberType.ToMemberString();
        }

    }

    /// <summary>
    /// 멤버 이름 라벨 데이터. ObjLevelLinkCard.SetMemberName 에서 생성.
    /// </summary>
    public class Setting
    {
        // 슬롯의 멤버 조건
        public MEMBER_TYPE MemberType;
    }
}
