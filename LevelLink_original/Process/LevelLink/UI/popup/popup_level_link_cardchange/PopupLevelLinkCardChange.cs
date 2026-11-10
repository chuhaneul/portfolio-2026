using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UniRx;
using Game.RestAPI;

/// <summary>
/// LevelLink 슬롯 카드 등록 / 등록 해제 확인 팝업 (카드 레벨이 어떻게 바뀌는지 보여준다)
/// - 여는 곳 1) 등록 : PopupLevelLinkSelectcard 확인 버튼(처음 선택과 다른 카드를 골랐을 때) → OpenPopupLevelLinkCardChange()
///             Setting(선택 카드 GDID, LevelLink 레벨), IsLevelLinkUnregister = false
///             → IsSuccess 면 카드 선택 팝업도 성공으로 닫히고, PageLevelLink 이 RequestLevelLinkCardSave(선택 카드) API 호출
/// - 여는 곳 2) 해제 : PageLevelLink.OnClickLevelLinkCard (슬롯에 카드가 등록되어 있을 때) → OpenPopupLevelLinkCardRemove()
///             Setting(등록된 카드 GDID = sData.slot, LevelLink 레벨 = sData.lv), IsLevelLinkUnregister = true, CoolTime = 테이블 level_link_cooldown
///             → IsSuccess 면 PageLevelLink 이 RequestLevelLinkCardRemove() API 호출
/// - Result : 확인 버튼 → IsSuccess = true, 닫기 버튼 / 백키 → false
/// - 화면 : TopUI(제목) / MidUI_Card(왼쪽 = 변경 전, 오른쪽 = 변경 후 카드) / MidUI_Info(레벨 변화 문구, 안내 메시지)
/// - 이 팝업 자체는 서버 통신을 하지 않는다
/// </summary>
public class PopupLevelLinkCardChange : CPopupUI<PopupLevelLinkCardChange.Setting, PopupLevelLinkCardChange.Result>
{
    //제목 영역 (등록 해제 / 안내)
    [SerializeField] private PopupLevelLinkCardChange_TopUI TopUI;
    //변경 전 / 후 카드 2장 표시 영역
    [SerializeField] private PopupLevelLinkCardChange_MidUI_Card MidUI_Card;
    //레벨 변화 문구 + 안내 메시지 영역
    [SerializeField] private PopupLevelLinkCardChange_MidUI_Info MidUI_Info;

    //확인 버튼 (등록 / 해제 진행)
    [SerializeField] private Button BtnOK;
    //닫기(취소) 버튼
    [SerializeField] private Button BtnClose; 

    //버튼 바인딩 중복 방지
    private bool IsInit = false;

    /// <summary>
    /// 팝업 생성 시 1회 : 결과 Subject / 기본 결과(실패) 생성, DISABLE_HIDE 설정, 버튼 바인딩
    /// </summary>
    protected override void OnAwake()
    {
        base.OnAwake();

        //호출측에 결과를 전달할 Subject. 기본은 IsSuccess = false
        subject = new Subject<Result>();
        result = new Result {IsSuccess = false};

        Flags = PopupFlags.DISABLE_HIDE;
        
        InitUI();
        
    }

    /// <summary>
    /// 호출측이 Setting 전달 시 호출 → 하위 UI 구성
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);

        SetUI();
    }

    /// <summary>
    /// 버튼 바인딩 및 카드 / 정보 영역 초기화 (OnAwake 에서 1회)
    /// </summary>
    private void InitUI()
    {
        if( IsInit ) return;
        IsInit = true;

        if( BtnOK != null )
        {
            //확인 : 성공으로 표시 후 닫기 → OnClose 에서 호출측으로 결과 전달. BUTTONDELAYTIME 동안 재클릭 방지
            BtnOK.BindToOnClick(_=>
            {
                result.IsSuccess = true;
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnClose != null )
        {
            //닫기 : IsSuccess = false 그대로 닫기 → 호출측은 아무 API 도 호출하지 않는다
            BtnClose.BindToOnClick(_=>
            {
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( MidUI_Card != null )
        {
            MidUI_Card.InitUI();
        }

        if( MidUI_Info != null )
        {
            MidUI_Info.InitUI();
        }
    }

    /// <summary>
    /// setting 값으로 하위 UI 3개에 Setting 을 만들어 넘기고 바로 화면 반영 (SetData 에서 호출)
    /// </summary>
    private void SetUI()
    {
        //제목 : 해제 모드면 "등록 해제", 등록 모드면 "안내"
        if( TopUI != null )
        {
            TopUI.SetData(new PopupLevelLinkCardChange_TopUI.Setting()
            {
                IsLevelLinkUnregister = setting.IsLevelLinkUnregister,
            });

            TopUI.SetUI();
        }

        //변경 전 / 후 카드
        if( MidUI_Card != null )
        {

            MidUI_Card.SetData(new PopupLevelLinkCardChange_MidUI_Card.Setting()
            {
                CardID = setting.CardID,
                LevelLinkLV = setting.LevelLinkLV,
                IsLevelLinkUnregister = setting.IsLevelLinkUnregister,
                IsOriginLV = setting.IsOriginLV,
            });

            MidUI_Card.SetUI();
        }

        //레벨 변화 문구 / 안내 메시지
        if( MidUI_Info != null )
        {
            MidUI_Info.SetData(new PopupLevelLinkCardChange_MidUI_Info.Setting()
            {
                //카드 자체 레벨
                OriginLevel = setting.CardLevel,
                LevelLinkLevel = setting.LevelLinkLV,
                IsLevelLinkUnregister = setting.IsLevelLinkUnregister,
                CoolTime = setting.CoolTime,
                MaxCardLevel = setting.MaxCardLevel,
            });

            MidUI_Info.SetUI();
        }

    }


    /// <summary>
    /// 카드 등록 / 해제 확인 팝업 입력 데이터
    /// - 생성자(카드 GDID, LevelLink 레벨)에서 카드 정보 / 최대 레벨 / 레벨 조정 / IsOriginLV 를 계산한다
    /// - IsLevelLinkUnregister, CoolTime 은 생성자 뒤 객체 초기화자({ ... })로 호출측이 지정한다
    /// </summary>
    public class Setting
    {
        //대상 카드 GDID (등록 : 선택한 카드, 해제 : 현재 슬롯에 등록된 카드)
        public int CardID {get; private set;}
        //LevelLink 레벨. 카드 등급 최대 레벨보다 크면 최대 레벨로 조정된 값
        public int LevelLinkLV {get; private set;}
        //true = 등록 해제 모드, false = 등록 모드
        public bool IsLevelLinkUnregister = false;
        //true = 카드 자체 레벨이 (조정된) LevelLink 레벨 이상 → LevelLink 레벨이 적용되지 않고 원래 레벨이 유지되는 카드
        public bool IsOriginLV {get; private set;} = false;
        //해제 모드에서 안내 문구에 넣을 쿨타임 (테이블 level_link_cooldown 값)
        public long CoolTime = 0;
        //보유 카드 서버 정보 (레벨, 등급 등)
        public CardList SCardInfo{get; private set;}

        //카드 등급의 최대 카드 레벨 (cardgrade_max_card_lv)
        public int MaxCardLevel {get; private set;}
        //카드 자체 레벨. 카드 정보가 없으면 1
        public int CardLevel 
        {
            get
            {
                if( SCardInfo != null )
                {
                    return SCardInfo.lv;
                }

                return 1;
            }
        }
        
        /// <summary>
        /// cardID : 대상 카드 GDID, levelLinkLV : LevelLink의 LevelLink 레벨
        /// </summary>
        public Setting(int cardID, int levelLinkLV)
        {
            CardID = cardID;

            //인벤토리에서 카드 서버 정보 조회
            SCardInfo = CardInventoryManager.Instance.GetCardInfoByGDID(cardID);

            //카드 등급 테이블에서 최대 카드 레벨 조회
            // [참고] SCardInfo 를 null 체크 없이 사용하고 아래 IsOriginLV 계산에서야 null 체크를 하므로, 카드 정보가 없으면 여기서 NullReferenceException 이 난다
            MaxCardLevel = CCardDataManager.Instance.GetCardGradeData(SCardInfo.grade).cardgrade_max_card_lv;
            
            //레벨조율
            //LevelLink 레벨이 카드 등급 최대 레벨을 넘으면 최대 레벨까지만 적용
            if( levelLinkLV > MaxCardLevel)
            {
                LevelLinkLV = MaxCardLevel;
            }
            else
            {
                LevelLinkLV = levelLinkLV;
            }


            //카드 자체 레벨이 조정된 LevelLink 레벨 이상이면 원래 레벨 유지 카드
            if( SCardInfo != null )
            {
                IsOriginLV = SCardInfo.lv >= LevelLinkLV;
            }
            
        }
        
    }

    /// <summary>
    /// 카드 등록 / 해제 확인 결과
    /// </summary>
    public class Result
    {
        //true = 확인 버튼 (호출측이 등록 또는 해제 API 진행)
        public bool IsSuccess;
    }
}
