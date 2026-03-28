using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.SceneManagement;
public class LoadingScreen : MonoBehaviour
{
    public Image loadingImage;
    public float loadingTime = 5f;

    void Start()
    {
        StartCoroutine(LoadingRoutine());
    }


    IEnumerator LoadingRoutine()
    {
        float timer = 0f;
        while (timer < loadingTime)
        {
            timer += Time.deltaTime;
            loadingImage.fillAmount = timer / loadingTime;
            yield return null;
        }
        loadingImage.fillAmount = 1f;
        SceneManager.LoadScene("MainMenu");
    }
    
}