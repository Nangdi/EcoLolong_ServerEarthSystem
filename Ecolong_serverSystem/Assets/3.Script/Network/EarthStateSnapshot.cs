using System;
using UnityEngine;

[Serializable]
public class EarthStateSnapshot
{
    // 현재까지 TCP로 누적된 탄소 count입니다.
    public int CarbonCount;

    // 현재까지 TCP로 누적된 발전 count입니다.
    public int PowerGenerationCount;

    // 현재까지 TCP로 누적된 전기 count입니다. 5종 발전(화력/수력/태양광/풍력/수소)과 ELECTRIC 입력의 합계입니다.
    public int ElectricCount;

    // 화면에 표시되는 "현재 탄소" 값입니다. CARBON 입력으로 늘고 CARBON_CAPTURE 만큼 차감됩니다.
    public int CurrentCarbon;

    // 화면에 표시되는 "현재 발전토큰" 값입니다. POWER_GENERATION 입력으로 누적됩니다.
    public int CurrentPowerGeneration;

    // 계산된 친환경도 단계입니다. 1이 가장 낮고 5가 가장 높습니다.
    public int EcoLevel = 5;

    // 계산된 발전도 단계입니다. 1이 가장 낮고 5가 가장 높습니다.
    public int DevelopmentLevel = 1;

    // 소프트웨어 보정값입니다. 기획에서 말한 SW 가중치(+1 / 0 / -1)를 담습니다.
    public int EcoLevelOffset;

    // 친환경도/발전도 조합으로 결정된 최종 지구 상태 이름입니다.
    public string StateName = "자연낙원";

    // 산업화 이전 280ppm을 기준으로 계산한 현재 이산화탄소 농도입니다.
    public float CarbonPpm = 280f;

    // 친환경도/발전도/탄소토큰에 의해 매초 얼마나 ppm이 변하는지 나타냅니다.
    public float CarbonPpmChangePerSecond;

    // ΔT = 4.28 * ln(C / 280) 식으로 계산한 온도 상승값입니다.
    public float TemperatureDeltaC;

    // 100 - α * ΔT 식으로 계산한 북극 얼음 잔존율입니다.
    public float ArcticIcePercent = 100f;

    // k * ΔT 식으로 계산한 해수면 상승 높이(미터)입니다.
    public float SeaLevelRiseMeters;

    // 새 게임 시작 시 기준값으로 되돌릴 때 사용하는 초기화 메서드입니다.
    public void ResetToDefaults()
    {
        CarbonCount = 0;
        PowerGenerationCount = 0;
        ElectricCount = 0;
        CurrentCarbon = 0;
        CurrentPowerGeneration = 0;
        EcoLevel = 5;
        DevelopmentLevel = 1;
        EcoLevelOffset = 0;
        StateName = "자연낙원";
        CarbonPpm = 280f;
        CarbonPpmChangePerSecond = 0f;
        TemperatureDeltaC = 0f;
        ArcticIcePercent = 100f;
        SeaLevelRiseMeters = 0f;
    }

    // 모든 필드를 한 번에 비교한 뒤 차이가 있는 경우에만 값을 갱신하고 true를 반환합니다.
    // 호출자(EarthStateManager)는 반환값으로 StateChanged 이벤트 발행 여부를 결정할 수 있습니다.
    public bool SetValues(
        int carbonCount,
        int powerGenerationCount,
        int electricCount,
        int currentCarbon,
        int currentPowerGeneration,
        int ecoLevel,
        int developmentLevel,
        int ecoLevelOffset,
        string stateName,
        float carbonPpm,
        float carbonPpmChangePerSecond,
        float temperatureDeltaC,
        float arcticIcePercent,
        float seaLevelRiseMeters)
    {
        bool changed =
            CarbonCount != carbonCount ||
            PowerGenerationCount != powerGenerationCount ||
            ElectricCount != electricCount ||
            CurrentCarbon != currentCarbon ||
            CurrentPowerGeneration != currentPowerGeneration ||
            EcoLevel != ecoLevel ||
            DevelopmentLevel != developmentLevel ||
            EcoLevelOffset != ecoLevelOffset ||
            !string.Equals(StateName, stateName, StringComparison.Ordinal) ||
            !Mathf.Approximately(CarbonPpm, carbonPpm) ||
            !Mathf.Approximately(CarbonPpmChangePerSecond, carbonPpmChangePerSecond) ||
            !Mathf.Approximately(TemperatureDeltaC, temperatureDeltaC) ||
            !Mathf.Approximately(ArcticIcePercent, arcticIcePercent) ||
            !Mathf.Approximately(SeaLevelRiseMeters, seaLevelRiseMeters);

        if (!changed)
            return false;

        CarbonCount = carbonCount;
        PowerGenerationCount = powerGenerationCount;
        ElectricCount = electricCount;
        CurrentCarbon = currentCarbon;
        CurrentPowerGeneration = currentPowerGeneration;
        EcoLevel = ecoLevel;
        DevelopmentLevel = developmentLevel;
        EcoLevelOffset = ecoLevelOffset;
        StateName = stateName;
        CarbonPpm = carbonPpm;
        CarbonPpmChangePerSecond = carbonPpmChangePerSecond;
        TemperatureDeltaC = temperatureDeltaC;
        ArcticIcePercent = arcticIcePercent;
        SeaLevelRiseMeters = seaLevelRiseMeters;
        return true;
    }
}
