using System.Collections;
using System.Collections.Generic;
using System.Linq;
/// <summary>
/// LevelLink 그룹 테이블(DATA_LEVELLINK_GROUP_LIST)의 한 행 = 덱 슬롯 하나의 구성 정보.
/// 같은 level_link_group_index를 가진 행들이 모여 하나의 LevelLink 덱(최대 7칸)을 이룬다.
/// 사용처: LevelLinkManager(GetSlotGroupList, 자동 배치), PageLevelLink / 덱 편집 팝업(GroupList로 슬롯 UI 구성)
/// </summary>
public class LevelLinkGroupList
{
    // 행 고유 ID (그룹 안에서의 Dictionary key)
    public int level_link_group_id;
    // 그룹 인덱스. LevelLinkList.level_link_group_index와 연결되어 "어느 LevelLink의 덱인지"를 나타낸다
    public int level_link_group_index;
    // 덱 안에서의 슬롯 번호(1~7). 서버 LevelLinkList.card_1 ~ card_7 의 번호와 대응한다
    public int level_link_group_order;
    // 슬롯 배치 조건 멤버. 그룹 전체 값이면 아무 멤버 카드나 가능, 특정 멤버 값이면 그 멤버 카드만 배치 가능
    public MEMBER_TYPE level_link_group_con;
}

/// <summary>
/// LevelLink 테이블(DATA_LEVELLINK_LIST)의 한 행 = LevelLink 하나의 설정.
/// 사용처: LevelLinkManager(목록 순서, 그룹 조회), PageLevelLink(쿨타임 최대값 등), PopupLevelLinkUnlock(해금 조건 / 비용 / 재료)
/// </summary>
public class LevelLinkList
{
    // LevelLink ID. 서버 LevelLinkList.gdid와 같은 값
    public int level_link_id;
	// 표시 순서. GetLevelLinkLists()가 이 값 오름차순으로 정렬한다 (이전/다음 이동 순서)
	public int level_link_order;
    // 확장 가능 여부(BOOL_DISABLE / BOOL_ENABLE). 테이블에서 읽기만 하고 LevelLink 코드에서는 사용하지 않는다
    public COMMON_AVAILABILLITY level_link_extendable;
    // 덱 슬롯 구성 그룹 인덱스 → CLevelLinkDataManager.GetLevelLinkGroupLists(이 값)
    public int level_link_group_index;
    // 중앙 슬롯 카드 등록 해제 후 쿨타임(초). 쿨타임 슬라이더 최대값, 해제/초기화 안내 문구에 사용된다
    public int level_link_cooldown;
    // 해금 미션 그룹 인덱스 → CQuestDataManager.GetQuestListDatas(이 값)로 해금 조건 퀘스트 목록을 가져온다
    public int	level_link_condition_index;
    // 해금에 필요한 골드
    public int 	level_link_gold_value;

    // 해금에 필요한 재료 목록 (테이블의 level_link_extend_type1~3 / id1~3 / value1~3 열을 묶은 것, 최대 3개)
    public List<LevelLinkExtend> level_link_extend_list;
}

/// <summary>
/// LevelLink 해금 재료 하나 (LevelLinkList.level_link_extend_list의 원소).
/// LevelLinkManager.CheckItemCount()와 PopupLevelLinkUnlock(재료 표시 / 부족분 구매)에서 사용한다.
/// </summary>
public class LevelLinkExtend
{
    // 재료 종류(공통 카테고리). ConvertToRewardType()으로 보상 타입(아이템 / 상품 등)으로 바꿔 보유 수량을 조회한다
    public COMMON_CATEGORY	level_link_extend_type;
    // 재료 ID (아이템 ID 등)
    public int	level_link_extend_id;
    // 필요 수량
    public int 	level_link_extend_value;
}

/// <summary>
/// LevelLink 테이블 데이터 매니저 (Singleton).
/// LevelLink 테이블과 LevelLink 그룹 테이블을 읽어 Dictionary로 보관하고, 조회 함수를 제공한다. 서버 데이터는 다루지 않는다(그건 LevelLinkManager).
/// 로드 시점: CDataManager가 테이블 프리로딩을 끝내면 등록된 콜백(OnLoadLevelLinkListDone / OnLoadLevelLinkGroupListDone)을 호출한다.
/// </summary>
public class CLevelLinkDataManager : Singleton<CLevelLinkDataManager>
{
    /// <summary>
    /// Dictionary<level_link_group_index, Dictionary<level_link_group_id, LevelLinkGroupList>>
    /// </summary>
    private Dictionary<int, Dictionary<int, LevelLinkGroupList>> LevelLinkGroupListDic = new Dictionary<int, Dictionary<int, LevelLinkGroupList>>();
    // key: level_link_id, value: LevelLink 테이블 행
    private Dictionary<int, LevelLinkList> LevelLinkListDic = new Dictionary<int, LevelLinkList>();

    /// <summary>
    /// LevelLink 테이블 로드 완료 콜백. CDataManager의 _preLoadingData에 DATA_LEVELLINK_LIST 키로 등록되어 호출된다.
    /// </summary>
    public static void OnLoadLevelLinkListDone()
    {
        Instance.LoadLevelLinkListData();
    }
    /// <summary>
    /// LevelLink 그룹 테이블 로드 완료 콜백. CDataManager의 _preLoadingData에 DATA_LEVELLINK_GROUP_LIST 키로 등록되어 호출된다.
    /// </summary>
    public static void OnLoadLevelLinkGroupListDone()
    {
        Instance.LoadLevelLinkGroupListData();
    }

    /// <summary>
    /// LevelLink 테이블을 가져와 LevelLinkListDic을 새로 만든다. 테이블이 없으면 에러 로그만 남기고 기존 데이터를 유지한다.
    /// </summary>
    private void LoadLevelLinkListData()
    {
        DataTable table = CDataManager.GetTable(EDataDefine.DATA_LEVELLINK_LIST);

        if (null == table)
        {
            CDebug.LogError(string.Format("Not Found Table : [{0}]", EDataDefine.DATA_LEVELLINK_LIST));
            return;
        }

        // 기존 Dictionary를 비우고 버린 뒤 새로 파싱한 Dictionary로 교체 (테이블 재로드 대응)
        if (LevelLinkListDic != null)
        {
            LevelLinkListDic.Clear();
            LevelLinkListDic = null;
        }

        LevelLinkListDic = LoadLevelLinkListDataTable(table);
    }

    /// <summary>
    /// LevelLink 그룹 테이블을 가져와 LevelLinkGroupListDic을 새로 만든다. 테이블이 없으면 에러 로그만 남긴다.
    /// </summary>
    private void LoadLevelLinkGroupListData()
    {
        DataTable table = CDataManager.GetTable(EDataDefine.DATA_LEVELLINK_GROUP_LIST);

        if (null == table)
        {
            CDebug.LogError(string.Format("Not Found Table : [{0}]", EDataDefine.DATA_LEVELLINK_GROUP_LIST));
            return;
        }

        if (LevelLinkGroupListDic != null)
        {
            LevelLinkGroupListDic.Clear();
            LevelLinkGroupListDic = null;
        }

        LevelLinkGroupListDic = LoadLevelLinkGroupListDataTable(table);
    }


    /// <summary>
    /// LevelLink 테이블의 모든 행을 LevelLinkList로 파싱해 Dictionary(key: level_link_id)로 반환한다.
    /// 같은 level_link_id가 두 번 나오면 먼저 나온 행을 유지하고 에러 로그를 남긴다.
    /// </summary>
    private static Dictionary<int, LevelLinkList> LoadLevelLinkListDataTable(DataTable table)
    {
        var dictionary = new Dictionary<int, LevelLinkList>();

        // 행 단위로 열 이름을 지정해 값을 읽는다 (i = 행 번호)
        for (int i = 0; i < table.RowCount; i++)
        {
            LevelLinkList data = new LevelLinkList();
            data.level_link_id = table.GetValue<int>("level_link_id", i);
            data.level_link_order = table.GetValue<int>("level_link_order", i);
            data.level_link_extendable = table.GetValue<int>("level_link_extendable", i).ToEnum<COMMON_AVAILABILLITY>();
            data.level_link_group_index = table.GetValue<int>("level_link_group_index", i);
            data.level_link_cooldown = table.GetValue<int>("level_link_cooldown", i);
            data.level_link_condition_index = table.GetValue<int>("level_link_condition_index", i);
            data.level_link_gold_value = table.GetValue<int>("level_link_gold_value", i);

            data.level_link_extend_list = new List<LevelLinkExtend>();

            // 재료 열은 level_link_extend_type1 / id1 / value1 ~ type3 / id3 / value3 의 3세트
            // 앞에서부터 읽다가 타입이 NONE(0)인 세트를 만나면 그 뒤는 읽지 않는다 (재료는 앞에서부터 채워져 있다는 전제)
            for(int j = 1; j <= 3; j++)
            {
                COMMON_CATEGORY commonCategory = table.GetValue<int>($"level_link_extend_type{j}", i).ToEnum<COMMON_CATEGORY>();

                if( commonCategory == COMMON_CATEGORY.NONE) break; //값이 0 이면 읽지 않는다

                LevelLinkExtend extendData = new LevelLinkExtend();
                extendData.level_link_extend_type = commonCategory;
                extendData.level_link_extend_id = table.GetValue<int>($"level_link_extend_id{j}", i);
                extendData.level_link_extend_value = table.GetValue<int>($"level_link_extend_value{j}", i);

                data.level_link_extend_list.Add(extendData);
            }

            if (!dictionary.ContainsKey(data.level_link_id))
            {
                dictionary.Add(data.level_link_id, data);
            }
            else
            {
                CDebug.LogError(string.Format("Error dictionary Key Duplication!!! {0} Index[{1}]",EDataDefine.DATA_LEVELLINK_LIST, data.level_link_id));
            }
        }

        return dictionary;
    }

    /// <summary>
    /// LevelLink 그룹 테이블의 모든 행을 2단 Dictionary로 파싱해 반환한다.
    /// 바깥 key: level_link_group_index(어느 LevelLink 덱인지), 안쪽 key: level_link_group_id(행 ID), value: 슬롯 구성 행
    /// 같은 그룹 안에서 level_link_group_id가 중복되면 먼저 나온 행을 유지하고 에러 로그를 남긴다.
    /// </summary>
    private static Dictionary<int, Dictionary<int, LevelLinkGroupList>> LoadLevelLinkGroupListDataTable(DataTable table)
    {
        var dictionary = new Dictionary<int, Dictionary<int, LevelLinkGroupList>>();

        for (int i = 0; i < table.RowCount; i++)
        {
            LevelLinkGroupList data = new LevelLinkGroupList();
            data.level_link_group_id = table.GetValue<int>("level_link_group_id", i);
            data.level_link_group_index = table.GetValue<int>("level_link_group_index", i);
            data.level_link_group_order = table.GetValue<int>("level_link_group_order", i);
            // 테이블의 정수 값을 MEMBER_TYPE으로 변환 (그룹 전체 또는 특정 멤버)
            data.level_link_group_con = table.GetValue<int>("level_link_group_con", i).ToMemberType();


            // 이 그룹 인덱스가 처음 나오면 안쪽 Dictionary를 만든다
            if (!dictionary.ContainsKey(data.level_link_group_index))
            {
                var dic = new Dictionary<int, LevelLinkGroupList>();
                dictionary.Add(data.level_link_group_index, dic);
            }

            // 안쪽 Dictionary에 행 ID 기준으로 추가 (중복이면 에러 로그)
            if( !dictionary[data.level_link_group_index].ContainsKey(data.level_link_group_id))
            {
                dictionary[data.level_link_group_index].Add(data.level_link_group_id, data);
            }
            else
            {
                CDebug.LogError(string.Format("Error dictionary Key Duplication!!! {0} Index[{1}]",EDataDefine.DATA_LEVELLINK_GROUP_LIST, data.level_link_group_id));
            }
        }

        return dictionary;
    }    


    /// <summary>
    /// LevelLink ID(level_link_id)로 테이블 행을 찾는다. 테이블 미로드 또는 ID가 없으면 null.
    /// 사용처: LevelLinkManager(GetSlotGroupList, 자동 배치), PageLevelLink / PageLevelLink.popup(쿨타임, 그룹 인덱스, 해금 팝업 데이터)
    /// </summary>
    public LevelLinkList GetLevelLinkData(int id)
    {
        if (LevelLinkListDic == null) return null;

        if (LevelLinkListDic.ContainsKey(id))
        {
            return LevelLinkListDic[id];
        }

        return null;
    }

    /// <summary>
    /// 전체 LevelLink 테이블 행을 level_link_order 오름차순으로 정렬한 새 리스트로 반환한다. 테이블 미로드면 null.
    /// 사용처: LevelLinkManager.InitData()(이동 순서 목록), PageLevelLink.SetData_BottomUI()(하단 슬롯 목록)
    /// </summary>
    public List<LevelLinkList> GetLevelLinkLists()
    {
        if (LevelLinkListDic == null) return null;

        // Values를 리스트로 복사한 뒤 표시 순서로 정렬 (앞의 ToList()는 없어도 결과가 같다)
        return LevelLinkListDic.Values.ToList().OrderBy(x => x.level_link_order).ToList();
    }


    /// <summary>
    /// 그룹 인덱스 + 행 ID로 슬롯 구성 행 하나를 찾는다. 없으면 null.
    /// (현재 LevelLink 코드에서는 호출하는 곳이 없다)
    /// </summary>
    public LevelLinkGroupList GetLevelLinkGroupData(int groupIndex, int groupId)
    {
        if (LevelLinkGroupListDic == null) return null;

        if (LevelLinkGroupListDic.ContainsKey(groupIndex))
        {
            if (LevelLinkGroupListDic[groupIndex].ContainsKey(groupId))
            {
                return LevelLinkGroupListDic[groupIndex][groupId];
            }
        }

        return null;
    }

    /// <summary>
    /// 그룹 인덱스에 속한 슬롯 구성 행 전체를 level_link_group_order(슬롯 번호) 오름차순으로 반환한다. 없으면 null.
    /// = 한 LevelLink 덱의 슬롯 목록. 리스트 길이가 곧 그 LevelLink의 덱 슬롯 수다.
    /// 사용처: LevelLinkManager(GetSlotGroupList, GetLevelLinkCardDeckAutoList), PageLevelLink(중앙 UI GroupList), 덱 편집 팝업
    /// </summary>
    public List<LevelLinkGroupList> GetLevelLinkGroupLists(int groupIndex)
    {
        if (LevelLinkGroupListDic == null) return null;

        if (LevelLinkGroupListDic.ContainsKey(groupIndex))
        {
            // 안쪽 Dictionary의 값들을 슬롯 번호 순으로 정렬한 새 리스트
            return LevelLinkGroupListDic[groupIndex].Values.ToList().OrderBy(x => x.level_link_group_order).ToList();
        }

        return null;
    }

}
