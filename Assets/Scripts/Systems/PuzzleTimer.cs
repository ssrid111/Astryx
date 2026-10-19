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

    public float TimeRemaining => timeRemaining;
    public bool  IsRunning     => isRunning;
    public UnityEvent OnTimeExpired;   // hook puzzle-failure here later

    public void StartTimer(float duration = -1f)
    {
        timeRemaining = duration > 0f ? duration : startingTime;
        isRunning = true;
        Debug.Log($"[TIMER] Puzzle timer started at {timeRemaining:0}s");
    }

    public void StopTimer() => isRunning = false;

    private void Update()
    {
        if (!isRunning) return;
        timeRemaining -= Time.deltaTime;
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
        timeRemaining += seconds;
        if (!isRunning && seconds > 0f) isRunning = true;
        Debug.Log($"[TIMER] +{seconds:0}s -> {timeRemaining:0}s");
    }

    // HAZARD debuff -> deduct seconds from the puzzle countdown
    public void RemoveTime(float seconds)
    {
        timeRemaining = Mathf.Max(0f, timeRemaining - seconds);
        Debug.Log($"[TIMER] -{seconds:0}s -> {timeRemaining:0}s");
    }
}
