using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
public class CardView : MonoBehaviour, IPointerClickHandler
{
    public Image cardImage;
    public Sprite cardBack;
    public CardData cardData;


    public void Setup(CardData card, bool faceUp)
    {
        cardData = card;
        if (faceUp)
            cardImage.sprite = card.cardSprite;
        else
            cardImage.sprite = cardBack;
    }

    
       public void OnPointerClick(PointerEventData eventData)
    {
        TurnManager.instance.TryPlayCard(this);
    }
}