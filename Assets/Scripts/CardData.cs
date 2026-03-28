using UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "Scriptable Objects/CardData")]
public class CardData : ScriptableObject
{
    
public CardColor color;
public CardColor secondColor;

public CardType type ;
public int Number;
 public Sprite cardSprite;
}

public enum CardColor
{
    red,blue,green,yellow,wild
}
public enum CardType
{
    Number,Skip,Reverse,Draw2,Wild,WildDraw4
}

