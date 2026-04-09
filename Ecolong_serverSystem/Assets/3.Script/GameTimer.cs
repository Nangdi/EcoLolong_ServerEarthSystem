using UnityEngine;

public class GameTimer : MonoBehaviour
{
    public bool IsRunning { get; private set; }
    [SerializeField] private float currentTime;

    public float CurrentTime
    {
        get { return currentTime; }
        private set { currentTime = value; }
    }
    private void Start()
    {
        StartTimer();
    }
    private void Update()
    {
        if (!IsRunning) return;

        CurrentTime += Time.deltaTime;
    }
    public void StartTimer()
    {
        CurrentTime = 0f;
        IsRunning = true;
    }

    public void StopTimer()
    {
        IsRunning = false;
    }

    public void ResetTimer()
    {
        CurrentTime = 0f;
    }

   

    public float GetCurrentTime()
    {
        return CurrentTime;
    }
}