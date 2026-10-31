using UnityEngine;
using TMPro;


public class Timer : MonoBehaviour
{
        public float timeRemaining = 120f;
        public TMP_Text timerText;
        public GameObject LoseMenu;

        private bool timerRunning = true; 

        void Start()
    {
        if (LoseMenu != null)
        LoseMenu.SetActive(false);

    }
    

    // Update is called once per frame
    void Update()
    {
        if (!timerRunning)
        return;

        timeRemaining -= Time.deltaTime;
        timeRemaining = Mathf.Max(timeRemaining, 0f);
        
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);

        if (timerText != null)
            timerText.text = $"{minutes:00}:{seconds:00}";

            if (timeRemaining <= 0f)
        {
            timerRunning = false;
            
            if (LoseMenu != null)
            LoseMenu.SetActive(true);

            Time.timeScale = 1f;

            Debug.Log("mission Succeeded!");
        
        }
    }
        
        public void StopTimer()
        {
            timerRunning = false;
        } 

    
}
