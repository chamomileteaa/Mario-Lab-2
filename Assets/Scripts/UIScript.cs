using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class UIScript : MonoBehaviour
{
    public TMP_Text livesTxt;

    public TMP_Text coinsTxt;

    public TMP_Text timerTxt;
    public float timeLeft;
    bool gameOver = false;

    public TMP_Text scoreTxt;

    //add audio here whenever coin collected??


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        timeLeft = 400;
    }

    // Update is called once per frame
    void Update()
    {
        if (timeLeft > 0)
        {
            timeLeft -= Time.deltaTime;
            timerTxt.text = "TIME " + timeLeft.ToString("000");
        }
        else if (!gameOver)
        {
            gameOver = true;
            GameManager.Dead();
        }

    }



    public void UpdateLives()
    {
        livesTxt.text = "Lives x " + GameData.lives.ToString("0"); 
        //only on beginning transition scene
        //change to mario sprite
    }

    public void UpdateText()
    {
        coinsTxt.text = "Coins x " + GameData.coins.ToString("00");
        //change to coin sprite
    }

    public void UpdateScore()
    {
        scoreTxt.text = "MARIO " + GameData.score.ToString("000000");
    }

}
