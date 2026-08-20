using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class GameSettingsPanelUI : MonoBehaviour
{
    [Header("References")]
    [FormerlySerializedAs("settingPanel")]
    [SerializeField] private Transform _settingPanel;
    [FormerlySerializedAs("textTemplate")]
    [SerializeField] private TextMeshProUGUI _textTemplate;
    [FormerlySerializedAs("inputFieldTemplate")]
    [SerializeField] private TMP_InputField _inputFieldTemplate;
    [FormerlySerializedAs("saveButton")]
    [SerializeField] private Button _saveButton;

    private JsonManager _jsonManager;

    // 행 생성이 끝났는지 여부입니다. (패널이 처음 켜질 때 Start보다 OnEnable이 먼저 불립니다)
    private bool _isBuilt;

    // JsonManager를 찾고 현재 게임 설정값으로 UI 행을 생성합니다.
    private void Start()
    {
        _jsonManager = JsonManager.instance != null ? JsonManager.instance : FindObjectOfType<JsonManager>();

        if (_jsonManager == null)
        {
            Debug.LogError("JsonManager not found in the scene.");
            return;
        }

        BuildSettingRows();
        EnsureKeyRebindUI();

        // 설정창 안의 모든 TMP 텍스트를 한글 폰트(NotoSansKR SDF)로 교체합니다.
        // 폰트가 지정되지 않은 텍스트는 TMP 기본 폰트(LiberationSans)로 그려져 한글이 깨지기 때문입니다.
        KoreanFontProvider.ApplyToHierarchy(_settingPanel);

        if (_saveButton != null)
            _saveButton.onClick.AddListener(SaveSettings);

        _isBuilt = true;

        // 키 재지정 등으로 설정값이 코드에서 바뀌면 입력칸 표시도 즉시 따라가게 합니다.
        GameKeyBindings.Changed += RefreshInputsFromData;
    }

    // 패널을 다시 열 때마다 현재 저장값을 입력칸에 반영합니다.
    // (키 설정 버튼으로 바뀐 startKey/replayKey/endKey가 옛 값으로 보이는 것을 막습니다)
    private void OnEnable()
    {
        if (_isBuilt)
            RefreshInputsFromData();
    }

    // 시작/리플레이/종료 키 재지정 기능을 설정창에 붙입니다.
    // 씬에 KeyRebindController가 없으면 이 오브젝트에 추가하고, 버튼/안내문구는 자동 생성합니다.
    private void EnsureKeyRebindUI()
    {
        KeyRebindController rebindController = FindObjectOfType<KeyRebindController>(true);

        if (rebindController == null)
            rebindController = gameObject.AddComponent<KeyRebindController>();

        rebindController.EnsureUI(_settingPanel, _saveButton, _textTemplate);
    }

    // Save 버튼 이벤트가 중복으로 남지 않도록 해제합니다.
    private void OnDestroy()
    {
        if (_saveButton != null)
            _saveButton.onClick.RemoveListener(SaveSettings);

        GameKeyBindings.Changed -= RefreshInputsFromData;
    }

    // 현재 GameSettingData 값을 모든 입력칸에 다시 써넣습니다.
    // 입력칸이 옛 값을 들고 있으면 Save할 때 그 옛 값이 그대로 덮어쓰기 때문에 반드시 최신화가 필요합니다.
    private void RefreshInputsFromData()
    {
        if (_settingPanel == null || _jsonManager == null || _jsonManager.gameSettingData == null)
            return;

        GameSettingData settings = _jsonManager.gameSettingData;
        TMP_InputField[] inputs = _settingPanel.GetComponentsInChildren<TMP_InputField>(true);

        foreach (TMP_InputField input in inputs)
        {
            if (!input.name.StartsWith("InputField_"))
                continue;

            string fieldName = input.name.Substring("InputField_".Length);
            FieldInfo field = typeof(GameSettingData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);

            if (field == null)
                continue;

            string currentValue = ValueToString(field.GetValue(settings));
            if (input.text != currentValue)
                input.SetTextWithoutNotify(currentValue);
        }
    }

    // GameSettingData의 public 필드를 Text + InputField 행으로 자동 생성합니다.
    private void BuildSettingRows()
    {
        if (_settingPanel == null || _textTemplate == null || _inputFieldTemplate == null)
        {
            Debug.LogError("GameSettingsPanelUI has missing references.");
            return;
        }

        GameSettingData settings = _jsonManager.gameSettingData;
        FieldInfo[] fields = typeof(GameSettingData).GetFields(BindingFlags.Instance | BindingFlags.Public);

        _textTemplate.gameObject.SetActive(false);
        _inputFieldTemplate.gameObject.SetActive(false);

        foreach (FieldInfo field in fields)
        {
            GameObject row = new GameObject($"{field.Name}_Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_settingPanel, false);

            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.spacing = 20f;

            TextMeshProUGUI label = Instantiate(_textTemplate, row.transform);
            label.name = $"Text_{field.Name}";
            label.text = field.Name;
            label.gameObject.SetActive(true);

            TMP_InputField input = Instantiate(_inputFieldTemplate, row.transform);
            input.name = $"InputField_{field.Name}";
            input.text = ValueToString(field.GetValue(settings));
            input.gameObject.SetActive(true);
        }
    }

    // InputField에 입력된 값을 GameSettingData에 반영하고 JSON으로 저장합니다.
    private void SaveSettings()
    {
        if (_jsonManager == null || _jsonManager.gameSettingData == null)
            return;

        TMP_InputField[] inputs = _settingPanel.GetComponentsInChildren<TMP_InputField>(true);

        foreach (TMP_InputField input in inputs)
        {
            if (!input.gameObject.activeInHierarchy || !input.name.StartsWith("InputField_"))
                continue;

            string fieldName = input.name.Substring("InputField_".Length);
            FieldInfo field = typeof(GameSettingData).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);

            if (field == null)
                continue;

            if (TryParseValue(input.text, field.FieldType, out object parsedValue))
            {
                field.SetValue(_jsonManager.gameSettingData, parsedValue);
            }
            else
            {
                Debug.LogWarning($"Failed to save {fieldName}. Input: {input.text}");
            }
        }

        _jsonManager.SaveGameSettingData();

        // 저장된 gameTimeScale/총시간을 즉시 런타임에 적용하여 GameTimer와 그래프 갱신 주기가 새 값으로 동작하게 합니다.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameTimeScale(_jsonManager.gameSettingData.gameTimeScale);
            GameManager.Instance.SetGameTotalTime(_jsonManager.gameSettingData.gameTotalTime);
            GameManager.Instance.SetReplayTimerSpeed(_jsonManager.gameSettingData.replayTimerSpeed);
        }

        // 입력필드로 직접 수정한 startKey/replayKey/endKey 문자열도 즉시 반영합니다.
        GameKeyBindings.LoadFromSettings();

        // 파싱에 실패해 반영되지 않은 입력이 있을 수 있으므로, 실제 저장된 값으로 표시를 맞춥니다.
        RefreshInputsFromData();

        // 저장된 DualMonitorSpan 설정을 즉시 스팬 창에 반영합니다(실시간 적용).
        DualMonitorSpanController spanController = FindObjectOfType<DualMonitorSpanController>();
        if (spanController != null)
            spanController.ApplyFromSettings();

        // 저장된 지구상태 레벨 판정 임계값을 즉시 EarthStateManager에 반영합니다(실시간 적용).
        if (EarthStateManager.Instance != null)
            EarthStateManager.Instance.ApplyFromSettings();

        Debug.Log($"Game setting data saved: {_jsonManager.GameDataPath}");
    }

    // 배열 값은 콤마로 이어 붙이고, 일반 값은 문자열로 변환합니다.
    private static string ValueToString(object value)
    {
        if (value == null)
            return string.Empty;

        if (value is IEnumerable enumerable && !(value is string))
        {
            string result = string.Empty;

            foreach (object item in enumerable)
            {
                if (!string.IsNullOrEmpty(result))
                    result += ", ";

                result += Convert.ToString(item, CultureInfo.InvariantCulture);
            }

            return result;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    // InputField 문자열을 대상 필드 타입에 맞게 변환합니다.
    private static bool TryParseValue(string text, Type targetType, out object parsedValue)
    {
        parsedValue = null;

        if (targetType == typeof(string))
        {
            parsedValue = text;
            return true;
        }

        if (targetType == typeof(int))
        {
            bool success = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value);
            parsedValue = value;
            return success;
        }

        if (targetType == typeof(float))
        {
            bool success = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
            parsedValue = value;
            return success;
        }

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(text, out bool value))
            {
                parsedValue = value;
                return true;
            }

            string normalized = text.Trim().ToLowerInvariant();
            if (normalized == "1" || normalized == "yes" || normalized == "y")
            {
                parsedValue = true;
                return true;
            }

            if (normalized == "0" || normalized == "no" || normalized == "n")
            {
                parsedValue = false;
                return true;
            }

            return false;
        }

        if (targetType.IsEnum)
        {
            try
            {
                parsedValue = Enum.Parse(targetType, text, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        if (targetType.IsArray)
        {
            Type elementType = targetType.GetElementType();
            string[] parts = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            Array array = Array.CreateInstance(elementType, parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseValue(parts[i].Trim(), elementType, out object elementValue))
                    return false;

                array.SetValue(elementValue, i);
            }

            parsedValue = array;
            return true;
        }

        return false;
    }
}
