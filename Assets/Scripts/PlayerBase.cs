using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
public class PlayerBase : MonoBehaviour
{
    public List<CardData> cards = new List<CardData>();
    public bool isBot;
public Image turnTimerImage;
    public RectTransform playerHandUI; // للاعب الحقيقي
    public Transform botHandPoint;     // للبوت

    public CardView GetCardView(CardData data)
{
    foreach (CardView card in GetComponentsInChildren<CardView>())
    {
        if (card.cardData == data)
            return card;
    }

    return null;
}
}