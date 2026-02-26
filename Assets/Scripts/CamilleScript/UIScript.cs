using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class UIScript : MonoBehaviour
{
    public Image marioPic;
    public Image coinPic; 

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
        if (livesTxt != null)
        livesTxt.text = GameData.lives.ToString("0");
        //only on beginning transition scene
        //change to mario sprite
    }

    public void UpdateCoins()
    {
        if (coinsTxt != null)
        coinsTxt.text = GameData.coins.ToString("00");
        //change to coin sprite
    }

    public void UpdateScore()
    {
        if (scoreTxt != null)
        scoreTxt.text = "MARIO " + GameData.score.ToString("000000");
    }

    public void UpdateUI()
    {
        UpdateLives();
        UpdateCoins();
        UpdateScore();
    }

}


