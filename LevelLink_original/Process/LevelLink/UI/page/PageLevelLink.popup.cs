using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Game.RestAPI;

/// <summary>
/// PageLevelLink 의 partial 파일 - LevelLink 페이지에서 여는 팝업들을 모아둔 곳.
/// 공통 패턴:
///  1) CPopupService 로 팝업을 열고 (필요하면 popup.SetData 로 Setting 전달)
///  2) popup.ShowAsObservable() 을 구독 → 팝업이 닫히면서 결과(res)를 1회 발행한다
///  3) 결과가 성공이면 LevelLinkManager 의 서버 요청 함수를 호출
///     (요청 성공 시 매니저가 LevelLinkSelectGDID / LevelLinkUnLockSlot 을 발행 → PageLevelLink.InItUI 구독에서 화면 갱신)
///  4) SingleAssignmentDisposable 에 구독을 담아두고, 결과를 받은 콜백 안에서 직접 Dispose 해서 1회성 구독으로 만든다
/// </summary>
public partial class PageLevelLink : PagePopup
{
    // 현재 화면에 보이는 LevelLink GDID (매니저의 CurrentPageGDID 를 그대로 읽는 단축 프로퍼티)
    // PageLevelLink.cs 의 RefreshCardData 구독에서도 사용한다
    private int CurrentGDID => LevelLinkManager.Instance.CurrentPageGDID;
    
    /// <summary>
    /// LevelLink 언락 팝업
    /// 호출: PageLevelLink.OnClickLock (잠금 화면 클릭, 이전 LevelLink가 해금된 경우)
    /// 팝업 안에서 미션/재료/골드 조건을 확인하고, 조건을 만족해 확인하면 IsSuccess = true 로 닫힌다.
    /// → RequestSlotAdd(슬롯 추가 API) → 성공 시 LevelLinkUnLockSlot 발행 → 잠금 UI 해금 연출
    /// </summary>
    private void OpenPopupLevelLinkUnlock()
    {
        // 현재 LevelLink의 테이블 데이터 (해금 조건/비용 정보가 들어있음)
        var data = CLevelLinkDataManager.Instance.GetLevelLinkData(CurrentGDID);
        
        //LevelLink 슬롯 추가 팝업 띄우기
        var popup = CCoreServices.GetCoreService<CPopupService>().OpenPopupLevelLinkUnlock(data);
        // 팝업 결과 1회 구독. 콜백에서 Dispose 하고, 추가로 AddTo(this) 로 페이지가 먼저 파괴되는 경우에도 해제된다.
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = popup.ShowAsObservable()
        .Subscribe(res=>
        {
            if( res.IsSuccess )
            {
                // 슬롯 추가 API. 결과 처리는 매니저 → LevelLinkUnLockSlot 메시지로 이어진다
                LevelLinkManager.Instance.RequestSlotAdd();
            }

            disposable.Dispose();

        }).AddTo(this);
    }

    /// <summary>
    /// LevelLink 카드 등록 할 수 있는 선택 팝업
    /// 호출: PageLevelLink.OnClickLevelLinkCard (LevelLink 레벨 1 이상 + 카드 미등록 + 쿨타임 없음)
    /// 팝업에서 카드를 골라 확인하면 RequestLevelLinkCardSave(카드 등록 API) → 성공 시 LevelLinkSelectGDID 발행 → 화면 갱신
    /// </summary>
    private void OpenPopupLevelLinkSelectcard()
    {
        // 현재 LevelLink 서버 데이터 (현재 등록 카드 slot, LevelLink 레벨 lv 를 팝업에 넘김)
        var sData = LevelLinkManager.Instance.GetLevelLinkData(CurrentGDID);

        //LevelLink에 등록할 카드 리스트 가져온다
        var cardIDList = LevelLinkManager.Instance.GetLevelLinkCardIDList();

        //LevelLink 카드 세팅 팝업 띄우기
        var popup = CCoreServices.GetCoreService<CPopupService>().OpenPopupLevelLinkSelectcard();
        // Setting(등록 후보 카드 GDID 리스트, 현재 등록된 카드 GDID, LevelLink 레벨)
        popup.SetData(new PopupLevelLinkSelectcard.Setting(cardIDList, sData.slot,sData.lv));
        
        // 팝업 결과 1회 구독 (콜백에서 Dispose + AddTo(this))
        SingleAssignmentDisposable dispose = new SingleAssignmentDisposable();
        dispose.Disposable = popup.ShowAsObservable()
        .Subscribe(res=>
        {
            if( res.IsSuccess ) 
            {
                // 카드 세팅 API 호출 한다
                LevelLinkManager.Instance.RequestLevelLinkCardSave(res.SelectCardID);
            }

            dispose.Dispose();

        }).AddTo(this);

    }


    /// <summary>
    /// LevelLink 카드 등록 해제 팝업
    /// 호출: PageLevelLink.OnClickLevelLinkCard (LevelLink 카드가 이미 등록된 상태에서 클릭)
    /// PopupLevelLinkCardChange 를 "등록 해제" 모드(IsLevelLinkUnregister = true)로 열고, 해제 시 걸리는 쿨타임(테이블 값)을 안내한다.
    /// 확인 시 RequestLevelLinkCardRemove(카드 해제 API) → 성공 시 LevelLinkSelectGDID 발행 → 화면 갱신
    /// </summary>
    private void OpenPopupLevelLinkCardRemove()
    {
        LevelLinkList data = CLevelLinkDataManager.Instance.GetLevelLinkData(CurrentGDID); //테이블 데이터
        var sData = LevelLinkManager.Instance.GetLevelLinkData(CurrentGDID);

        var popup = CCoreServices.GetCoreService<CPopupService>().OpenPopupLevelLinkCardChange();

        // Setting(등록된 카드 GDID, LevelLink 레벨) + 해제 모드 플래그 + 쿨타임(초)
        popup.SetData(new PopupLevelLinkCardChange.Setting( sData.slot, sData.lv )
        {
            IsLevelLinkUnregister = true,
            CoolTime = data.level_link_cooldown,
        });

        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = popup.ShowAsObservable()
        .Subscribe(result =>
        {
            if( result.IsSuccess )
            {
                LevelLinkManager.Instance.RequestLevelLinkCardRemove();
            }

            // 1회성 구독 해제 (이 구독은 AddTo(this) 가 없어 콜백에서의 Dispose 로만 해제된다)
            disposable.Dispose();
        });

    }

    /// <summary>
    /// 쿨타임 즉시 완료 팝업을 열기 전 준비 과정.
    /// 호출: PageLevelLink.OnClickLevelLinkCard (카드 미등록 + 쿨타임 진행 중)
    /// 1) 광고 목록 API(ADList)로 광고 시청 가능 정보를 최신화
    /// 2) 성공(e == 0)하면 매니저의 시간 단축 수단 리스트(광고 / 젬 즉시 완료 / 시간 단축 티켓들)를 다시 만들고
    /// 3) 즉시 완료 팝업을 연다. (실패하면 아무 팝업도 열지 않음)
    /// </summary>
    private void OpenPopupLevelLinkInstantCompleteProcess()
    {
        // API 응답 1회 구독, 콜백에서 Dispose
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable =  APIHelper.UserDataService.ADList()
        .Subscribe(res =>
        {
            if( res.e == 0 )
            {
                // 광고 → 즉시 완료(젬) → 티켓(sng_time_reduction1~3_id) 순으로 ShortTimeList 구성
                LevelLinkManager.Instance.InitShortTimeList();
                OpenPopupLevelLinkInstantComplete();
            }

            disposable.Dispose();
        });
        
    }

    /// <summary>
    /// 쿨타임 즉시 완료(시간 단축) 팝업. OpenPopupLevelLinkInstantCompleteProcess 에서만 호출된다.
    /// 팝업 안에서 광고/젬/티켓으로 시간 단축 API(ReqLevelLinkReduceTime)를 호출하고, 성공하면 IsSuccess = true 로 닫힌다.
    /// (API 응답에서 매니저의 서버 데이터 remain_time 이 이미 갱신된 상태)
    /// → LevelLinkPageUpdate_CoolTime 발행 → PageLevelLink 이 기존 TimeStream 을 지우고 새 남은 시간으로 타이머를 다시 그림
    /// </summary>
    private void OpenPopupLevelLinkInstantComplete()
    {

        LevelLinkList data = CLevelLinkDataManager.Instance.GetLevelLinkData(CurrentGDID); //테이블 데이터
        var sData = LevelLinkManager.Instance.GetLevelLinkData(CurrentGDID);

        var popup = CCoreServices.GetCoreService<CPopupService>().OpenPopupLevelLinkInstantcomplete();
        popup.SetData(new PopupLevelLinkInstantcomplete.Setting()
        {
            // 전체 쿨타임(테이블) / 남은 쿨타임(서버)
            TotalTime = data.level_link_cooldown,
            RemainTime = sData.remain_time,
            // 페이지의 쿨타임 타이머와 같은 TimeStream 키 ("LEVELLINK_SLOT_{gdid}") → 팝업 안 타이머도 같은 시간을 공유
            STR_TIMER_NAME = string.Format(LevelLinkManager.LEVELLINK_SLOT_TIME_KEY_FORMAT, CurrentGDID),
        });

        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = popup.ShowAsObservable()
        .Subscribe(result=>
        {
            if( result.IsSuccess )
            {
                //타이머 갱신
                CDebug.Log("타이머 갱신하세요");
                // 기본 인자 isEndTime = true 로 발행 (수신측 PageLevelLink 은 이 값을 사용하지 않음)
                MessageBroker.Default.Publish(new LevelLinkPageUpdate_CoolTime());
                
            }
            
            disposable.Dispose();
        });

    }



    /// <summary>
    /// LevelLink 하단 덱 편집 팝업
    /// 호출: PageLevelLink.OnClickCard (중단 하단의 덱 카드 아무거나 클릭)
    /// 현재 덱 구성(GroupList: 슬롯별 멤버 조건, SlotList: 슬롯별 배치 카드)을 넘겨 편집하게 하고,
    /// 저장하면 res.SlotList(편집 결과)로 RequestLevelLinkDeckSave(덱 저장 API) → 성공 시 토스트 + LevelLinkSelectGDID 발행
    /// </summary>
    private void OpenPopupLevelLinkDeckManage()
    {
        LevelLinkList data = CLevelLinkDataManager.Instance.GetLevelLinkData(CurrentGDID); //테이블 데이터
        LevelLinkList sData =  LevelLinkManager.Instance.GetLevelLinkData(CurrentGDID); //서버 데이터
        // 편집 전 LevelLink 레벨. 팝업에서 편집 후 레벨이 바뀌는지 비교할 때 기준값으로 쓰인다
        int originLevelLinkLV = 0;

        if( sData != null )
        {
            originLevelLinkLV = sData.lv;
        }

        var popup = CCoreServices.GetCoreService<CPopupService>().OpenPopupLevelLinkDeckManage();
        popup.SetData(new PopupLevelLinkDeckManage.Setting()
        {
            OriginLevelLinkLv = originLevelLinkLV,
            GroupList = CLevelLinkDataManager.Instance.GetLevelLinkGroupLists(data.level_link_group_index),
            SData = sData,
            // GetSlotGroupList: GroupList 에 있는 슬롯 순서(level_link_group_order)만 키로 넣은 { 순서 : 카드 GDID } 딕셔너리
            // (페이지 중단에 넘기는 SlotList 는 1~7 키를 항상 채우는 반면, 여기는 실제 사용 슬롯만 들어간다)
            SlotList = LevelLinkManager.Instance.GetSlotGroupList(CurrentGDID),
        });

        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = popup.ShowAsObservable()
        .Subscribe(res=>
        {
            if( res.IsSuccess)
            {
                //카드 덱 저장 API 호출
                LevelLinkManager.Instance.RequestLevelLinkDeckSave(res.SlotList);
            }

            disposable.Dispose();

        }).AddTo(this);
    }

    /// <summary>
    /// 초기화 팝업
    /// 호출: PageLevelLink.OnClickReset (LevelLink 레벨 1 이상일 때만 버튼이 활성)
    /// LevelLink 카드가 등록되어 있으면 "해제 시 쿨타임" 문구(desc_10)를, 아니면 일반 초기화 문구(desc_09)를 두 버튼 알림창으로 띄운다.
    /// 확인 시 RequestLevelLinkReset(덱 전체 초기화 API) → 성공 시 토스트 + LevelLinkSelectGDID 발행
    /// </summary>
    private void OpenPopupResetInfo()
    {
        LevelLinkList data = CLevelLinkDataManager.Instance.GetLevelLinkData(CurrentGDID); //테이블 데이터
        LevelLinkList sData =  LevelLinkManager.Instance.GetLevelLinkData(CurrentGDID); //서버 데이터

        //LevelLink 덱의 모든 카드가 해제됩니다. 정말 초기화할까요?
        string strValue = CStringDataManager.Instance.GetStringData("levellink_popup_desc_09");

        // LevelLink 카드가 등록된 상태라면 초기화 시 카드가 해제되며 쿨타임이 생기므로, 쿨타임 시간을 문구에 넣어 안내
        if( sData.slot > 0 )
        {
            // 쿨타임(초)을 표시용 시간 문자열로 변환해서 desc_10 의 {0} 에 넣는다
            string param = Utility.GetTimeFormatStringDataMail(data.level_link_cooldown);
            strValue = string.Format(CStringDataManager.Instance.GetStringData("levellink_popup_desc_10"), param);
        }

        // 확인/취소 두 버튼 알림창. 결과의 IsSucess(오타 그대로의 필드명) 가 true 면 확인
        var popup = CCoreServices.GetCoreService<CPopupService>().NoticeAlertByMessage(strValue, PopupAlert.BUTTON_TYPE.BTN_TWO);
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = popup.ShowAsObservable()
        .Subscribe(res=>
        {
            if( res.IsSucess)
            {
                LevelLinkManager.Instance.RequestLevelLinkReset();
            }

            disposable.Dispose();
        });        
    }

    /// <summary>
    /// 순서 확인해야함
    /// (자동 배치 확인 팝업) 호출: PageLevelLink.OnClickAuto (Mid_MidUI 의 자동 배치 버튼)
    /// 확인 시 매니저가 보유 카드로 자동 덱(Item2)을 계산하고,
    ///  - 배치 가능한 카드가 부족하면(Item1 == false) 토스트만 표시
    ///  - 현재 덱과 다를 때만 RequestLevelLinkDeckSave(덱 저장 API) 호출 → 성공 시 토스트 + LevelLinkSelectGDID 발행
    /// </summary>
    public void OpenPopupLevelLinkDeckAutoSave()
    {
        //자동 배치 후 LevelLink 덱이 저장됩니다. 자동 배치를 실행할까요?
        var popup = CCoreServices.GetCoreService<CPopupService>().NoticeAlertByKey("levellink_popup_desc_01", PopupAlert.BUTTON_TYPE.BTN_TWO);
        SingleAssignmentDisposable disposable = new SingleAssignmentDisposable();
        disposable.Disposable = popup.ShowAsObservable()
        .Subscribe(res=>
        {
            if( res.IsSucess)
            {
                //자동배치
                // Tuple<bool, Dictionary<int,int>>: Item1 = 자동 배치 성공 여부, Item2 = { 슬롯 순서 : 카드 GDID }
                var result = LevelLinkManager.Instance.GetLevelLinkCardDeckAutoList();

                if( result.Item1 == false )
                {
                    //배치할 수 있는 카드가 부족합니다.
                    CCoreServices.GetCoreService<CPopupService>().ShowNoticeMessage("levellink_toast_02");
                }
                else
                {

                    //현재 배치된 카드가 있는지 확인해서 완전 동일할 경우 API 호출 하지 않도록 수정한다
                    if( LevelLinkManager.Instance.IsChangeSlotInfo(result.Item2) )
                    {
                        LevelLinkManager.Instance.RequestLevelLinkDeckSave(result.Item2);
                    }
                    else
                    {
                        CDebug.Log("변경된 내용이 없다");
                    }
                }
            }

            disposable.Dispose();
        });        

    }








}
