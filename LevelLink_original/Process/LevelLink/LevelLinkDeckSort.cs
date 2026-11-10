using System.Collections.Generic;

/// <summary>
/// LevelLink 덱 편집 정렬 목록
/// 목록 순서가 곧 드롭다운 순서(CurrentDeckSortType 값)이다
/// 정렬 추가는 ICardSortStrategy 구현 후 이 목록에 한 줄만 넣으면 된다
///
/// 사용처:
///  - ObjLevelLinkDeck_MidUI_Sort: List를 순서대로 돌며 각 전략의 StrKey로 드롭다운 항목을 만든다. 선택 시 LevelLinkManager.CurrentDeckSortType에 인덱스를 넣는다
///  - LevelLinkManager.GetLevelLinkCardDeckList: Get(CurrentDeckSortTypeValue)로 전략을 꺼내 덱 편집 카드 리스트를 정렬한다
/// 전략 객체는 상태가 없으므로 static readonly 배열 하나를 공유한다 (Strategy 패턴)
/// </summary>
public static class LevelLinkDeckSort
{
    // 정렬 전략 목록. 인덱스 0: 레벨, 1: 등급, 2: 전투력, 3: 이름, 4: 최근 획득
    // IReadOnlyList로 노출해 외부에서 목록을 바꾸지 못하게 한다
    public static readonly IReadOnlyList<ICardSortStrategy> List = new ICardSortStrategy[]
    {
        new CardSort_Level(),
        new CardSort_Grade(),
        new CardSort_Power(),
        new CardSort_Name(),
        new CardSort_Recent(),
    };

    /// <summary>
    /// 인덱스(드롭다운 선택 값)에 해당하는 정렬 전략을 반환한다.
    /// 범위를 벗어난 인덱스(음수 또는 목록 길이 이상)는 0번(레벨순) 전략으로 대체한다.
    /// </summary>
    public static ICardSortStrategy Get(int index)
    {
        //범위를 벗어나면 레벨순
        if( index < 0 || List.Count <= index ) return List[0];

        return List[index];
    }
}
