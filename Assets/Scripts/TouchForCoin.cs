using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.Serialization;

public class TouchForCoin : MonoBehaviour
{
    private const string CoinPrefabName = "Coin";
    private const string PlayerTag = "Player";

    [FormerlySerializedAs("coinPrefab")]
    [SerializeField] private GameObject CoinPrefab;
    [Min(0f)] public int maxTouches = 1;

    private int touchCount;

    private void Reset()
    {
        AssignCoinPrefabIfNeeded();
    }

    private void OnValidate()
    {
        AssignCoinPrefabIfNeeded();
    }

    private void Awake()
    {
        AssignCoinPrefabIfNeeded();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsTouchedByPlayer(other))
            return;

        if (touchCount >= maxTouches)
            return;

        if (CoinPrefab == null)
        {
            Debug.LogWarning($"TouchForCoin could not find a prefab named {CoinPrefabName}.", this);
            return;
        }

        Instantiate(CoinPrefab, transform.position, Quaternion.identity);
        touchCount++;
    }

    private void AssignCoinPrefabIfNeeded()
    {
        if (CoinPrefab != null)
            return;

#if UNITY_EDITOR
        string[] prefabGuids = AssetDatabase.FindAssets($"{CoinPrefabName} t:prefab");
        foreach (string prefabGuid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject foundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (foundPrefab != null && foundPrefab.name == CoinPrefabName)
            {
                CoinPrefab = foundPrefab;
                return;
            }
        }
#endif
    }

    private static bool IsTouchedByPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        GameObject rootObject = other.transform.root.gameObject;
        return rootObject != null && rootObject.CompareTag(PlayerTag);
    }
}
