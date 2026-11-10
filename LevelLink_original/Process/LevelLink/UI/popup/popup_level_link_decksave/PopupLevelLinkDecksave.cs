using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System;

/// <summary>
/// LevelLink 덱 저장 확인 팝업 (LevelLink 레벨 하락 경고)
/// - 여는 곳 : PopupLevelLinkDeckManage.OnClickEventOK → OpenPopupDeckSave()
///   (새 배치의 LevelLink 레벨이 서버에 저장된 레벨보다 낮아질 때만 열린다 : IsChangeLevelLinkLowLV)
/// - Setting : BeforeLV(기존 LevelLink 레벨) / AfterLV(새 배치 기준 레벨 = 배치 카드 최저 레벨)
/// - Result  : 확인 버튼 → IsSuccess = true, 닫기 버튼 / 백키 → false
///             → 덱 편집 팝업은 IsSuccess 일 때만 자신의 결과를 성공으로 채우고 닫는다 (실제 저장 API 는 LevelLink 페이지가 호출)
/// - 이 팝업은 서버 통신을 하지 않는다
/// </summary>
public class PopupLevelLinkDecksave : CPopupUI<PopupLevelLinkDecksave.Setting, PopupLevelLinkDecksave.Result>
{
    //기존 LevelLink 레벨 텍스트
    [SerializeField] private Text TxtBefore;
    //변경 후 LevelLink 레벨 텍스트
    [SerializeField] private Text TxtAfter;
    //닫기(취소) 버튼
    [SerializeField] private Button BtnClose;
    //확인(저장) 버튼
    [SerializeField] private Button BtnOK;

    //버튼 바인딩 중복 방지
    private bool IsInit = false;
    
    /// <summary>
    /// 팝업 생성 시 1회 : 결과 Subject / 기본 결과(실패) 생성, DISABLE_HIDE 설정, 버튼 바인딩
    /// </summary>
    protected override void OnAwake()
    {
        base.OnAwake();
        //호출측(덱 편집 팝업)에 결과를 전달할 Subject. 기본은 IsSuccess = false
        subject = new Subject<Result>();
        result = new Result {IsSuccess = false};

        Flags = PopupFlags.DISABLE_HIDE;        
        InitUI();
    }

    /// <summary>
    /// 호출측이 Setting 전달 시 호출 → 레벨 텍스트 표시
    /// </summary>
    public override void SetData(Setting data)
    {
        base.SetData(data);

        SetUI();
    }

    /// <summary>
    /// 버튼 바인딩 (OnAwake 에서 1회)
    /// </summary>
    private void InitUI()
    {
        if( IsInit ) return;
        IsInit = true;

        if( BtnClose != null )
        {
            //닫기 : result 를 기본값(IsSuccess = false) 그대로 두고 닫는다 → 덱 편집 화면 유지
            //BUTTONDELAYTIME 타이머가 끝날 때까지 버튼 재클릭 방지
            BtnClose.BindToOnClick(_=>
            {
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

        if( BtnOK != null )
        {
            //확인 : 성공으로 표시 후 닫기 → OnClose 에서 결과가 덱 편집 팝업으로 전달된다
            BtnOK.BindToOnClick(_=>
            {
                result.IsSuccess = true;
                Close();
                return Observable.Timer(TimeSpan.FromSeconds(CDefines.BUTTONDELAYTIME)).AsUnitObservable();

            }).AddTo(this);
        }

    }

    /// <summary>
    /// 기존 / 변경 후 레벨 텍스트 표시
    /// </summary>
    private void SetUI()
    {
        SetTextBefore();
        SetTextAfter();
    }

    //기존 LevelLink 레벨
    private void SetTextBefore()
    {
        if( TxtBefore != null )
        {
            TxtBefore.text = setting.BeforeLV.ToString();
        }
    }

    //변경 후 LevelLink 레벨
    private void SetTextAfter()
    {
        if( TxtAfter != null )
        {
            TxtAfter.text = setting.AfterLV.ToString();
        }
    }

    /// <summary>
    /// 저장 확인 팝업 입력 데이터 (PopupLevelLinkDeckManage.OpenPopupDeckSave 에서 생성)
    /// </summary>
    public class Setting
    {
        //서버에 저장된 기존 LevelLink 레벨 (PopupLevelLinkDeckManage.Setting.OriginLevelLinkLv)
        public int BeforeLV = 0 ;
        //새 배치 기준 LevelLink 레벨 (PopupLevelLinkDeckManage.Setting.GetSlotLevel())
        public int AfterLV = 0;

    }

    /// <summary>
    /// 저장 확인 결과
    /// </summary>
    public class Result
    {
        //true = 레벨이 낮아져도 저장 진행
        public bool IsSuccess = false;

    }
}
