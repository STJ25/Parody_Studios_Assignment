using UnityEngine;

[CreateAssetMenu(fileName = "CollectibleData", menuName = "Collection System/Collectible Data")]
public class CollectibleData : ScriptableObject
{
    [Header("Identity")]
    public string itemName = "Coin";
    public int pointValue = 1;

    [Header("Goal Settings")]
    public int goalAmount = 10;
    public float timeLimit = 30f;

    // [Header("Feedback")] //
    // public GameObject collectVFXPrefab;
    // public AudioClip collectSFX;
}