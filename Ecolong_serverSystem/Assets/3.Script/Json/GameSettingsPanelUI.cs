using System;
using System.Collections;
using System.Collections.Generic;
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

    // 구역 제목과 보충 설명에 쓰는 색상입니다. (TMP 리치 텍스트 태그로 들어갑니다)
    private const string AdminHeaderColor = "#7EC8FF";
    private const string DeveloperHeaderColor = "#FFB86B";
    private const string DescriptionColor = "#9AA0A6";
    private const string DeveloperSectionDescription = "설치·밸런싱용 항목입니다. 값이 잘못되면 게임이 정상 동작하지 않을 수 있습니다.";

    private JsonManager _jsonManager;

    // 행 생성이 끝났는지 여부입니다. (패널이 처음 켜질 때 Start보다 OnEnable이 먼저 불립니다)
    private bool _isBuilt;

    // 개발자 구역의 행 목록과 헤더입니다. 헤더를 클릭하면 아래 행들을 한 번에 접거나 펼칩니다.
    private readonly List<GameObject> _developerRows = new List<GameObject>();
    private TextMeshProUGUI _developerHeader;
    private bool _isDeveloperSectionVisible;

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

        // 키 설정 버튼이 관리자 구역 쪽에 끼어들 수 있으므로, 개발자 구역을 다시 맨 아래로 내립니다.
        MoveDeveloperSectionToEnd();

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
    // 관리자 목록을 먼저, 개발자 목록을 그 아래에 구역을 나눠 배치합니다.
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

        // 관리자 구역: 현장 운영자가 평소에 만지는 항목만 펼쳐 둡니다.
        CreateSectionHeader("관리자 설정", "전시 운영 중 조정하는 항목입니다.", AdminHeaderColor);
        BuildRowsForGroup(fields, settings, SettingGroup.Admin, null);

        // 개발자 구역: 헤더를 눌러 펼치고 접습니다. 기본은 접힌 상태입니다.
        _developerHeader = CreateSectionHeader("개발자 설정", DeveloperSectionDescription, DeveloperHeaderColor);
        BuildRowsForGroup(fields, settings, SettingGroup.Developer, _developerRows);

        if (_developerHeader != null)
        {
            // 헤더 텍스트 자체를 클릭 영역으로 씁니다. (TextMeshProUGUI가 Graphic이라 별도 이미지가 필요 없습니다)
            Button toggleButton = _developerHeader.gameObject.AddComponent<Button>();
            toggleButton.targetGraphic = _developerHeader;
            toggleButton.onClick.AddListener(ToggleDeveloperSection);
        }

        SetDeveloperSectionVisible(false);
    }

    // 구역 제목 줄을 만듭니다. 입력칸 없이 제목 + 설명만 표시합니다.
    private TextMeshProUGUI CreateSectionHeader(string title, string description, string colorHex)
    {
        // 값 행과 같은 레이아웃 구조로 만들어야 세로 레이아웃이 높이를 제대로 계산합니다.
        GameObject row = new GameObject($"Header_{title}", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(_settingPanel, false);

        HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.spacing = 20f;

        TextMeshProUGUI header = Instantiate(_textTemplate, row.transform);
        header.name = "SectionHeader";
        header.text = BuildHeaderText(title, description, colorHex);
        // 헤더를 클릭해 접고 펼치므로 레이캐스트를 켜 둡니다. (템플릿에서 꺼져 있을 수 있습니다)
        header.raycastTarget = true;
        header.gameObject.SetActive(true);

        return header;
    }

    // 제목은 크고 진하게, 설명은 작고 흐리게 한 줄로 이어 붙입니다.
    private static string BuildHeaderText(string title, string description, string colorHex)
    {
        string text = $"<b><color={colorHex}>── {title} ──</color></b>";

        if (!string.IsNullOrEmpty(description))
            text += $"\n<size=65%><color={DescriptionColor}>{description}</color></size>";

        return text;
    }

    // 지정한 그룹에 속한 필드만 골라 행을 만듭니다. createdRows가 있으면 만든 행을 담아 둡니다.
    private void BuildRowsForGroup(FieldInfo[] fields, GameSettingData settings, SettingGroup group, List<GameObject> createdRows)
    {
        foreach (FieldInfo field in fields)
        {
            SettingFieldAttribute attribute = field.GetCustomAttribute<SettingFieldAttribute>();

            // 특성이 없는 필드는 개발자 목록에 필드 이름 그대로 표시합니다. (표시가 누락되지 않도록)
            SettingGroup fieldGroup = attribute != null ? attribute.Group : SettingGroup.Developer;
            if (fieldGroup != group)
                continue;

            GameObject row = new GameObject($"{field.Name}_Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_settingPanel, false);

            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.spacing = 20f;

            TextMeshProUGUI label = Instantiate(_textTemplate, row.transform);
            label.name = $"Text_{field.Name}";
            label.text = BuildLabelText(field, attribute);
            label.gameObject.SetActive(true);

            TMP_InputField input = Instantiate(_inputFieldTemplate, row.transform);
            input.name = $"InputField_{field.Name}";
            input.text = ValueToString(field.GetValue(settings));
            input.gameObject.SetActive(true);

            createdRows?.Add(row);
        }
    }

    // 한글 라벨과 보충 설명을 한 덩어리 텍스트로 만듭니다. 특성이 없으면 필드 이름을 그대로 씁니다.
    private static string BuildLabelText(FieldInfo field, SettingFieldAttribute attribute)
    {
        if (attribute == null)
            return field.Name;

        string text = attribute.Label;

        if (!string.IsNullOrEmpty(attribute.Description))
            text += $"\n<size=65%><color={DescriptionColor}>{attribute.Description}</color></size>";

        return text;
    }

    // 개발자 구역 헤더를 눌렀을 때 펼치기/접기를 전환합니다.
    private void ToggleDeveloperSection()
    {
        SetDeveloperSectionVisible(!_isDeveloperSectionVisible);
    }

    // 개발자 구역의 행들을 한 번에 보이거나 숨기고, 헤더 제목에 현재 상태를 표시합니다.
    private void SetDeveloperSectionVisible(bool visible)
    {
        _isDeveloperSectionVisible = visible;

        for (int i = 0; i < _developerRows.Count; i++)
        {
            if (_developerRows[i] != null)
                _developerRows[i].SetActive(visible);
        }

        if (_developerHeader == null)
            return;

        string title = visible ? "개발자 설정 (클릭해서 접기) ▲" : "개발자 설정 (클릭해서 펼치기) ▼";
        _developerHeader.text = BuildHeaderText(title, DeveloperSectionDescription, DeveloperHeaderColor);
    }

    // 키 설정 버튼 등 다른 UI가 추가된 뒤에도 개발자 구역이 항상 맨 아래에 오도록 순서를 정리합니다.
    private void MoveDeveloperSectionToEnd()
    {
        if (_developerHeader != null)
            _developerHeader.transform.parent.SetAsLastSibling();

        for (int i = 0; i < _developerRows.Count; i++)
        {
            if (_developerRows[i] != null)
                _developerRows[i].transform.SetAsLastSibling();
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
            // 개발자 구역이 접혀 있어도(행이 비활성) 입력값은 그대로 저장되도록 템플릿만 제외합니다.
            if (input == _inputFieldTemplate || !input.name.StartsWith("InputField_"))
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
