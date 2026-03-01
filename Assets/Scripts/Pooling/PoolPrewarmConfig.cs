using UnityEngine;

[DisallowMultipleComponent]
public class PoolPrewarmConfig : MonoBehaviour
{
    [SerializeField] private GameObject scorePopupPrefab;
    [SerializeField] private GameObject fireballPrefab;

    public GameObject ScorePopupPrefab => scorePopupPrefab;
    public GameObject FireballPrefab => fireballPrefab;
}
