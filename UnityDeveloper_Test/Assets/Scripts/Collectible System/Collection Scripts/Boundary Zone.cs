using UnityEngine;

public class BoundaryZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        UIManager uiManager = FindFirstObjectByType<UIManager>();
        uiManager.ShowEndPanel("Out of Bounds!");
    }
}