using System.Collections.Generic;
using System.Linq;
using Game.RestAPI;

/// <summary>
/// 카드 정렬 전략
/// 화면별 정렬 목록(ex. LevelLinkDeckSort)에서 순서대로 조합해서 사용한다
///
/// 정렬 방식 하나를 객체 하나로 캡슐화한 Strategy 패턴의 인터페이스.
/// 호출부(LevelLinkManager.GetLevelLinkCardDeckList)는 어떤 정렬인지 모르고 Sort()만 호출한다.
/// </summary>
public interface ICardSortStrategy
{
    string StrKey { get; } //드롭다운 표시 문자열 키
    // 카드 목록을 정렬해서 반환한다. IOrderedEnumerable이라 호출부에서 ThenBy로 기준을 더 붙일 수 있다 (지연 실행: 열거할 때 정렬됨)
    IOrderedEnumerable<CardList> Sort(IEnumerable<CardList> list);
}

//정렬 기준이 같으면 card_sort로 보조 정렬한다
/// <summary>
/// 카드 정렬 전략의 공통 베이스 (Template Method 형태).
/// 파생 클래스는 1차 정렬 기준(SortKey)과 표시 문자열 키(StrKey)만 구현하고,
/// Sort()가 1차 정렬 뒤에 카드 테이블의 card_sort 내림차순 보조 정렬을 공통으로 붙인다.
/// </summary>
public abstract class CardSortStrategyBase : ICardSortStrategy
{
    public abstract string StrKey { get; }
    // 1차 정렬 기준. OrderBy / OrderByDescending 결과(IOrderedEnumerable)를 반환해야 ThenBy를 이어 붙일 수 있다
    protected abstract IOrderedEnumerable<CardList> SortKey(IEnumerable<CardList> list);

    // 1차 정렬(SortKey) → 동률이면 카드 테이블 데이터의 card_sort 큰 순으로 정렬
    public IOrderedEnumerable<CardList> Sort(IEnumerable<CardList> list)
    {
        return SortKey(list).ThenByDescending(x => x.GetCardData().card_sort);
    }
}

//레벨
// 카드 레벨(lv) 높은 순. LevelLinkDeckSort의 0번이자 기본 정렬
public class CardSort_Level : CardSortStrategyBase
{
    public override string StrKey => "UI_common_sort_level";
    protected override IOrderedEnumerable<CardList> SortKey(IEnumerable<CardList> list) => list.OrderByDescending(x => x.lv);
}

//등급
// 카드 등급(grade) 높은 순
public class CardSort_Grade : CardSortStrategyBase
{
    public override string StrKey => "UI_common_sort_grade";
    protected override IOrderedEnumerable<CardList> SortKey(IEnumerable<CardList> list) => list.OrderByDescending(x => x.grade);
}

//전투력
// 카드 전투력(GetBattlePower() 계산값) 높은 순
public class CardSort_Power : CardSortStrategyBase
{
    public override string StrKey => "UI_common_sort_power";
    protected override IOrderedEnumerable<CardList> SortKey(IEnumerable<CardList> list) => list.OrderByDescending(x => x.GetBattlePower());
}

//이름
// GetCardNameIDFilter() 값 오름차순 (이 정렬만 오름차순)
// GetCardNameIDFilter()는 카드 테이블의 member 값을 int로 반환하므로, 실제로는 문자열 이름순이 아니라 멤버(MEMBER_TYPE 값) 순서로 묶인다
public class CardSort_Name : CardSortStrategyBase
{
    public override string StrKey => "UI_common_sort_name";
    protected override IOrderedEnumerable<CardList> SortKey(IEnumerable<CardList> list) => list.OrderBy(x => x.GetCardNameIDFilter());
}

//최근 획득
// 서버 카드 데이터의 idx 큰 순 (idx가 클수록 나중에 획득한 카드로 보고 정렬)
public class CardSort_Recent : CardSortStrategyBase
{
    public override string StrKey => "UI_common_sort_recent";
    protected override IOrderedEnumerable<CardList> SortKey(IEnumerable<CardList> list) => list.OrderByDescending(x => x.idx);
}
