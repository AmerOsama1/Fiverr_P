using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public GameObject Win,Lose;
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
    if (Win != null)
    {
        checkSprite(Win);
    }
    _SoundManager.PlaySoundclipOneShot(_SoundManager.WinClip,_SoundManager.sc);
    Time.timeScale = 0;
}

void PlayerLose()
{
   // Debug.Log("Player Lose!");
    if (Lose != null)
    {
        checkSprite(Lose);
    }
    _SoundManager.PlaySoundclipOneShot(_SoundManager.LoseClip,_SoundManager.sc);
    Time.timeScale = 0;
}

void checkSprite(GameObject image){
   image.SetActive(true);
}
}