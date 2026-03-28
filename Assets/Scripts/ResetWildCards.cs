using UnityEngine;

public class ResetWildCards : MonoBehaviour
{
    public  CardData[] CardRB;
        public  CardData[] CardYG;

    void Start()
    {
        CardData[] cards = Resources.LoadAll<CardData>("Cards");
reset();
        foreach (CardData card in cards)
        {
            if (card.type == CardType.Wild || card.type == CardType.WildDraw4)
            {
                card.color = CardColor.wild; 
            }
        }
    }
    public void  reset()
    {
        foreach (var card in CardYG)
        {
                 card.color =CardColor.yellow;
       card.secondColor =CardColor.green;
        }
      
 foreach (var card in CardRB)
        {
                 card.color =CardColor.blue;
       card.secondColor =CardColor.red;
        }
     
    }
}