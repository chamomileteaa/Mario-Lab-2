using System;
using UnityEngine;
using UnityEngine.LowLevelPhysics2D;

public class FlagPoleCalculations : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    //retuern the part ofthe pole that mario touches
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        
        PoleZone zone = GetComponent<PoleZone>();

        int score = zone.GoalScore;
        
        Debug.Log("Added score: " + score);
    }
    

    // Update is called once per frame
    void Update()
    {
        
    }
}
