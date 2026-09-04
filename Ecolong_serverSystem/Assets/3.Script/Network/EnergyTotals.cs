using System;

[Serializable]
public class EnergyTotals
{
    public int thermalPower;
    public int hydroPower;
    public int solarPower;
    public int windPower;
    public int hydrogen;
    public int electricCount;
    public int totalCarbon;
    public int powerGeneration;
    public int cityEcoScore;
    public int totalCityBuildingCount;
    public int captureCarbon;
    public int currentPowerGeneration;

    // 게임 시작 시 기본으로 지급되는 탄소토큰입니다.
    // "생산"된 값이 아니므로 누적 생산량(totalCarbon)과는 분리해서 들고 있고,
    // 플레이어가 실제로 들고 있는 개수(GetCurrentCarbon)에만 더해집니다.
    public int initialCarbon;


    // 지원하는 모든 누적값을 한 번에 0으로 초기화합니다.
    // initialCarbonTokenCount를 주면 탄소만 그 개수부터 시작합니다(게임 시작 시 기본 지급되는 탄소토큰).
    public void Clear(int initialCarbonTokenCount = 0)
    {
        thermalPower = 0;
        hydroPower = 0;
        solarPower = 0;
        windPower = 0;
        hydrogen = 0;
        electricCount = 0;
        totalCarbon = 0;
        powerGeneration = 0;
        cityEcoScore = 0;
        totalCityBuildingCount = 0;
        captureCarbon = 0;
        currentPowerGeneration = 0;

        // 시작 지급분은 누적 생산량(totalCarbon)이 아니라 별도 필드에 담아
        // "누적 탄소" 표시/기록에는 잡히지 않고 "현재 탄소"에만 반영되게 합니다.
        initialCarbon = Math.Max(0, initialCarbonTokenCount);
    }

    // 화면에 표시되는 "현재 탄소 토큰" 개수입니다.
    // 시작 지급분 + 누적 생산량 - 포집량이며, 음수로는 내려가지 않습니다.
    public int GetCurrentCarbon()
    {
        return Math.Max(0, initialCarbon + totalCarbon - captureCarbon);
    }

    // 다른 누적값 묶음의 모든 항목을 그대로 복사합니다.
    // 게임 종료 시점의 최종값을 백업해 두었다가 리플레이 종료 후 화면에 되살릴 때 사용합니다.
    public void CopyFrom(EnergyTotals source)
    {
        if (source == null)
            return;

        thermalPower = source.thermalPower;
        hydroPower = source.hydroPower;
        solarPower = source.solarPower;
        windPower = source.windPower;
        hydrogen = source.hydrogen;
        electricCount = source.electricCount;
        totalCarbon = source.totalCarbon;
        powerGeneration = source.powerGeneration;
        cityEcoScore = source.cityEcoScore;
        totalCityBuildingCount = source.totalCityBuildingCount;
        captureCarbon = source.captureCarbon;
        currentPowerGeneration = source.currentPowerGeneration;
        initialCarbon = source.initialCarbon;
    }

    // [디버그] "현재 탄소 토큰"을 delta만큼 움직입니다.
    // 늘릴 때는 누적 생산량에 더하고, 줄일 때는 누적 생산량 → 시작 지급분 순으로 깎아
    // 현재 보유량이 0 밑으로 내려가지 않게 합니다.
    public void AdjustCarbonTokens(int delta)
    {
        if (delta >= 0)
        {
            totalCarbon += delta;
            return;
        }

        int remaining = -delta;

        int fromProduction = Math.Min(remaining, totalCarbon);
        totalCarbon -= fromProduction;
        remaining -= fromProduction;

        if (remaining > 0)
            initialCarbon = Math.Max(0, initialCarbon - remaining);
    }

    // [디버그] 발전토큰을 delta만큼 움직입니다.
    // 발전도 판정은 누적값(powerGeneration)을 보므로 누적/현재를 같은 값으로 함께 증감하고 0을 하한으로 둡니다.
    public void AdjustPowerGenerationTokens(int delta)
    {
        int applied = delta;
        if (applied < 0)
            applied = -Math.Min(-applied, Math.Min(powerGeneration, currentPowerGeneration));

        powerGeneration += applied;
        currentPowerGeneration += applied;
    }

    // 표준 키 이름에 맞는 누적값을 증가시킵니다.
    // 5종 발전(화력/수력/태양광/풍력/수소)은 들어온 count만큼 electricCount도 함께 누적합니다.
    public bool AddValue(string canonicalKey, int amount)
    {
        switch (canonicalKey)
        {
            case "THERMAL":
                thermalPower += amount;
                electricCount += amount;
                return true;
            case "HYDRO":
                hydroPower += amount;
                electricCount += amount;
                return true;
            case "SOLAR":
                solarPower += amount;
                electricCount += amount;
                return true;
            case "WIND":
                windPower += amount;
                electricCount += amount;
                return true;
            case "HYDROGEN":
                hydrogen += amount;
                electricCount += amount;
                return true;
            case "ELECTRIC":
                electricCount += amount;
                return true;
            case "CARBON":
                totalCarbon += amount;
                return true;
            case "POWER_GENERATION":
                powerGeneration += amount;
                currentPowerGeneration += amount;
                return true;
            case "CARBON_CAPTURE":
                captureCarbon = amount;
                return true;
            case "ECO":
                cityEcoScore = amount;
                return true;
            case "BUILDING":
                totalCityBuildingCount = amount;
                return true;
            default:
                return false;
        }
    }
}
