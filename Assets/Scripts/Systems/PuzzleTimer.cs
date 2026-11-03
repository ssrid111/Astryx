using UnityEngine;
using UnityEngine.Events;

// =============================================================================
// PuzzleTimer
// Author: Hamitha Viyyapu
//
// Per-room puzzle countdown timer. This is SEPARATE from oxygen (the ambient
// survival resource). The Fate Roulette's EXTRA TIME and HAZARD outcomes
// modify this timer, per the Fate Roulette System Logic Specification.
// =============================================================================
public class PuzzleTimer : MonoBehaviour
{
    [SerializeField] private float startingTime = 60f;
    private float timeRemaining;
    private bool  isRunning;
    private bool  stopped;   // room finished -> stays stopped even after a menu closes

    public float TimeRemaining => timeRemaining;
    public bool  IsRunning     => isRunning;
    public UnityEvent OnTimeExpired;   // hook puzzle-failure here later

    public void StartTimer(float duration = -1f)
    {
        timeRemaining = duration > 0f ? duration : startingTime;
        isRunning = true;
        stopped = false;
        Debug.Log($"[TIMER] Puzzle timer started at {timeRemaining:0}s");
    }

    public void StopTimer() { isRunning = false; stopped = true; }

    // Pause / resume without resetting the remaining time (used when a menu opens).
    public void Pause()  => isRunning = false;
    public void Resume() { if (!stopped && timeRemaining > 0f) isRunning = true; }

    private void Update() => Tick(Time.deltaTime);

    // Counts the timer down. Public so the tests can step it without waiting.
    public void Tick(float deltaTime)
    {
        if (!isRunning) return;
        timeRemaining -= deltaTime;
        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            isRunning = false;
            Debug.Log("[TIMER] Puzzle time expired!");
            OnTimeExpired?.Invoke();
        }
    }

    // XTRA TIME buff -> add seconds to the puzzle countdown
    public void AddTime(float seconds)
    {
        timeRemaining += seconds;   // does not change running state (stays frozen if paused)
        Debug.Log($"[TIMER] +{seconds:0}s -> {timeRemaining:0}s");
    }

    // HAZARD debuff -> deduct seconds from the puzzle countdown
    public void RemoveTime(float seconds)
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - seconds);
        Debug.Log($"[TIMER] -{seconds:0}s -> {timeRemaining:0}s");
    }
}
