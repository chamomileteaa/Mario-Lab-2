using UnityEngine;

[DisallowMultipleComponent]
public class PoolPrewarmConfig : MonoBehaviour
{
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private GameObject fireworksPrefab;

    public GameObject ScorePopupPrefab => scorePopupPrefab;
    public GameObject FireballPrefab => fireballPrefab;
    public GameObject FireworksPrefab => fireworksPrefab;
}
