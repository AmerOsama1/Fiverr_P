using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class DeckManager : MonoBehaviour
{
    public static DeckManager instance;
    public List<CardData> deck = new List<CardData>();
    public List<PlayerBase> players = new List<PlayerBase>();
    public Transform deckSpawnPoint;
    public RectTransform tableCardPoint;
    public int currentPlayerIndex = 0;
    public CardData firstCard;
    public int copiesPerCard = 2;
    public int cardsPerPlayer = 7;
    public GameObject cardPrefab;
    public TurnManager turnManager;
    SoundManager _SoundManager;
    AudioSource Sc;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        _SoundManager = SoundManager.Instance;
        Sc = GetComponent<AudioSource>();
        GenerateDeck();
        Shuffle();
    }

    public void res()
    {
        StartCoroutine(DealCards());
    }

    void GenerateDeck()
    {
        deck.Clear();
        CardData[] cards = Resources.LoadAll<CardData>("Cards");
        foreach (CardData card in cards)
            for (int i = 0; i < copiesPerCard; i++)
                deck.Add(card);
    }

    void Shuffle()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            CardData temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
    }

    public CardData DrawCard()
    {
        if (deck.Count == 0) return null;
        CardData card = deck[0];
        deck.RemoveAt(0);
        return card;
    }

    IEnumerator DealCards()
    {
        for (int i = 0; i < cardsPerPlayer; i++)
        {
            foreach (PlayerBase player in players)
            {
                CardData card = DrawCard();
                player.cards.Add(card);

                GameObject cardObj = Instantiate(
                    cardPrefab,
                    deckSpawnPoint.position,
                    Quaternion.identity,
                    deckSpawnPoint.parent
                );

                _SoundManager.PlaySoundclipOneShot(_SoundManager.CardClip, Sc);

                CardView view = cardObj.GetComponent<CardView>();
                if (player.isBot)
                    view.Setup(card, false);
                else
                    view.Setup(card, true);

                CardMover anim = cardObj.GetComponent<CardMover>();
                if (player.isBot)
                {
                    anim.MoveToWorld(player.botHandPoint);
                    StartCoroutine(SetParentAfterMove(cardObj, player.botHandPoint));
                }
                else
                {
                    anim.MoveToWorld(player.playerHandUI);
                    StartCoroutine(SetParentAfterMove(cardObj, player.playerHandUI));
                }

                yield return new WaitForSeconds(0.15f);
            }
        }

        StartCoroutine(PlaceFirstCard());
    }

    IEnumerator SetParentAfterMove(GameObject card, Transform hand)
    {
        yield return new WaitForSeconds(0.4f);
        card.transform.SetParent(hand, false);
    }

    IEnumerator PlaceFirstCard()
    {
        yield return new WaitForSeconds(0.5f);

        firstCard = DrawCard();

        GameObject cardObj = Instantiate(
            cardPrefab,
            deckSpawnPoint.position,
            Quaternion.identity,
            tableCardPoint
        );

        _SoundManager.PlaySoundclipOneShot(_SoundManager.CardClip, Sc);

        CardView view = cardObj.GetComponent<CardView>();
        view.Setup(firstCard, true);

        CardMover anim = cardObj.GetComponent<CardMover>();
        anim.MoveToWorld(tableCardPoint);

        turnManager.currentTableCard = firstCard;
        TurnManager.instance.turnloop();

        if (firstCard.type == CardType.Wild || firstCard.type == CardType.WildDraw4)
        {
            CardColor randomColor = (CardColor)Random.Range(0, 4);
            firstCard.color = randomColor;
            turnManager.UpdateColorIndicator(randomColor);
            Debug.Log("First card was Wild, color set to: " + randomColor);
        }
        else
        {
            turnManager.UpdateColorIndicator(firstCard.color);
        }
    }

    public void DrawCardForPlayer(PlayerBase player)
    {
        CardData card = DrawCard();
        if (card == null) return;

        GameObject cardObj = Instantiate(
            cardPrefab,
            deckSpawnPoint.position,
            Quaternion.identity,
            deckSpawnPoint.parent
        );

        _SoundManager.PlaySoundclipOneShot(_SoundManager.CardClip, Sc);

        CardView view = cardObj.GetComponent<CardView>();
        if (player.isBot)
            view.Setup(card, false);
        else
            view.Setup(card, true);

        CardMover mover = cardObj.GetComponent<CardMover>();
        if (player.isBot)
        {
            mover.MoveToWorld(player.botHandPoint);
            StartCoroutine(SetParentAfterMove(cardObj, player.botHandPoint));
        }
        else
        {
            mover.MoveToWorld(player.playerHandUI);
            StartCoroutine(SetParentAfterMove(cardObj, player.playerHandUI));
        }

        player.cards.Add(card);
    }

    public void PlayerDraw()
    {
        PlayerBase player = players[currentPlayerIndex];

        if (player.isBot) return;

        if (!TurnManager.instance.CanDrawNow())
        {
            Debug.Log("Draw is locked right now.");
            return;
        }

        foreach (CardData card in player.cards)
        {
            if (TurnManager.instance.IsCardPlayable(card))
            {
                Debug.Log("You have a playable card! You cannot draw.");
                return;
            }
        }

        DrawCardForPlayer(player);
        TurnManager.instance.EndPlayerTurn();
    }

    void BotTurn(PlayerBase bot)
    {
        DrawCardForPlayer(bot);
    }
}