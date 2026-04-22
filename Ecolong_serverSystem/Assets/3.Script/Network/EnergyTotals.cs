using System;

[Serializable]
public class EnergyTotals
{
    public int ThermalPower;
    public int HydroPower;
    public int SolarPower;
    public int WindPower;
    public int Hydrogen;
    public int ElectricEnergy;
    public int Carbon;
    public int PowerGeneration;
    public int CityEcoScore;
    public int CityBuildingCount;

    // 지원하는 모든 누적값을 한 번에 0으로 초기화합니다.
    public void Clear()
    {
        ThermalPower = 0;
        HydroPower = 0;
        SolarPower = 0;
        WindPower = 0;
        Hydrogen = 0;
        ElectricEnergy = 0;
        Carbon = 0;
        PowerGeneration = 0;
        CityEcoScore = 0;
        CityBuildingCount = 0;
    }

    // 표준 키 이름에 해당하는 값을 반환합니다.
    public int GetValue(string canonicalKey)
    {
        switch (canonicalKey)
        {
            case "THERMAL_POWER":
                return ThermalPower;
            case "HYDRO_POWER":
                return HydroPower;
            case "SOLAR_POWER":
                return SolarPower;
            case "WIND_POWER":
                return WindPower;
            case "HYDROGEN":
                return Hydrogen;
            case "ELECTRIC_ENERGY":
                return ElectricEnergy;
            case "CARBON":
                return Carbon;
            case "POWER_GENERATION":
                return PowerGeneration;
            case "CITY_ECO_SCORE":
                return CityEcoScore;
            case "CITY_BUILDING_COUNT":
                return CityBuildingCount;
            default:
                return 0;
        }
    }

    // 표준 키 이름에 맞는 누적값을 증가시킵니다.
    public bool AddValue(string canonicalKey, int amount)
    {
        switch (canonicalKey)
        {
            case "THERMAL_POWER":
                ThermalPower += amount;
                return true;
            case "HYDRO_POWER":
                HydroPower += amount;
                return true;
            case "SOLAR_POWER":
                SolarPower += amount;
                return true;
            case "WIND_POWER":
                WindPower += amount;
                return true;
            case "HYDROGEN":
                Hydrogen += amount;
                return true;
            case "ELECTRIC_ENERGY":
                ElectricEnergy += amount;
                return true;
            case "CARBON":
                Carbon += amount;
                return true;
            case "POWER_GENERATION":
                PowerGeneration += amount;
                return true;
            case "CITY_ECO_SCORE":
                CityEcoScore += amount;
                return true;
            case "CITY_BUILDING_COUNT":
                CityBuildingCount += amount;
                return true;
            default:
                return false;
        }
    }
}
