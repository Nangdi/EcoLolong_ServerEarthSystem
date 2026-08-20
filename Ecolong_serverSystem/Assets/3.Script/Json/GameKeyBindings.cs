using System;
using UnityEngine;

// =============================================================================
//  시작 / 리플레이 / 종료 키를 한 곳에서 보관하는 런타임 저장소입니다.
//
//  - 기본값은 S(시작) / R(리플레이) / E(종료)입니다.
//  - 실제 값은 gameSettingData.json의 startKey / replayKey / endKey에 저장되며,
//    ESC 설정창의 "시작/리플레이/종료 키 설정" 버튼(KeyRebindController)에서 변경합니다.
//  - GameManager, EndPanelController 등 실제 입력을 받는 쪽은 이 클래스의
//    StartKey / ReplayKey / EndKey 프로퍼티만 참조하면 됩니다.
// =============================================================================
// 어떤 키가 어떤 기능에 쓰이는지 한 쌍으로 담습니다. (키 겹침 안내용)
public readonly struct KeyUsage
{
    public readonly KeyCode Key;
    public readonly string Description;

    public KeyUsage(KeyCode key, string description)
    {
        Key = key;
        Description = description;
    }
}

public static class GameKeyBindings
{
    public const KeyCode DefaultStartKey = KeyCode.S;
    public const KeyCode DefaultReplayKey = KeyCode.R;
    public const KeyCode DefaultEndKey = KeyCode.E;

    private static KeyCode _startKey = DefaultStartKey;
    private static KeyCode _replayKey = DefaultReplayKey;
    private static KeyCode _endKey = DefaultEndKey;

    // JsonManager가 준비된 뒤 한 번이라도 JSON에서 읽어왔는지 여부입니다.
    // false면 프로퍼티에 접근할 때마다 다시 읽기를 시도합니다.
    private static bool _isLoaded;

    // 키가 바뀌었을 때(설정 저장/재지정) 호출됩니다. 안내 UI 갱신용입니다.
    public static event Action Changed;

    // 키 재지정(안내 문구를 띄우고 입력을 기다리는) 중에는 true입니다.
    // 이 동안에는 게임 쪽 단축키가 동작하지 않도록 각 입력 스크립트가 이 값을 검사합니다.
    public static bool IsRebinding { get; private set; }

    public static KeyCode StartKey
    {
        get { EnsureLoaded(); return _startKey; }
    }

    public static KeyCode ReplayKey
    {
        get { EnsureLoaded(); return _replayKey; }
    }

    public static KeyCode EndKey
    {
        get { EnsureLoaded(); return _endKey; }
    }

    public static void SetRebinding(bool isRebinding)
    {
        IsRebinding = isRebinding;
    }

    // 시작/리플레이/종료 키로 이미 사용 중인 키인지 확인합니다.
    public static bool IsReserved(KeyCode key)
    {
        if (key == KeyCode.None)
            return false;

        return key == StartKey || key == ReplayKey || key == EndKey;
    }

    // 디버그/강제 키 등 "보조 키" 입력을 읽을 때 사용합니다.
    // 설정된 시작/리플레이/종료 키와 겹치면 보조 키 쪽을 무시해 설정 키가 항상 우선하게 합니다.
    // 키 재지정 중에도 false를 돌려주어 눌린 키가 기능으로 이어지지 않게 막습니다.
    public static bool GetSecondaryKeyDown(KeyCode key)
    {
        if (IsRebinding || key == KeyCode.None || IsReserved(key))
            return false;

        return Input.GetKeyDown(key);
    }

    // 현재 지정된 키를 "S / R / E" 형태의 안내 문자열로 돌려줍니다.
    public static string Describe()
    {
        return $"{StartKey} / {ReplayKey} / {EndKey}";
    }

    // 아직 JSON을 읽지 않았다면 읽어옵니다. JsonManager가 없으면 기본값을 유지하고 다음에 다시 시도합니다.
    private static void EnsureLoaded()
    {
        if (_isLoaded)
            return;

        LoadFromSettings();
    }

    // gameSettingData.json에 저장된 키 문자열을 KeyCode로 변환해 반영합니다.
    // GameSettingsPanelUI에서 사용자가 문자열을 직접 고친 뒤 저장했을 때도 호출됩니다.
    public static void LoadFromSettings()
    {
        JsonManager jsonManager = JsonManager.instance;
        if (jsonManager == null || jsonManager.gameSettingData == null)
            return;

        GameSettingData settings = jsonManager.gameSettingData;

        _startKey = ParseOrDefault(settings.startKey, DefaultStartKey);
        _replayKey = ParseOrDefault(settings.replayKey, DefaultReplayKey);
        _endKey = ParseOrDefault(settings.endKey, DefaultEndKey);
        _isLoaded = true;

        Changed?.Invoke();
    }

    // 새로 지정한 세 키를 적용하고, 필요하면 gameSettingData.json에 바로 저장합니다.
    public static void Apply(KeyCode startKey, KeyCode replayKey, KeyCode endKey, bool save)
    {
        _startKey = startKey;
        _replayKey = replayKey;
        _endKey = endKey;
        _isLoaded = true;

        JsonManager jsonManager = JsonManager.instance;
        if (jsonManager != null && jsonManager.gameSettingData != null)
        {
            jsonManager.gameSettingData.startKey = startKey.ToString();
            jsonManager.gameSettingData.replayKey = replayKey.ToString();
            jsonManager.gameSettingData.endKey = endKey.ToString();

            if (save)
                jsonManager.SaveGameSettingData();
        }

        Debug.Log($"[KeyBindings] 시작={startKey} / 리플레이={replayKey} / 종료={endKey}");
        Changed?.Invoke();
    }

    // 저장된 문자열("S", "Alpha1", "F7" 등)을 KeyCode로 변환합니다. 실패하면 기본값을 사용합니다.
    private static KeyCode ParseOrDefault(string text, KeyCode fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallback;

        if (Enum.TryParse(text.Trim(), true, out KeyCode parsed) && parsed != KeyCode.None)
        {
            // ESC는 설정창 토글/재지정 취소 전용이라 게임 키로는 쓸 수 없습니다.
            if (parsed == KeyCode.Escape)
            {
                Debug.LogWarning($"[KeyBindings] ESC는 설정창 전용 키라 지정할 수 없습니다. → 기본값 {fallback} 사용");
                return fallback;
            }

            return parsed;
        }

        Debug.LogWarning($"[KeyBindings] 알 수 없는 키 이름 '{text}' → 기본값 {fallback} 사용");
        return fallback;
    }
}
