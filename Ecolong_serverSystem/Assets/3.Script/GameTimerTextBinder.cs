using TMPro;
using UnityEngine;

// GameTimer의 남은 시간을 받아 TMP_Text에 MM:SS 형식으로 표시합니다.
public class GameTimerTextBinder : MonoBehaviour
{
    [Header("Timer 연결")]
    [SerializeField] private GameTimer gameTimer;

    [Header("표시 대상")]
    [SerializeField] private TMP_Text remainingTimeText;

    [Tooltip("남은 시간 표시 형식. {0}=분, {1}=초, {2}=밀리초")]
    [SerializeField] private string timeFormat = "{0:00}:{1:00}:{2:00}";

    private void Awake()
    {
        if (gameTimer == null)
            gameTimer = GameTimer.Instance;
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
        if (gameTimer == null)
            gameTimer = GameTimer.Instance;

        if (gameTimer != null)
            gameTimer.OnTimeChanged += OnTimeChanged;
    }

    private void Unsubscribe()
    {
        if (gameTimer != null)
            gameTimer.OnTimeChanged -= OnTimeChanged;
    }

    private void OnTimeChanged(float currentTime)
    {
        RefreshText();
    }

    // 시작 전이나 종료 시점에도 15:00이나 00:00이 정상적으로 보이도록 별도 함수로 분리합니다.
    private void RefreshText()
    {
        if (remainingTimeText == null)
            return;

        float remaining = gameTimer != null ? gameTimer.GetRemainingTime() : 0f;
        int totalSeconds = Mathf.FloorToInt(remaining);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        int milliseconds = Mathf.FloorToInt((remaining - totalSeconds) * 100);
        remainingTimeText.text = string.Format(timeFormat, minutes, seconds, milliseconds);
    }
}
