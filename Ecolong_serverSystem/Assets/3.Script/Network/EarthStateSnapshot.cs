using System;

[Serializable]
public class EarthStateSnapshot
{
    // 현재까지 TCP로 누적된 탄소 count입니다.
    public int CarbonCount;

    // 현재까지 TCP로 누적된 발전 count입니다.
    public int PowerGenerationCount;

    // 계산된 친환경도 단계입니다. 1이 가장 낮고 5가 가장 높습니다.
    public int EcoLevel = 5;

    // 계산된 발전도 단계입니다. 1이 가장 낮고 5가 가장 높습니다.
    public int DevelopmentLevel = 1;

    // 소프트웨어 보정값입니다. 기획에서 말한 SW 가중치(+1 / 0 / -1)를 담습니다.
    public int EcoLevelOffset;

    // 친환경도/발전도 조합으로 결정된 최종 지구 상태 이름입니다.
    public string StateName = "자연낙원";

    // 여러 필드를 한 번에 갱신할 때 사용하는 헬퍼입니다.
    // 값이 한 프레임 안에서 같이 바뀌므로, 세터를 따로 여러 번 호출하는 대신
    // 이 메서드로 현재 상태 스냅샷을 한 번에 덮어씁니다.
    public void SetValues(int carbonCount, int powerGenerationCount, int ecoLevel, int developmentLevel, int ecoLevelOffset, string stateName)
    {
        CarbonCount = carbonCount;
        PowerGenerationCount = powerGenerationCount;
        EcoLevel = ecoLevel;
        DevelopmentLevel = developmentLevel;
        EcoLevelOffset = ecoLevelOffset;
        StateName = stateName;
    }
}
