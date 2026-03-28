
using UnityEngine;

public class BotIntializer : MonoBehaviour
{
    public PlayerBase[] botPlayers;
    public GameObject[] timer;

    void Start()
    {
        int bots = PlayerPrefs.GetInt("BotCount");

        for (int i = 0; i < botPlayers.Length; i++)
        {
            if (i < bots)
                botPlayers[i].gameObject.SetActive(true);
            else
                botPlayers[i].gameObject.SetActive(false);
        }

     for (int i =0; i < bots; i++)
        {
           timer[i].SetActive(true);
       }
        
        TurnManager.instance.players.Clear();

foreach (PlayerBase p in botPlayers)
{
    if (p.gameObject.activeSelf)
     TurnManager.instance.players.Add(p);
}
      DeckManager.instance.players.Clear();
      
foreach (PlayerBase p in botPlayers )
{
    if (p.gameObject.activeSelf){
        DeckManager.instance.players.Add(p);}

}
     DeckManager.instance.res();
    }
}