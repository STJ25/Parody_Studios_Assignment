using UnityEngine;

public class CollectibleBehaviour : MonoBehaviour
{
    [Header("Data")]
    public CollectibleData data;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        CollectionManager.Instance.CollectItem(data);
        gameObject.SetActive(false);
    }
}