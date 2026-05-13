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


    // 지원하는 모든 누적값을 한 번에 0으로 초기화합니다.
    public void Clear()
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
