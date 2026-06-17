using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

// GameTimer의 남은 시간을 받아 TMP_Text에 MM:SS 형식으로 표시합니다.
// 게임 실행(Play) 전 에디터에서도 미리보기가 갱신되도록 ExecuteAlways로 동작합니다.
[ExecuteAlways]
public class GameTimerTextBinder : MonoBehaviour
{
    [Header("Timer 연결")]
    [FormerlySerializedAs("gameTimer")]
    [SerializeField] private GameTimer _gameTimer;

    [Header("표시 대상")]
    [FormerlySerializedAs("remainingTimeText")]
    [SerializeField] private TMP_Text _remainingTimeText;

    [Tooltip("남은 시간 표시 형식. {0}=분, {1}=초, {2}=밀리초")]
    [FormerlySerializedAs("timeFormat")]
    [SerializeField] private string _timeFormat = "{0:00}:{1:00}:{2:00}";

    [Header("고정폭 (숫자 흔들림 방지)")]
    [Tooltip("켜면 모든 글자 폭을 고정해(TMP <mspace>) 자릿수가 바뀌어도 좌우로 흔들리지 않습니다.")]
    [SerializeField] private bool _useMonospace = true;
    [Tooltip("고정 글자폭(em). 폰트에 맞춰 0.5~0.7 정도로 조절하세요. 숫자가 잘리면 키우고, 너무 벌어지면 줄입니다.")]
    [Min(0.1f)]
    [SerializeField] private float _monospaceEm = 0.6f;

    private void Awake()
    {
        if (_gameTimer == null)
            _gameTimer = GameTimer.Instance;

        // <mspace> 태그가 동작하려면 리치 텍스트가 켜져 있어야 합니다.
        if (_remainingTimeText != null && _useMonospace)
            _remainingTimeText.richText = true;
    }

    private void OnEnable()
    {
        // 타이머 이벤트 구독은 플레이 중에만 합니다. 에디터(시작 전)에서는 미리보기만 갱신합니다.
        if (Application.isPlaying)
            Subscribe();
        RefreshText();
    }

    // 인스펙터에서 고정폭 값(_useMonospace/_monospaceEm 등)을 바꾸면 즉시 반영되도록 다시 그립니다.
    // 플레이 중은 물론, 게임 실행 전 에디터에서도 미리보기가 갱신됩니다.
    private void OnValidate()
    {
        if (_remainingTimeText != null && _useMonospace)
            _remainingTimeText.richText = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // OnValidate 도중 직접 텍스트를 바꾸면 경고가 날 수 있어 한 프레임 뒤에 갱신합니다.
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    RefreshText();
            };
            return;
        }
#endif
        RefreshText();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            Unsubscribe();
    }

    private void Subscribe()
    {
        if (_gameTimer == null)
            _gameTimer = GameTimer.Instance;

        if (_gameTimer != null)
            _gameTimer.OnTimeChanged += OnTimeChanged;
    }

    private void Unsubscribe()
    {
        if (_gameTimer != null)
            _gameTimer.OnTimeChanged -= OnTimeChanged;
    }

    private void OnTimeChanged(float currentTime)
    {
        RefreshText();
    }

    // 시작 전이나 종료 시점에도 15:00이나 00:00이 정상적으로 보이도록 별도 함수로 분리합니다.
    private void RefreshText()
    {
        if (_remainingTimeText == null)
            return;

        float remaining = _gameTimer != null ? _gameTimer.GetRemainingTime() : 0f;
        int totalSeconds = Mathf.FloorToInt(remaining);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        int milliseconds = Mathf.FloorToInt((remaining - totalSeconds) * 100);
        string formatted = string.Format(_timeFormat, minutes, seconds, milliseconds);

        // 모든 글자(숫자/콜론)를 동일 폭으로 고정해 자릿수 변화에 따른 좌우 흔들림을 없앱니다.
        if (_useMonospace)
        {
            string em = _monospaceEm.ToString("0.###", CultureInfo.InvariantCulture);
            formatted = $"<mspace={em}em>{formatted}</mspace>";
        }

        _remainingTimeText.text = formatted;
    }
}
