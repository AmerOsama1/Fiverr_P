using UnityEngine;
using TMPro;
public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public GameObject resultPopup;
    public TextMeshProUGUI resultText;
    SoundManager _SoundManager;



    void Awake(){instance = this;}

    
    void  Start() {_SoundManager =SoundManager.Instance; }


 public void CheckWin(PlayerBase player)
    {
     if (player.cards.Count == 0)

    {
       if (player.isBot) { PlayerLose();  }
       else { PlayerWin(); }
    }
}

void PlayerWin()
{
   // Debug.Log("Player Win!");
    if (resultPopup != null)
    {
        resultText.text = "YOU WIN!";
        resultPopup.SetActive(true);
    }
    _SoundManager.PlaySoundclipOneShot(_SoundManager.WinClip,_SoundManager.sc);
    Time.timeScale = 0;
}

void PlayerLose()
{
   // Debug.Log("Player Lose!");
    if (resultPopup != null)
    {
        resultText.text = "YOU LOSE!";
        resultPopup.SetActive(true);
    }
    _SoundManager.PlaySoundclipOneShot(_SoundManager.LoseClip,_SoundManager.sc);
    Time.timeScale = 0;
}
}