using UnityEngine;
using System;

public class CollectionManager : MonoBehaviour
{
    public static CollectionManager Instance { get; private set; }

    [Header("Data")]
    public CollectibleData collectibleData;
    
    public static event Action <int>OnItemCollected;
    public static event Action OnGoalReached;
    public static event Action OnTimeExpired;
    public static event Action <float>OnTimerUpdated;

    private int   currentCount    = 0;
    private float remainingTime;
    private bool  isTimerRunning  = false;
    private bool  goalReached     = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        remainingTime = collectibleData.timeLimit;
    }

    private void Update()
    {
        if (!isTimerRunning || goalReached) return;

        remainingTime -= Time.deltaTime;
        OnTimerUpdated?.Invoke(remainingTime);

        if (remainingTime <= 0f)
        {
            remainingTime  = 0f;
            isTimerRunning = false;
            OnTimeExpired?.Invoke();
        }
    }

    public void StartGame()
    {
        isTimerRunning = true;
    }

    public void CollectItem(CollectibleData data)
    {
        if (!isTimerRunning || goalReached) return;

        currentCount += data.pointValue;
        OnItemCollected?.Invoke(currentCount);

        if (currentCount >= collectibleData.goalAmount)
        {
            goalReached    = true;
            isTimerRunning = false;
            OnGoalReached?.Invoke();
        }
    }
}