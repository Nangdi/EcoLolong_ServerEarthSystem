using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
public class GameSettingData
{
    public bool useUnityOnTop;
    public int[] displayIndex = { 0, 1, 2 };
}
public class GameDynamicData
{
}
public class PortJson
{
    public string com = "COM4";
    public int baudLate = 19200;
}

public class JsonManager : MonoBehaviour
{

    public static JsonManager instance;
    public GameSettingData gameSettingData;
    public PortJson portJson;
    public GameDynamicData gameDynamicData;
    private string gameDataPath;
    private string gameDynamicDataPath;
    private string portPath;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }

        portPath = Path.Combine(Application.streamingAssetsPath, "port.json");
        gameDynamicDataPath = Path.Combine(Application.streamingAssetsPath, "Setting.json");
        gameDataPath = Path.Combine(Application.persistentDataPath, "gameSettingData.json");
        gameSettingData.displayIndex = new int[] { 0, 1, 2 };
        gameSettingData = LoadData(gameDataPath, gameSettingData);
        gameDynamicData = LoadData(gameDynamicDataPath, gameDynamicData);
        portJson = LoadData(portPath, portJson);
    }

    //저장할 json 객체 , 경로설정
    public static void SaveData<T>(T jsonObject, string path) where T : new()
    {
        if (jsonObject == null)
            jsonObject = new T();  // 기본 생성자로 객체 초기화
        string json = JsonUtility.ToJson(jsonObject, true);
        File.WriteAllText(path, json);
        Debug.Log($"저장됨: {path}");
    }

    public static T LoadData<T>(string path, T data) where T : new()
    {
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("JSON 파일이 존재하지 않습니다.");
                SaveData(data, path);
            }
            Debug.Log("JSON로드");
            string json = File.ReadAllText(path);
            T jsonData = JsonUtility.FromJson<T>(json);
            return jsonData;
        }

        //예시 실행코드
        //JsonManager.LoadData(파일경로 , 데이터클래스);

    }
}
