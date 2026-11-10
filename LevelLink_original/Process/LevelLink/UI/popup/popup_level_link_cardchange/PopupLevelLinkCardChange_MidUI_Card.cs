using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 카드 등록 / 해제 확인 팝업(PopupLevelLinkCardChange)의 카드 비교 영역 : 왼쪽 = 변경 전, 오른쪽 = 변경 후
/// - 부모 : PopupLevelLinkCardChange (InitUI 는 OnAwake, SetData / SetUI 는 부모 SetUI 에서 호출)
/// - ObjLevelLinkCard.Setting 의미
///   IsOrigLevel      : true = 카드 자체 레벨 표시, false = LevelLink 레벨(CardLevelLinkLevel) 표시
///   LevelLinkLevel     : 0 보다 크면 LevelLink 배경, 0 이하면 비활성 배경
///   CardLevelLinkLevel : LevelLink 레벨 표시 텍스트 값
/// - 등록 모드 : 왼쪽 = 원래 레벨 / 오른쪽 = LevelLink 레벨 적용 (카드 레벨이 더 높으면 원래 레벨 유지)
/// - 해제 모드 : 왼쪽 = 현재 LevelLink 적용 상태 (카드 레벨이 더 높으면 원래 레벨) / 오른쪽 = 원래 레벨
/// </summary>
public class PopupLevelLinkCardChange_MidUI_Card : BaseUILayout<PopupLevelLinkCardChange_MidUI_Card.Setting>
{
    //변경 전 카드
    [SerializeField] private ObjLevelLinkCard CardLeft;
    //변경 후 카드
    [SerializeField] private ObjLevelLinkCard CardRight;

    /// <summary>
    /// 카드 UI 2장 초기화 (부모 InitUI 에서 1회)
    /// </summary>
    public override void InitUI()
    {
        base.InitUI();

        if( CardLeft != null )
        {
            CardLeft.InitUI();
        }

        if( CardRight != null )
        {
            CardRight.InitUI();
        }
    }

    /// <summary>
    /// 모드(등록 / 해제)와 IsOriginLV 에 따라 왼쪽 / 오른쪽 카드의 표시 방식을 정해 Setting 을 넘긴다
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);
        //카드 서버 정보 (null 이면 아래 분기를 건너뛰고 기본값으로 표시)
        var cardInfo = CardInventoryManager.Instance.GetCardInfoByGDID(setting.CardID);

        //기본값 : 등록 모드 → 왼쪽 원래 레벨(true) / 오른쪽 LevelLink 레벨(false), 해제 모드 → 반대
        bool originLV_Left= setting.IsLevelLinkUnregister? false: true;
        bool originLV_Right = setting.IsLevelLinkUnregister? true: false;
        //카드에 표시할 LevelLink 레벨 값
        int cardLevelLinkLevel = setting.LevelLinkLV;

        if( cardInfo != null )
        {
            //등록해제
            if( setting.IsLevelLinkUnregister)
            {
                //왼쪽(현재 상태) : 카드 레벨이 LevelLink 레벨 이상이면 원래 레벨, 아니면 LevelLink 레벨 표시
                originLV_Left = setting.IsOriginLV;
                // cardLevelLinkLevel = cardInfo.level_link_lv;

            }
            else
            {
                //카드 레벨 확인 한다
                //오른쪽(등록 후) : 카드 레벨이 LevelLink 레벨 이상이면 원래 레벨, 아니면 LevelLink 레벨 표시
                originLV_Right = setting.IsOriginLV;

                var cardGradeData = CCardDataManager.Instance.GetCardGradeData(cardInfo.grade);

                //레벨 조율
                //카드 등급 최대 레벨로 LevelLink 레벨 제한
                //(setting.LevelLinkLV 는 PopupLevelLinkCardChange.Setting 생성자에서 이미 같은 기준으로 조정된 값이라 결과는 같다)
                if( setting.LevelLinkLV > cardGradeData.cardgrade_max_card_lv )
                {
                    cardLevelLinkLevel = cardGradeData.cardgrade_max_card_lv;
                }
                else
                {
                    cardLevelLinkLevel = setting.LevelLinkLV;
                }
            }

        }

        //왼쪽 = 변경 전. LevelLinkLevel : 해제 모드면 LevelLink 레벨(LevelLink 배경), 등록 모드면 0(비활성 배경)
        if( CardLeft != null )
        {
            CardLeft.SetData(new ObjLevelLinkCard.Setting()
            {
                CardID = setting.CardID,
                IsOrigLevel = originLV_Left,
                LevelLinkLevel = setting.IsLevelLinkUnregister? setting.LevelLinkLV : 0,
                CardLevelLinkLevel = cardLevelLinkLevel,
            });
        }

        //오른쪽 = 변경 후. LevelLinkLevel : 등록 모드면 LevelLink 레벨(LevelLink 배경), 해제 모드면 0(비활성 배경)
        if( CardRight != null )
        {
            CardRight.SetData(new ObjLevelLinkCard.Setting()
            {
                CardID = setting.CardID,
                IsOrigLevel = originLV_Right,
                LevelLinkLevel = setting.IsLevelLinkUnregister? 0 : setting.LevelLinkLV,
                CardLevelLinkLevel = cardLevelLinkLevel,
            });
        }
    }

    /// <summary>
    /// 카드 2장 화면 반영
    /// </summary>
    public override void SetUI()
    {
        base.SetUI();

        if( CardLeft != null )
        {
            CardLeft.SetUI();
        }

        if( CardRight != null )
        {
            CardRight.SetUI();
        }
    }

    /// <summary>
    /// 카드 비교 영역 입력 데이터 (PopupLevelLinkCardChange.SetUI 에서 생성)
    /// </summary>
    public class Setting
    {
        //대상 카드 GDID
        public int CardID;
        //LevelLink 레벨 (최대 레벨로 조정된 값)
        public int LevelLinkLV;
        
        //true = 등록 해제 모드
        public bool IsLevelLinkUnregister = false;
        //true = 카드 자체 레벨이 LevelLink 레벨 이상 (LevelLink 레벨 대신 원래 레벨 표시)
        public bool IsOriginLV = false;
    }
}
