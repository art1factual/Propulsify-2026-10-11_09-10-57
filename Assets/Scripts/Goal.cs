using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;


public class Goal : MonoBehaviour
{ 

    public GameObject Win;
    public Button RestartButton;
    public Animator newspaperAnimator;

    public string openingAnimation = "_";
    public string closingAnimation = "_";

    public float closingDuration = .833f;

    private bool hasWon = false;
    private bool isClosing = false;

    public Animator restartAnimator;
    public string restartAnimation = "_";

    public float restartDuration = 0.833f;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        //Time.timeScale = 1f;

        Win.SetActive(false);
        
        newspaperAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        
        RestartButton.onClick.AddListener(RestartScene);
        
    
       

    }
    public void NextButton()
    {
        if (isClosing || !hasWon) return;

        isClosing = true;
        StartCoroutine(CloseNewspaper());
   
        
    }

    // Update is called once per frame
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || hasWon)  
        return;

        hasWon = true;
        
        

        Win.SetActive(true);

        newspaperAnimator.Play(openingAnimation, 0, 0f);

        Time.timeScale = 0f;

        

        Debug.Log("Win condition reached!");
        
    } 

    private IEnumerator WinSequence()
    {
        Win.SetActive(true);

        newspaperAnimator.Rebind();
        newspaperAnimator.Update(0f);
        
        newspaperAnimator.Play("Terrorism Attack", 0, 0f);

        Debug.Log("Win Condition Reached!");
        
        yield break;

        Time.timeScale = 0f;
        
    }
    public void RestartScene()
    {
       
        if(isClosing) return;

        isClosing = true;
        StartCoroutine(RestartSequence());
    }

    IEnumerator RestartSequence()
    {
        restartAnimator.gameObject.SetActive(true);
        restartAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;

        restartAnimator.Play(restartAnimation, 0, 0f);

        yield return new WaitForSecondsRealtime(restartDuration);

        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    IEnumerator CloseNewspaper()
    {
        

        newspaperAnimator.Play("TerrorismAttackEnd", 0, 0f);

        yield return new WaitForSecondsRealtime(closingDuration);

        Time.timeScale = 1f;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        
    }
}
