using UnityEngine;


public class BotSettings : MonoBehaviour
{
    public void SetBotCount(int count)
    {
        PlayerPrefs.SetInt("BotCount", count);
        PlayerPrefs.Save();

        Debug.Log("Saved Bot Count: " + count);
    }
}