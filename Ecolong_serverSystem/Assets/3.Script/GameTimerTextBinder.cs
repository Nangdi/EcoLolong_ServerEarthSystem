using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// GameTimer의 남은 시간을 받아 TMP_Text에 MM:SS 형식으로 표시합니다.
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

    private void Awake()
    {
        if (_gameTimer == null)
            _gameTimer = GameTimer.Instance;
    }

    private void OnEnable()
    {
        Subscribe();
        RefreshText();
    }

    private void OnDisable()
    {
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
        _remainingTimeText.text = string.Format(_timeFormat, minutes, seconds, milliseconds);
    }
}
