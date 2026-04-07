using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Score UI")]
    public TextMeshProUGUI collectedText;
    public TextMeshProUGUI remainingText;

    [Header("Timer UI")]
    public TextMeshProUGUI timerText;

    [Header("Start Panel")]
    public CanvasGroup startPanelCanvasGroup;

    [Header("End Panel")]
    public CanvasGroup endPanelCanvasGroup;
    public TextMeshProUGUI endPanelText;

    [Header("Data")]
    public CollectibleData collectibleData;

    private void OnEnable()
    {
        CollectionManager.OnItemCollected += UpdateScoreUI;
        CollectionManager.OnTimerUpdated  += UpdateTimerUI;
        CollectionManager.OnGoalReached   += HandleGoalReached;
        CollectionManager.OnTimeExpired   += HandleTimeExpired;
    }

    private void OnDisable()
    {
        CollectionManager.OnItemCollected -= UpdateScoreUI;
        CollectionManager.OnTimerUpdated  -= UpdateTimerUI;
        CollectionManager.OnGoalReached   -= HandleGoalReached;
        CollectionManager.OnTimeExpired   -= HandleTimeExpired;
    }

    private void Start()
    {
        collectedText.text = $"0";
        remainingText.text = $"{collectibleData.goalAmount}";
        timerText.text     = $"Time : {collectibleData.timeLimit:F1}";

        SetPanel(startPanelCanvasGroup, true);
        SetPanel(endPanelCanvasGroup,   false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void OnStartButtonPressed()
    {
        SetPanel(startPanelCanvasGroup, false);
        CollectionManager.Instance.StartGame();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnRestartButtonPressed()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void UpdateScoreUI(int currentCount)
    {
        int remaining      = Mathf.Max(0, collectibleData.goalAmount - currentCount);
        collectedText.text = $"{currentCount}";
        remainingText.text = $"{remaining}";
    }

    private void UpdateTimerUI(float remainingTime)
    {
        timerText.text = $"Time : {Mathf.Max(0, remainingTime):F1}";
    }

    private void HandleGoalReached()
    {
        ShowEndPanel("You Win!");
    }

    private void HandleTimeExpired()
    {
        ShowEndPanel("Time's Up!");
    }

    public void ShowEndPanel(string message)
    {
        endPanelText.text = message;
        SetPanel(endPanelCanvasGroup, true);
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void HideEndPanel()
    {
        SetPanel(endPanelCanvasGroup, false);
        Time.timeScale = 1f;
    }

    private void SetPanel(CanvasGroup group, bool isVisible)
    {
        group.alpha          = isVisible ? 1f : 0f;
        group.interactable   = isVisible;
        group.blocksRaycasts = isVisible;
    }
}