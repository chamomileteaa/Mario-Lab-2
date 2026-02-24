using UnityEngine;

public class CollectableAudio : MonoBehaviour
{
    [SerializeField] private AudioCue collectCue;

    public void Collect()
    {
        AudioSource.PlayClipAtPoint(
            collectCue.clip,
            transform.position,
            collectCue.volume
        );
        Destroy(gameObject);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
