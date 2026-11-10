# LevelLink 설계 문서 (원본 코드 분석)

이 문서는 `LevelLink` 폴더의 클라이언트 코드(51개 파일)가 **어떤 시스템이고, 어떻게 나뉘어 있고, 데이터와 이벤트가 어떻게 흐르는지**를 정리합니다.
코드 한 줄 한 줄의 설명은 각 파일의 주석에 있고, 이 문서는 그 위의 "지도" 역할을 합니다.

---

## 목차
1. 시스템 개요
2. 사용자 흐름
3. 전체 구조
4. UI 프레임워크 (기반 클래스)
5. 화면 구성
6. 데이터 모델
7. 서버 API
8. 이벤트와 데이터 갱신 흐름
9. 핵심 로직
10. 설계 패턴 분석
11. 파일 목록

---

## 1. 시스템 개요

### 1-1. 의도
**LevelLink는 이미 성장한 카드를 활용해 새로운 카드의 육성을 쉽게 해 주는 도움 시스템입니다.**
이미 키운 카드들로 덱을 꾸리면 그 덱의 레벨이 정해지고, 새로 얻은 카드를 등록하면 그 레벨을 그대로 빌려 씁니다.

### 1-2. 핵심 개념

| 개념 | 설명 | 코드 |
|---|---|---|
| **LevelLink (칸)** | 여러 개가 있으며 순서대로 해금한다. 칸마다 덱 구성(슬롯 수, 멤버 조건)과 쿨타임이 테이블로 정해져 있다 | 테이블 `LevelLinkList`, 서버 `LevelLinkList` |
| **덱** | 칸마다 최대 7슬롯. 슬롯은 "특정 멤버 전용" 또는 "그룹 전체(아무 멤버)" 조건을 가진다. **이미 성장한 카드**를 넣는다 | 테이블 `LevelLinkGroupList`, 서버 `card_1 ~ card_7` |
| **LevelLink 레벨** | 덱에 넣은 카드 중 **가장 낮은 카드 레벨**. 모든 슬롯을 채워야 저장할 수 있다 | `GetLevelLinkSlotLV()`, 서버 `lv` |
| **중앙 슬롯 (등록 카드)** | **새 카드** 1장을 등록하면 카드 레벨이 LevelLink 레벨로 적용된다. 원래 레벨이 더 높으면 원래 레벨을 유지한다 | 서버 `slot`, 카드 `level_link_lv` |
| **쿨타임** | 등록 카드를 해제하면 그 칸에 쿨타임이 걸린다. 광고 / 보석 / 단축 티켓으로 줄일 수 있다 | 서버 `remain_time`, 테이블 `level_link_cooldown` |
| **해금** | 잠긴 칸은 미션 달성 + 골드 + 재료로 연다. 재료가 부족하면 보석으로 살 수 있다 | `PopupLevelLinkUnlock` |

### 1-3. 칸의 상태
`LevelLinkManager.GetStateLevelLink()`가 서버 데이터로 계산합니다. 판정 순서가 곧 우선순위입니다.

```
LOCK      (state == 0)        잠김 → 해금 팝업
COOLTIME  (remain_time > 0)   쿨타임 중 → 단축 팝업
SETTING   (slot > 0)          카드 등록됨 → 해제 팝업
EMPTY     (그 외)              비어 있음 → 카드 선택 팝업
```

### 1-4. 카드의 `level_link_lv`
| 값 | 의미 |
|---|---|
| `0` | LevelLink가 적용되지 않은 카드. 덱 후보와 등록 후보가 될 수 있다 |
| 양수 | 중앙 슬롯에 등록되어 LevelLink 레벨이 적용 중인 카드 |
| `-1` | 등록되었지만 원래 카드 레벨이 더 높아 원래 레벨을 쓰는 카드 |

값이 0인 카드만 덱 후보와 등록 후보가 됩니다.

---

## 2. 사용자 흐름

```
로비 LevelLink 버튼
  └─ LevelLinkManager.ShowLevelLinkPage()
       카드 정보 API → LevelLink 정보 API(level_link/top) → PageLevelLink 생성
            │
            ├─ 좌우 화살표 / 하단 슬롯 목록 : 볼 칸 선택 (CurrentPageGDID)
            │
            ├─ [LOCK] 잠금 덮개 클릭 (이전 칸이 열려 있어야 함)
            │     └─ 해금 팝업 : 미션 → 골드 → 재료 검사
            │           ├─ 재료 부족 → 재료 구매 팝업 → 보석 사용 확인 → slot_buy
            │           └─ 조건 충족 → slot_add → 해금 연출
            │
            ├─ 덱 영역 클릭 → 덱 편집 팝업 (정렬 / 필터 / 자동 배치)
            │     └─ 저장 (레벨이 바뀌면 확인 팝업) → deck_save
            ├─ 자동 배치 버튼 → 확인 → 자동 덱 계산 → 바뀌었으면 deck_save
            ├─ 초기화 버튼 → 확인 → reset_all
            │
            └─ 중앙 카드 클릭
                  ├─ 덱 미완성(lv == 0)  → 안내 토스트
                  ├─ [SETTING]  → 등록 해제 확인 (쿨타임 안내) → slot_unset_card
                  ├─ [COOLTIME] → 광고 목록 갱신 → 단축 팝업 (광고 / 보석 / 티켓) → reduce_time
                  └─ [EMPTY]    → 카드 선택 팝업 → 등록 확인 (레벨 변화 미리보기) → slot_set
```

---

## 3. 전체 구조

### 3-1. 계층

```
┌──────────────────────────── UI ────────────────────────────┐
│ PageLevelLink (PagePopup)            ← 화면의 루트, 팝업 열기 │
│   ├─ 하위 레이아웃 (BaseUILayout<Setting>) × 여러 단계        │
│   └─ 팝업 (CPopupUI<Setting, Result>) × 8종 + 하위 레이아웃   │
└───────────────▲──────────────────────────┬─────────────────┘
   MessageBroker│(갱신 알림)                │ 호출 (Instance)
┌───────────────┴──────────────────────────▼─────────────────┐
│ LevelLinkManager (Singleton)                                │
│   서버 상태 보관 · 규칙 계산 · 서버 요청 래핑 · 필터 상태     │
└──────┬──────────────────────┬──────────────────────────────┘
       │ 조회                  │ 요청
┌──────▼──────────────┐ ┌─────▼──────────────────┐ ┌──────────────────┐
│ CLevelLinkDataManager│ │ LevelLinkService        │ │ GameLogService    │
│ 테이블 데이터         │ │ REST API + 응답 반영(Do) │─▶│ (LevelLink 로그)  │
└─────────────────────┘ └─────────────────────────┘ └──────────────────┘
```

| 계층 | 클래스 | 책임 |
|---|---|---|
| 테이블 | `CLevelLinkDataManager` | 칸 / 덱 구성 / 해금 재료 테이블을 Dictionary로 보관하고 조회 |
| 서버 상태 + 규칙 | `LevelLinkManager` | 칸별 서버 데이터, 미션 진행도, 현재 칸, 카드 후보 계산, 자동 배치, 필터, 쿨타임 단축 수단, 보석 사용 흐름 |
| 통신 | `LevelLinkService` | `level_link/*` API 호출. 응답을 `Do()`에서 매니저와 인벤토리에 반영 |
| 로그 | `GameLogService` (partial) | API 응답마다 행동 로그 전송 |
| 정렬 | `ICardSortStrategy`, `LevelLinkDeckSort` | 덱 편집 카드 정렬 (Strategy) |
| UI 기반 | `BaseUILayout`, `CPopupUI`, `PagePopup` | 화면 수명주기, 팝업 결과 전달 |
| UI | `PageLevelLink`, `Popup*`, `Obj*` | 화면 표시와 사용자 입력 |

### 3-2. 폴더

```
LevelLink_original/
├─ Common/
│  ├─ CLevelLinkDataManager.cs            테이블 데이터
│  └─ GameLog/GameLogService.LevelLink.cs 로그
├─ Network/Service/LevelLinkService.cs    서버 API
└─ Process/
   ├─ Common/
   │  ├─ SortFilter/CardSortStrategy.cs   정렬 전략
   │  └─ UI/BaseUILayout.cs, CPopupUI.cs  UI 기반 클래스
   └─ LevelLink/
      ├─ LevelLinkManager.cs              핵심 매니저
      ├─ LevelLinkDeckSort.cs             정렬 목록
      └─ UI/
         ├─ Obj*.cs                       공용 카드 / 슬롯 / 타이머 UI
         ├─ page/                         메인 페이지와 하위 영역
         └─ popup/                        팝업 8종
```

---

## 4. UI 프레임워크 (기반 클래스)

LevelLink의 모든 화면은 아래 세 기반 클래스 위에 만들어져 있습니다.

### 4-1. `BaseUILayout<TSetting>` : 하위 영역
```
InitUI()                     1회 : 버튼 바인딩, 자식 InitUI
SetData(new Setting{...})    부모가 값 + 콜백(Action)을 넣는다
SetUI()                      setting 값으로 화면을 그린다 (자식에게 Setting을 나눠 주고 자식 SetUI)
UpdateData / UpdateUI        갱신이 필요할 때
```
- `Setting`은 화면 값과 **부모에게 돌려보낼 콜백**을 함께 담습니다. 자식은 부모를 직접 참조하지 않고 `setting.OnClickXXX()`를 호출합니다.
- 기본 메서드는 빈 virtual이고, 부모가 필요한 순서로 호출해 갱신 시점을 직접 정합니다.

### 4-2. `CPopupUI<TSetting, TResult>` : 결과를 돌려주는 팝업
```
OpenPopupXXX() → 생성(Awake: subject, result 기본값, Flags) → SetData(Setting)
호출측 : popup.ShowAsObservable().Subscribe(res => ...)   ← 구독해야 열린다
팝업   : 확인 버튼에서 result를 채우고 Close()
닫힘   : OnNext(result) 1회 → OnCompleted
```
- 팝업 하나가 "입력 → 결과 1개"인 함수처럼 동작해서, 호출측이 Rx로 다음 단계를 이어 붙일 수 있습니다.

### 4-3. `PagePopup` : 전체 화면 페이지
- 팝업 연결 리스트에 들어가지 않고, 결과 스트림이 없습니다. `OnClosePopup()`으로 파괴합니다.
- 백키 스택과 페이지 스택에 등록되고, `OnStart` / `OnBackEventProcess`로 수명주기를 받습니다.

---

## 5. 화면 구성

### 5-1. 메인 페이지 계층

```
PageLevelLink (PagePopup)                    루트 : Setting 생성, 팝업 열기, 메시지 구독
├─ TopUI     PageLevelLink_Top               뒤로가기 / 도움말
├─ MidUI     PageLevelLink_Mid               현재 칸 상세 (Setting 중계)
│  ├─ TopUI     PageLevelLink_Mid_TopUI      중앙 카드 + 쿨타임 + 레벨업 연출
│  │  ├─ CardUI     ObjLevelLinkCard
│  │  └─ CoolTimeUI ObjLevelLinkSlot_Timer
│  ├─ MidMidUI  PageLevelLink_Mid_MidUI      LevelLink 레벨, 초기화 / 자동 배치 버튼
│  └─ BottomUI  PageLevelLink_Mid_BottomUI   덱 카드 (최대 7칸, 2줄)
│     └─ ObjLevelLinkSelectCardList × 2 → ObjLevelLinkCard → ObjLevelLinkCardMemberName
├─ BottomUI  PageLevelLink_Bottom            전체 칸 목록 (가로 재사용 스크롤)
│  └─ ObjLevelLinkSlot → ObjLevelLinkSlot_Timer
├─ LockUI    PageLevelLink_Lock              잠금 덮개 + 해금 연출
└─ BtnPrev / BtnNext                         칸 이동
```

### 5-2. 팝업

| 팝업 | 여는 곳 (조건) | Setting | Result | 결과 후 처리 |
|---|---|---|---|---|
| `PopupLevelLinkUnlock` | 잠금 덮개 클릭 (이전 칸 해금됨) | 칸 테이블 (미션 / 골드 / 재료) | `IsSuccess` | `RequestSlotAdd()` |
| `PopupLevelLinkBuy` | 해금 팝업에서 재료 부족 | 부족 재료 목록, 개당 보석 가격 | `IsSuccess`, `TotalCost` | `OpenPopupUseCash` → `RequestItemBuy()` |
| `PopupLevelLinkDeckManage` | 덱 영역 클릭 | 기존 레벨, 슬롯 구성, 현재 배치 | `IsSuccess`, `SlotList` | `RequestLevelLinkDeckSave()` |
| `PopupLevelLinkDecksave` | 덱 편집 저장 시 레벨이 바뀔 때 | 기존 / 변경 후 레벨 | `IsSuccess` | 덱 편집 팝업이 성공으로 닫힘 |
| `PopupLevelLinkSelectcard` | 중앙 카드 클릭 (EMPTY, lv > 0) | 등록 후보 카드, 현재 카드, 레벨 | `IsSuccess`, `SelectCardID` | `RequestLevelLinkCardSave()` |
| `PopupLevelLinkCardChange` | 카드 선택 확인 / 등록 카드 클릭 | 카드, 레벨, 해제 여부, 쿨타임 | `IsSuccess` | 등록 : 선택 팝업 성공 / 해제 : `RequestLevelLinkCardRemove()` |
| `PopupLevelLinkInstantcomplete` | 중앙 카드 클릭 (COOLTIME) | 전체 / 남은 쿨타임, 타이머 키 | `IsSuccess` | `LevelLinkPageUpdate_CoolTime` 발행 |
| 공용 알림 (초기화 / 자동 배치 확인) | 초기화 / 자동 배치 버튼 | 문구 | 확인 여부 | `RequestLevelLinkReset()` / 자동 배치 후 저장 |

덱 편집 팝업 내부:
```
PopupLevelLinkDeckManage
├─ TitleUI  ObjLevelLinkDeck_TitleUI         닫기 / 도움말
├─ TopUI    ObjLevelLinkDeck_TopUI           편집 중인 덱 + 레벨(기존 → 현재)
│  └─ InfoUI ObjLevelLinkDeck_TopUI_Info     자동 배치 / 카드 관리 버튼
└─ MidUI    ObjLevelLinkDeck_MidUI
   ├─ SortUI       ObjLevelLinkDeck_MidUI_Sort          정렬 드롭다운 → CurrentDeckSortType
   ├─ ScrollUI     ObjLevelLinkDeck_MidUI_Scroll        보유 카드 (5장씩 한 줄)
   ├─ ButtonUI     ObjLevelLinkDeck_MidUI_Button        저장(모든 슬롯이 찼을 때) / 맨 위로
   └─ SortFilterUI ObjLevelLinkDeck_MidUI_SortFilter    멤버 / 속성 필터
```
편집은 `setting.SlotList`(로컬 사본)에만 반영되고, 서버 저장은 페이지가 결과를 받아서 합니다.

---

## 6. 데이터 모델

### 6-1. 테이블 (`CLevelLinkDataManager`)
| 클래스 | 한 행의 의미 | 주요 필드 |
|---|---|---|
| `LevelLinkList` | 칸 하나의 설정 | 칸 ID, 표시 순서, 덱 그룹 인덱스, 쿨타임, 해금 미션 / 골드 / 재료 |
| `LevelLinkGroupList` | 덱 슬롯 하나 | 그룹 인덱스, 슬롯 번호(1~7), 멤버 조건 (`level_link_group_con`) |
| `LevelLinkExtend` | 해금 재료 하나 | 타입, 아이템 ID, 필요 수량 |

### 6-2. 서버 (`LevelLinkManager`)
| 데이터 | 내용 |
|---|---|
| `SLevelLinkList` (칸별 `LevelLinkList`) | `state`(잠김 여부), `lv`(LevelLink 레벨), `slot`(등록 카드), `remain_time`(쿨타임), `card_1 ~ card_7`(덱) |
| `SMissionList` | `[칸 ID][미션 ID]` → 진행도(`cur_point` / `end_point` / `state`) |
| `CurrentPageGDID` | 지금 보고 있는 칸. setter가 `LevelLinkSelectGDID`를 발행한다 |
| `CurrentDeckSortType` | 덱 편집 정렬 인덱스 (`static ReactiveProperty<int>`) |
| 필터 상태 | `SortFilter_Member`, `SortFilter_PZTYPE` (편집 중 선택 / 적용 선택) |
| `ShortTimeList` | 쿨타임 단축 수단 목록 |

### 6-3. 덱 슬롯 표현
화면과 서버 사이에서 덱은 `Dictionary<int, int>`(슬롯 번호 → 카드 gdid, 0이면 빈 칸)로 오갑니다.
`GetSlotGroupList()`가 서버의 `card_1 ~ card_7`을 테이블 슬롯 번호 기준으로 이 형태로 바꾸고, 저장할 때 서비스가 다시 7개 값으로 풉니다.

---

## 7. 서버 API

모든 요청은 `LevelLinkService`에 있고, `LevelLinkManager`가 감싸서 호출합니다. 공통 동작은 다음과 같습니다.
- Cold Observable이라 구독해야 요청이 나갑니다.
- 응답 코드(`e != 0`) 안내는 공통 처리가 맡으므로, `Do()`와 구독자는 성공 응답 처리에 집중합니다.
- `Do()`가 구독자보다 먼저 실행되므로, 구독 콜백 시점에는 매니저 데이터가 이미 갱신되어 있습니다.

| API | 매니저 메서드 | 응답 반영 | 성공 후 |
|---|---|---|---|
| `level_link/top` | `ShowLevelLinkPage()` 내부 | 칸 목록 + 미션 진행도 | 페이지 생성 |
| `level_link/slot_add` | `RequestSlotAdd()` | 칸 목록(해금) | `LevelLinkUnLockSlot` 발행 → 해금 연출 |
| `level_link/slot_buy` | `RequestItemBuy()` → `IObservable<bool>` | 아이템 / 상품 인벤토리 | 해금 팝업 갱신 |
| `level_link/deck_save` | `RequestLevelLinkDeckSave()` | 칸 목록(덱, lv) | 토스트 + `LevelLinkSelectGDID` |
| `level_link/reset_all` | `RequestLevelLinkReset()` | 칸 목록 | 토스트 + `LevelLinkSelectGDID` |
| `level_link/slot_set` | `RequestLevelLinkCardSave()` | 칸 목록(slot) + 카드(`level_link_lv`) | `LevelLinkSelectGDID` |
| `level_link/slot_unset_card` | `RequestLevelLinkCardRemove()` | 칸 목록(slot 해제, 쿨타임) + 카드(복원) | `LevelLinkSelectGDID` |
| `level_link/reduce_time` | `ReqLevelLinkReduceTime()` → `IObservable<bool>` | 칸 목록(remain_time), 타이머 정리 | 팝업 성공 → `LevelLinkPageUpdate_CoolTime` |

`reduce_time`의 `itemID`는 1이면 보석 즉시 완료, 티켓 ID면 단축 티켓, 0이면 광고(`adID` 사용)입니다.

---

## 8. 이벤트와 데이터 갱신 흐름

### 8-1. 메시지 (`MessageBroker`)
| 메시지 | 발행 | 구독 | 처리 |
|---|---|---|---|
| `LevelLinkSelectGDID` | `CurrentPageGDID` setter, 덱 저장 / 초기화 / 등록 / 해제 성공, 해금 연출 종료 | `PageLevelLink` | 매니저에서 값을 다시 읽어 Mid / Bottom / Lock 갱신 |
| `LevelLinkPageUpdate_CoolTime` | 쿨타임 종료(타이머), 단축 성공 | `PageLevelLink`, 단축 팝업 | 타이머 재설정 / 팝업 자동 닫기 |
| `LevelLinkUnLockSlot` | 해금 성공 | `PageLevelLink` | 잠금 UI를 해금 연출 모드로 |
| `RefreshCardData` | 카드 인벤토리 (외부) | `PageLevelLink`, 덱 편집 팝업 | 카드 레벨 변화 반영 |

해금 팝업은 메시지 대신 `ReactiveProperty`를 직접 구독합니다. 보유 골드(`CPlayer.Gold`)가 바뀌면 해금 상태를 다시 계산하고, 상태(`CurrentState`)가 바뀌면 화면을 다시 그립니다. (`HudShopRrefresh` 구독 코드는 주석 처리되어 있습니다)

### 8-2. 한 번의 갱신이 도는 순서 (예: 덱 저장)
```
덱 편집 팝업 Close (Result: SlotList)
 → PageLevelLink 구독 콜백 → LevelLinkManager.RequestLevelLinkDeckSave(slotList)
   → LevelLinkService.ReqLevelLinkDeckSave()  ─ 서버 ─▶ 응답
     → Do(): Manager.SetLevelLinkListData(), 로그
   → 구독 콜백: 토스트, MessageBroker.Publish(LevelLinkSelectGDID)
 → PageLevelLink.Receive: SetData(...) → MidUI.SetUI() / BottomUI.UpdateUI() / LockUI.SetUI()
```
**데이터는 위(페이지)에서 아래(하위 영역)로 Setting으로 내려가고, 사용자 입력은 콜백으로 위로 올라갑니다.** 서버 결과는 메시지로 페이지에 알려지고, 페이지가 화면 전체를 다시 그립니다.

---

## 9. 핵심 로직

### 9-1. LevelLink 레벨
- `GetLevelLinkSlotLV(slotInfo)` = 덱 카드 레벨의 최솟값입니다. 덱을 고르게 키울수록 LevelLink 레벨이 올라갑니다.
- `GetSlotMinCard()`는 레벨을 정하는 카드를 찾아 화면에 표시해, 다음에 키울 카드를 알려 줍니다.
- 덱 저장 전에 `IsChangeLevelLinkLowLV()`로 레벨 변화를 확인하고, 필요하면 확인 팝업으로 변경 전후 레벨을 보여 줍니다.

### 9-2. 카드 후보 규칙
| 목록 | 메서드 | 제외 조건 | 정렬 |
|---|---|---|---|
| 덱 편집 카드 | `GetLevelLinkCardDeckList()` | **다른 칸** 덱 카드, `level_link_lv != 0` → 멤버 필터 → 속성 필터 | 선택한 정렬 전략 |
| 자동 배치 후보 | `GetLevelLinkCardDeckAutoList()` | 위와 같음 (현재 칸 덱 카드는 다시 후보) | 레벨 → 전투력 → 등급 → gdid |
| 중앙 슬롯 등록 후보 | `GetLevelLinkCardIDList()` | **모든 칸** 덱 카드(현재 칸 포함), `level_link_lv != 0` | 등급 → 레벨 → gdid |

덱 카드와 육성 카드(등록 카드)를 서로 겹치지 않게 나눠, "성장한 카드로 덱을 꾸리고, 새 카드를 키운다"는 의도를 규칙으로 보장합니다.

### 9-3. 자동 배치 (`GetLevelLinkCardDeckAutoList`)
```
1) 후보 = 인벤토리 - 다른 칸 덱 카드 - LevelLink 적용 카드
2) 후보 수 < 슬롯 수 → 배치 불가로 반환
3) 멤버 전용 슬롯 먼저 : 그 멤버 카드 중 우선순위 1등
   - 맞는 카드가 없는 슬롯이 있으면 배치 불가로 반환
4) 그룹 전체 슬롯 : 남은 후보를 우선순위대로 슬롯 수만큼
결과 : Tuple<bool 배치 가능, Dictionary<슬롯, 카드>>
```
- 제약이 강한 슬롯(멤버 전용)을 먼저 채워서, 멤버 전용 슬롯에 맞는 카드를 먼저 확보합니다.
- 호출하는 곳은 두 군데입니다. 페이지에서는 결과가 기존과 다르면 바로 저장하고, 덱 편집 팝업에서는 화면의 배치만 바꿉니다.

### 9-4. 정렬과 필터
- 정렬은 Strategy 패턴입니다. `ICardSortStrategy` 구현(`CardSort_Level / Grade / Power / Name / Recent`)을 `LevelLinkDeckSort.List`에 순서대로 두면 그 순서가 드롭다운 순서가 됩니다. 공통 보조 정렬(`card_sort`)은 `CardSortStrategyBase`가 붙입니다(Template Method).
- 드롭다운 선택 → `CurrentDeckSortType.Value` 변경 → 덱 편집 팝업이 `DistinctUntilChanged().Skip(1)`로 구독하다가 목록을 다시 계산합니다.
- 필터는 "편집 중 선택"과 "적용 선택"을 따로 둡니다. 필터 패널의 확인은 `SaveSortInfo(true)`, 취소는 `SaveSortInfo(false)`입니다.

### 9-5. 쿨타임 타이머
- `TimeStreamManager`의 타임스트림을 키 `LEVELLINK_SLOT_{칸 ID}`로 공유합니다. 중앙 타이머, 하단 슬롯 타이머, 단축 팝업 타이머가 같은 스트림을 구독합니다.
- 타이머가 끝나면 서버를 다시 부르지 않고 `UpdateLevelLinkListData_RemainTime_TimeKey()`로 로컬 `remain_time`을 0으로 맞추고, `LevelLinkPageUpdate_CoolTime`을 발행합니다.

### 9-6. 쿨타임 단축 수단
`InitShortTimeList()`가 아래 순서로 목록을 만들고, 지금 쓸 수 있는 수단만 보여 줍니다.
| 순서 | 수단 | 목록에 들어가는 조건 | 비용 |
|---|---|---|---|
| 1 | 광고 | 광고 사용 가능 + 남은 시청 횟수 > 0 | 광고 1회 |
| 2 | 보석 즉시 완료 | 게임 설정 `levellink_cash_shorten_payment` 존재 | 남은 초 ÷ 설정값(올림), 실시간 갱신 |
| 3~5 | 단축 티켓 3종 | 설정된 티켓 아이템을 1개 이상 보유 | 티켓 1장 |

### 9-7. 보석 사용 흐름 (`OpenPopupUseCash`)
```
보석 충분 → 사용 확인 팝업 → IsOK
보석 부족 → "구매하러 가시겠습니까?" → 확인 → 보석 상점 → 닫힘 → IsOK = false (돌아와서 다시 진행)
                                    → 취소 → IsOK = false
```
팝업 결과를 `SelectMany`로 이어 붙인 Rx 체인이며, 해금 재료 구매와 쿨타임 즉시 완료가 함께 씁니다.

---

## 10. 설계 패턴 분석

### 10-1. 구조 : MVP(Passive View)에 가까운 혼합형
| 역할 | 클래스 | 근거 |
|---|---|---|
| Model | `LevelLinkManager`, `CLevelLinkDataManager`, `LevelLinkService` | 상태, 규칙, 통신 |
| Presenter 역할 | `PageLevelLink`, 각 `Popup*` | 매니저 값을 읽어 Setting을 만들고, 클릭을 받아 매니저와 팝업을 호출 |
| View | `BaseUILayout` 하위 클래스들 | Setting으로 그리기만 하고, 입력은 콜백으로 위에 보고 |
| 화면용 데이터 | 각 `Setting` | 값 + 콜백 묶음. ViewModel과 비슷하지만 바인딩은 없음 |

구조의 특징:
1. **페이지가 View와 Presenter 역할을 함께 맡습니다.** `PageLevelLink` 한 파일에서 화면 구성과 사용자 흐름을 함께 볼 수 있습니다.
2. **하위 영역이 필요한 값을 매니저에서 바로 읽습니다.** 예: 필터 패널, 중앙 상단, 타이머. Setting을 여러 단계 거쳐 전달하지 않아도 됩니다.
3. **매니저가 공통 UI 처리를 함께 맡습니다.** 토스트, 페이지 로드, 보석 팝업을 매니저가 열어 호출하는 쪽 코드가 짧습니다.
4. **싱글톤으로 상태를 공유합니다.** `LevelLinkManager.Instance`, `CurrentDeckSortType`으로 어느 화면에서든 같은 상태를 봅니다.

`ReactiveProperty`는 세 곳(덱 편집의 `CurrentDeckSortType`, 해금 팝업의 `CPlayer.Gold`와 `CurrentState`)에서 구독하며, 값이 바뀌면 화면 갱신 메서드를 다시 호출합니다.

### 10-2. 함께 쓰인 패턴
| 패턴 | 위치 |
|---|---|
| Composite (계층 UI) | 페이지 → 영역 → 하위 영역, 공통 수명주기 `InitUI / SetData / SetUI` |
| Observer / Event Aggregator | `MessageBroker` 메시지 4종 (LevelLink 전용 3종 + 카드 갱신) |
| 요청과 응답 형태의 팝업 | `CPopupUI<Setting, Result>` + `ShowAsObservable()` |
| Strategy + Template Method | `ICardSortStrategy`, `CardSortStrategyBase` |
| Registry | `LevelLinkDeckSort.List` (목록 순서 = 드롭다운 순서) |
| Singleton | 매니저 3종 |
| Object Pool (재사용 스크롤) | `LoopListView2` : 하단 슬롯, 덱 카드, 카드 선택, 단축 수단 |

### 10-3. 장점
- 화면 구조가 프리팹 계층과 그대로 맞아, 화면에서 코드를 찾기 쉽습니다.
- 하위 영역이 부모를 몰라 재사용하기 쉽습니다. (`ObjLevelLinkCard`, 쿨타임 타이머는 여러 화면에서 함께 씀)
- 팝업이 결과 1개를 내는 함수처럼 동작해, 여러 팝업을 거치는 흐름을 Rx로 이어 붙이기 쉽습니다.
- 서버 결과를 메시지 하나로 모아, 화면 갱신 경로가 한 곳으로 정리됩니다.
- 매니저 하나에 LevelLink의 상태와 규칙이 모여 있어, 기능 전체를 한 곳에서 파악할 수 있습니다.

---

## 11. 파일 목록

| 폴더 | 파일 | 역할 |
|---|---|---|
| `Common` | `CLevelLinkDataManager.cs` | 테이블 데이터 (칸 / 덱 구성 / 해금 재료) |
| `Common/GameLog` | `GameLogService.LevelLink.cs` | 행동 로그 |
| `Network/Service` | `LevelLinkService.cs` | 서버 API 8종 |
| `Process/Common/SortFilter` | `CardSortStrategy.cs` | 정렬 전략 인터페이스와 구현 5종 |
| `Process/Common/UI` | `BaseUILayout.cs` | 하위 영역 기반 클래스 |
| | `CPopupUI.cs` | 팝업 / 페이지 기반 클래스 (`BasePopup`, `CPopupUI`, `PagePopup`) |
| `Process/LevelLink` | `LevelLinkManager.cs` | 핵심 매니저, 상태 enum, 메시지 struct |
| | `LevelLinkDeckSort.cs` | 정렬 목록 (Registry) |
| `Process/LevelLink/UI` | `ObjLevelLinkCard.cs`, `ObjLevelLinkCardMemberName.cs` | 카드 1장, 멤버 조건 라벨 |
| | `ObjLevelLinkSelectCardList.cs` | 덱 카드 한 줄 |
| | `ObjLevelLinkSlot.cs`, `ObjLevelLinkSlot_Timer.cs` | 하단 칸 아이템, 쿨타임 타이머 (3곳 공용) |
| `.../UI/page` | `PageLevelLink.cs`, `PageLevelLink.popup.cs` | 메인 페이지, 팝업 열기 모음 |
| | `PageLevelLink_Top / _Mid / _Mid_TopUI / _Mid_MidUI / _Mid_BottomUI / _Bottom / _Lock.cs` | 페이지 하위 영역 |
| `.../popup/popup_level_link_unlock` | `PopupLevelLinkUnlock.cs` 외 3개 | 해금 (미션 목록, 재료 목록) |
| `.../popup/popup_level_link_buy` | `PopupLevelLinkBuy.cs` | 해금 재료 구매 |
| `.../popup/popup_level_link_deck_manage` | `PopupLevelLinkDeckManage.cs` 외 9개 | 덱 편집 (정렬, 필터, 스크롤, 버튼) |
| `.../popup/popup_level_link_decksave` | `PopupLevelLinkDecksave.cs` | 레벨 변경 확인 |
| `.../popup/popup_level_link_selectcard` | `PopupLevelLinkSelectcard.cs`, `ObjLevelLinkSelectCardListGroup.cs` | 등록 카드 선택 |
| `.../popup/popup_level_link_cardchange` | `PopupLevelLinkCardChange.cs` 외 3개 | 등록 / 해제 확인 (레벨 변화 미리보기) |
| `.../popup/popup_level_link_instantcomplete` | `PopupLevelLinkInstantcomplete.cs` 외 6개 | 쿨타임 단축 (광고 / 보석 / 티켓) |
