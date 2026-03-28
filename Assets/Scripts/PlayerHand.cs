using System.Collections.Generic;
using UnityEngine;

public class PlayerHand : MonoBehaviour
{
    public List<CardData> hand = new List<CardData>();

    public GameObject cardPrefab;
   public RectTransform handArea;

    public void AddCard(CardData card)
    {
        hand.Add(card);

        GameObject cardObj = Instantiate(cardPrefab, handArea);
        CardView view = cardObj.GetComponent<CardView>();
       view.Setup(card, true);
    }
}