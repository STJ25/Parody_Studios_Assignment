using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Score UI")]
    public TextMeshProUGUI collectedText;
    public TextMeshProUGUI remainingText;

    [Header("Timer UI")]
    public TextMeshProUGUI timerText;

    [Header("Data")]
    public CollectibleData collectibleData;

    private void OnEnable()
    {
        CollectionManager.OnItemCollected += UpdateScoreUI;
        CollectionManager.OnTimerUpdated  += UpdateTimerUI;
        CollectionManager.OnGoalReached   += ShowGoalReached;
        CollectionManager.OnTimeExpired   += ShowTimeExpired;
    }

    private void OnDisable()
    {
        CollectionManager.OnItemCollected -= UpdateScoreUI;
        CollectionManager.OnTimerUpdated  -= UpdateTimerUI;
        CollectionManager.OnGoalReached   -= ShowGoalReached;
        CollectionManager.OnTimeExpired   -= ShowTimeExpired;
    }

    private void Start()
    {
        // Initialize UI with base stuff
        collectedText.text = $"0";
        remainingText.text = $"{collectibleData.goalAmount}";
        timerText.text = $"Time : {collectibleData.timeLimit:F1}";
    }

    private void UpdateScoreUI(int currentCount)
    {
        int remaining = Mathf.Max(0, collectibleData.goalAmount - currentCount);
        collectedText.text = $"{currentCount}";
        remainingText.text = $"{remaining}";
    }

    private void UpdateTimerUI(float remainingTime)
    {
        timerText.text = $"Time : {Mathf.Max(0, remainingTime):F1}";
    }

    private void ShowGoalReached()
    {
        timerText.text = "You Win!";
    }

    private void ShowTimeExpired()
    {
        timerText.text = "Time's Up!";
    }
}