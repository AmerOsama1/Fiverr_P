using UnityEngine;
using UnityEngine.SceneManagement;
public class MainMenu : MonoBehaviour
{
public void Play(string name)
    {
        SceneManager.LoadScene(name);
    }

    public void Restart()
    {
                Time.timeScale = 1;

      SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMain()
    {
                Time.timeScale = 1;

        SceneManager.LoadScene("MainMenu");
    }
      public void Exit()
    {
        Application.Quit();
    }
    


}
