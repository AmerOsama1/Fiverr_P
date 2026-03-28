using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TurnManager : MonoBehaviour
{
    public List<PlayerBase> players = new List<PlayerBase>();
    public DeckManager deckManager;
    public ResetWildCards reset;
    public GameObject colorPickerPanel;
    public Image currentColorImage;
    public GameObject redButton, blueButton, greenButton, yellowButton;
    public GameObject unoButton;

    [Header("Color Flash Popup")]
    public GameObject colorPopupObject;
    public Image colorPopupImage;
    public float colorPopupDuration = 2f;

    public bool  useTurnTimer   = true;
    public float playerTurnTime = 10f;
    public float botThinkTime   = 2f;
    public float hintDelay      = 5f;
    public bool  easyMode       = true;

    public int      currentPlayerIndex = 0;
    public bool     isPlayerTurn       = false;
    public CardData currentTableCard;

    public static TurnManager instance;


    int  turnDirection       = 1;
    public int drawStack     = 0;
    int  skipNextPlayerIndex = -1;
    bool playerPlayed        = false;
    bool jumpInHappened      = false;
    bool skipNextTurn        = false;   
    bool unoCalled           = false;
    bool isTurnLoopRunning   = false;

    bool inStackingPhase     = false;
    bool stackOccurred       = false;

    Coroutine playerTurnRoutine;
    Coroutine hintRoutine;
    Coroutine unoRoutine;
    Coroutine botJumpInRoutine;
    Coroutine colorPopupRoutine;

    Dictionary<CardData, PlayerBase> cardOwnerMap = new Dictionary<CardData, PlayerBase>();

    AudioSource  audioSource;
    SoundManager soundManager;

    static readonly Color ClockGreen  = new Color(0.18f, 0.80f, 0.44f);
    static readonly Color ClockYellow = new Color(1f,    0.82f, 0.10f);
    static readonly Color ClockRed    = new Color(0.93f, 0.23f, 0.23f);

    // ═════════════════════════════════════════════════════════════════
    #region Unity Lifecycle
    // ═════════════════════════════════════════════════════════════════

    void Awake() => instance = this;

    void Start()
    {
        soundManager = SoundManager.Instance;
        audioSource  = GetComponent<AudioSource>();
        if (colorPopupObject != null) colorPopupObject.SetActive(false);
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Turn Loop
    // ═════════════════════════════════════════════════════════════════

    public void turnloop() => StartCoroutine(TurnLoop());

    IEnumerator TurnLoop()
    {
        if (isTurnLoopRunning) yield break;
        isTurnLoopRunning = true;

        while (true)
        {
            if (skipNextTurn)
            {
                skipNextTurn = false;
                Debug.Log($"{players[currentPlayerIndex].name} turn skipped.");
                AdvanceTurn();
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            foreach (var p in players)
                if (p.turnTimerImage != null)
                    p.turnTimerImage.fillAmount = 0;

            PlayerBase currentPlayer = players[currentPlayerIndex];

            if (currentPlayer.isBot)
                yield return StartCoroutine(BotTurn(currentPlayer));
            else
            {
                playerTurnRoutine = StartCoroutine(PlayerTurn(currentPlayer));
                yield return playerTurnRoutine;
            }


            if (jumpInHappened)
            {
                jumpInHappened      = false;
                skipNextPlayerIndex = -1;
            }
            else
            {
                AdvanceTurn();
            }

            yield return new WaitForSeconds(0.1f);
            CheckBotJumpIn();
        }
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Player Turn
    // ═════════════════════════════════════════════════════════════════

    IEnumerator PlayerTurn(PlayerBase player)
    {
        playerPlayed    = false;
        inStackingPhase = false;
        stackOccurred   = false;

        if (!easyMode && drawStack > 0)
        {
            yield return StartCoroutine(DrawPenaltyPlayerTurn(player));
            yield break;
        }

        if (easyMode)
            hintRoutine = StartCoroutine(ShowPlayableHint(player));

        SetClockColor(player, ClockGreen);
        yield return StartCoroutine(RunClockTimer(player, playerTurnTime, () => playerPlayed));

        if (!playerPlayed)
        {
            Debug.Log("Time up! Player draws a card.");
            deckManager.DrawCardForPlayer(player);
        }

        StopHintRoutine();
        ResetCardHints(player);
        playerPlayed = true;
    }

  
    IEnumerator DrawPenaltyPlayerTurn(PlayerBase player)
    {
        inStackingPhase = true;
        stackOccurred   = false;
        Debug.Log($"Player targeted with +{drawStack}. Checking for stackable card...");

        CardData stackable = GetPlayerStackableCard(player);

        if (stackable == null)
        {
            inStackingPhase = false;
            Debug.Log($"No stackable card — auto-draw {drawStack}.");
            for (int i = 0; i < drawStack; i++)
                deckManager.DrawCardForPlayer(player);
            drawStack    = 0;
            playerPlayed = true;
            yield break;
        }

        Debug.Log($"Stackable card found — giving 2s window.");
        SetClockColor(player, ClockYellow);
        float window = 2f;
        float timer  = window;

        while (timer > 0 && !playerPlayed)
        {
            timer -= Time.deltaTime;
            UpdateClockFill(player, Mathf.Clamp01(timer / window));
            UpdateClockColor(player, Mathf.Clamp01(timer / window));
            yield return null;
        }

        inStackingPhase = false;

        if (!playerPlayed)
        {
            Debug.Log($"Stack window expired — auto-draw {drawStack}.");
            for (int i = 0; i < drawStack; i++)
                deckManager.DrawCardForPlayer(player);
            drawStack = 0;
        }

        playerPlayed = true;
    }

    public bool CanDrawNow() => !inStackingPhase;

    public void EndPlayerTurn() => playerPlayed = true;

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Bot Turn
    // ═════════════════════════════════════════════════════════════════

    IEnumerator BotTurn(PlayerBase bot)
    {
        if (!easyMode && drawStack > 0)
        {
            PlayerBase human = players.Find(p => !p.isBot);
            bool humanCanStack = human != null && GetPlayerStackableCard(human) != null;

            if (humanCanStack)
            {
                Debug.Log($"Bot {bot.name} has penalty — pausing 2.5s for human to stack.");
                inStackingPhase = true;
                yield return new WaitForSeconds(2.5f);
                inStackingPhase = false;
            }

            if (stackOccurred)
            {
                Debug.Log($"{bot.name} penalty cancelled — human stacked.");
                stackOccurred = false;
                yield break;
            }

            for (int i = 0; i < drawStack; i++)
                deckManager.DrawCardForPlayer(bot);
            Debug.Log($"{bot.name} drew {drawStack} penalty cards.");
            drawStack    = 0;
            skipNextTurn = true;
            yield break;
        }

        bool isDrawCard = currentTableCard != null
                       && (currentTableCard.type == CardType.Draw2 || currentTableCard.type == CardType.WildDraw4);
        if (isDrawCard)
        {
            int nextIdx           = WrapIndex(currentPlayerIndex + turnDirection);
            PlayerBase nextPlayer = players[nextIdx];
            if (!nextPlayer.isBot && GetPlayerStackableCard(nextPlayer) != null)
            {
                Debug.Log("Bot pausing 2.5s — human has stackable card.");
                yield return new WaitForSeconds(2.5f);
            }
        }

        SetClockColor(bot, ClockGreen);
        yield return StartCoroutine(RunClockTimer(bot, botThinkTime, null));

        if (bot.turnTimerImage != null) bot.turnTimerImage.fillAmount = 0f;

        yield return new WaitForSeconds(0.3f);

        CardData playable = GetBotPlayableCard(bot);

        if (playable != null)
        {
            if (playable.type == CardType.Draw2 || playable.type == CardType.Wild || playable.type == CardType.WildDraw4)
            {
                CardColor chosen = (playable.secondColor != playable.color && Random.value > 0.5f)
                    ? playable.secondColor
                    : playable.color;
                playable.color = chosen;
                UpdateColorIndicator(chosen);
                ShowColorPopup(chosen);
            }

            PlayBotCard(bot, playable);
        }
        else
        {
            deckManager.DrawCardForPlayer(bot);
        }

        Debug.Log("Bot turn finished.");
    }

    CardData GetBotPlayableCard(PlayerBase bot)
    {
        foreach (CardData card in bot.cards)
            if (IsCardPlayable(card)) return card;
        return null;
    }


    bool CanStackOn(CardData candidate, CardData tableCard)
    {
        if (candidate.type != tableCard.type) return false;
        bool sameColors = (candidate.color == tableCard.color && candidate.secondColor == tableCard.secondColor)
                       || (candidate.color == tableCard.secondColor && candidate.secondColor == tableCard.color);
        return sameColors;
    }

    CardData GetBotStackableCard(PlayerBase bot)
    {
        foreach (CardData card in bot.cards)
            if (CanStackOn(card, currentTableCard)) return card;
        return null;
    }

    CardData GetPlayerStackableCard(PlayerBase player)
    {
        foreach (CardData card in player.cards)
            if (CanStackOn(card, currentTableCard)) return card;
        return null;
    }

    void PlayBotCard(PlayerBase bot, CardData card)
    {
        bot.cards.Remove(card);
        currentTableCard      = card;
        deckManager.firstCard = currentTableCard;
        UpdateColorIndicator(card.color);

        Debug.Log("Bot played: " + card.name);

        CardView cardView = bot.GetCardView(card);
        if (cardView == null) return;

        cardView.Setup(card, true);
        soundManager.PlaySoundclipOneShot(soundManager.CardClip, audioSource);

        CardMover mover = cardView.GetComponent<CardMover>();
        mover.MoveToWorld(deckManager.tableCardPoint);
        cardView.transform.SetParent(deckManager.tableCardPoint);

        ApplyCardEffects(card, isBot: true);
        GameManager.instance.CheckWin(bot);

        ScheduleBotJumpInCheck();
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Player Card Play
    // ═════════════════════════════════════════════════════════════════

    public void TryPlayCard(CardView cardView)
    {
        PlayerBase player = GetPlayerFromCard(cardView);
        if (player == null) return;

        bool isMyTurn = (players[currentPlayerIndex] == player);

        if (isMyTurn)
        {
            if (!IsCardPlayable(cardView.cardData))
            {
                if (!easyMode)
                {
                    Debug.Log("Invalid card! Player draws 1 penalty card (Hard mode).");
                    deckManager.DrawCardForPlayer(player);
                }
                return;
            }

            if (easyMode && drawStack > 0)
            {
                bool isStackCard = cardView.cardData.type == CardType.Draw2 ||
                                   cardView.cardData.type == CardType.WildDraw4;
                if (isStackCard)
                {
                    Debug.Log("Easy mode: stacking not allowed — you must draw.");
                    return;
                }
            }

            if (!easyMode && drawStack > 0 && inStackingPhase)
            {
                if (!CanStackOn(cardView.cardData, currentTableCard))
                {
                    Debug.Log("Hard mode: card cannot stack — must be identical card.");
                    return;
                }
            }

            PlayPlayerCard(player, cardView);
        }
        else if (CanJumpIn(cardView.cardData))
        {
            PlayAnyCard(player, cardView);
            if (playerTurnRoutine != null) StopCoroutine(playerTurnRoutine);
            playerPlayed = true;
        }
    }

    void PlayPlayerCard(PlayerBase player, CardView cardView)
    {
        CardData card = cardView.cardData;

        player.cards.Remove(card);
        currentTableCard      = card;
        deckManager.firstCard = card;

        soundManager.PlaySoundclipOneShot(soundManager.CardClip, audioSource);

        CardMover mover = cardView.GetComponent<CardMover>();
        mover.MoveToWorld(deckManager.tableCardPoint);
        cardView.transform.SetParent(deckManager.tableCardPoint);

        Debug.Log("Player played: " + card.name);

        StopHintRoutine();

        if (player.cards.Count == 1)
        {
            unoButton.SetActive(true);
            unoCalled  = false;
            unoRoutine = StartCoroutine(UNOCountdown(player));
        }

        if (card.type == CardType.Wild || card.type == CardType.WildDraw4)
        {
            OpenColorPicker();
            return;
        }

        ApplyCardEffects(card, isBot: false);

        ResetCardHints(player);
        playerPlayed = true;

        GameManager.instance.CheckWin(player);
        ScheduleBotJumpInCheck();
    }

    void ApplyCardEffects(CardData card, bool isBot)
    {
        switch (card.type)
        {
            // ── Skip:─
            case CardType.Skip:
                skipNextTurn = true;
                Debug.Log("Skip played — next player's turn will be skipped.");
                break;

            // ── Reverse ──
            case CardType.Reverse:
                turnDirection *= -1;
                if (players.Count == 2)
                    skipNextTurn = true; 
                break;

            // ── Draw2 ──
            case CardType.Draw2:
                if (easyMode)
                {
                    int nextIdx     = WrapIndex(currentPlayerIndex + turnDirection);
                    PlayerBase next = players[nextIdx];
                    deckManager.DrawCardForPlayer(next);
                    deckManager.DrawCardForPlayer(next);
                    Debug.Log($"{next.name} drew 2 cards (Easy Draw2).");
                    skipNextTurn = true;
                }
                else
                {
                    if (drawStack > 0) stackOccurred = true;
                    drawStack += 2;
                    Debug.Log($"Hard: drawStack={drawStack}. Penalty passed forward.");
                }

                if (isBot)
                {
                    CardColor chosen = Random.value > 0.5f ? card.color : card.secondColor;
                    currentTableCard.color = chosen;
                    UpdateColorIndicator(chosen);
                    ShowColorPopup(chosen);
                }
                else
                {
                    OpenTwoColorPicker(card.color, card.secondColor);
                }
                break;

            // ── Wild ──
            case CardType.Wild:
                if (isBot)
                {
                    CardColor newColor = GetRandomColor();
                    currentTableCard.color = newColor;
                    UpdateColorIndicator(newColor);
                    ShowColorPopup(newColor);
                    Debug.Log("Bot Wild → " + newColor);
                }
                break;

            // ── WildDraw4 ──
            case CardType.WildDraw4:
                if (easyMode)
                {
                    StartCoroutine(HandleDraw4Easy());
                }
                else
                {
                    if (drawStack > 0) stackOccurred = true;
                    drawStack += 4;
                    Debug.Log($"Hard: drawStack={drawStack}. Penalty passed forward (WD4).");
                }

                if (isBot)
                {
                    CardColor randomColor = GetRandomColor();
                    currentTableCard.color = randomColor;
                    UpdateColorIndicator(randomColor);
                    ShowColorPopup(randomColor);
                }
                break;
        }
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Color Picker
    // ═════════════════════════════════════════════════════════════════

    public void OpenColorPicker()
    {
        EnableAllColors();
        colorPickerPanel.SetActive(true);
    }

    public void OpenTwoColorPicker(CardColor color1, CardColor color2)
    {
        redButton.SetActive(false);
        blueButton.SetActive(false);
        greenButton.SetActive(false);
        yellowButton.SetActive(false);

        EnableColorButton(color1);
        EnableColorButton(color2);
        colorPickerPanel.SetActive(true);
    }

    public void SetColor(CardColor color)
    {
        currentTableCard.color = color;
        colorPickerPanel.SetActive(false);

        
        if (easyMode)
        {
            currentColorImage.color = ColorFromCardColor(color);
        }
        else
        {
            ShowColorPopup(color);
            StartCoroutine(HideIndicatorAfterDelay(colorPopupDuration));
        }

        
        if (currentTableCard.type == CardType.WildDraw4)
        {
            if (easyMode)
                StartCoroutine(HandleDraw4Easy());
            else
            {
                drawStack    += 4;
                stackOccurred = false;
                Debug.Log($"Hard: drawStack={drawStack} after WD4 color chosen.");
            }
        }

        StartCoroutine(DelayedReset(7f));
        playerPlayed = true;

        GameManager.instance.CheckWin(players.Find(p => !p.isBot));
        ScheduleBotJumpInCheck();
    }

    IEnumerator HideIndicatorAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!easyMode)
            currentColorImage.color = Color.clear;
    }

    IEnumerator DelayedReset(float delay)
    {
        yield return new WaitForSeconds(delay);
        reset.reset();
    }

    public void SetRed()    => SetColor(CardColor.red);
    public void SetBlue()   => SetColor(CardColor.blue);
    public void SetGreen()  => SetColor(CardColor.green);
    public void SetYellow() => SetColor(CardColor.yellow);

    public void UpdateColorIndicator(CardColor color)
    {
       
        if (easyMode)
            currentColorImage.color = ColorFromCardColor(color);
    }

    Color ColorFromCardColor(CardColor color) => color switch
    {
        CardColor.red    => Color.red,
        CardColor.blue   => Color.blue,
        CardColor.green  => Color.green,
        CardColor.yellow => Color.yellow,
        _                => currentColorImage.color
    };

    void EnableAllColors()
    {
        redButton.SetActive(true);
        blueButton.SetActive(true);
        greenButton.SetActive(true);
        yellowButton.SetActive(true);
    }

    void EnableColorButton(CardColor color)
    {
        switch (color)
        {
            case CardColor.red:    redButton.SetActive(true);    break;
            case CardColor.blue:   blueButton.SetActive(true);   break;
            case CardColor.green:  greenButton.SetActive(true);  break;
            case CardColor.yellow: yellowButton.SetActive(true); break;
        }
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Color Popup (Hard Mode)
    // ═════════════════════════════════════════════════════════════════

    public void ShowColorPopup(CardColor color)
    {
        if (colorPopupObject == null || colorPopupImage == null) return;
        if (colorPopupRoutine != null) StopCoroutine(colorPopupRoutine);
        colorPopupRoutine = StartCoroutine(ColorPopupRoutine(color));
    }

    IEnumerator ColorPopupRoutine(CardColor color)
    {
        colorPopupImage.color = ColorFromCardColor(color);
        colorPopupObject.SetActive(true);
        yield return new WaitForSeconds(colorPopupDuration);
        colorPopupObject.SetActive(false);
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Draw Penalty
    // ═════════════════════════════════════════════════════════════════

    IEnumerator HandleDraw4Easy()
    {
        int nextIdx     = WrapIndex(currentPlayerIndex + turnDirection);
        PlayerBase next = players[nextIdx];

        for (int i = 0; i < 4; i++)
            deckManager.DrawCardForPlayer(next);

        skipNextTurn = true;
        yield return new WaitForSeconds(0.4f);
        Debug.Log($"{next.name} drew 4 cards (Easy WD4) and is skipped.");
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Jump-In
    // ═════════════════════════════════════════════════════════════════

    bool CanJumpIn(CardData card)
    {
       
        if (easyMode) return false;

        CardData top = deckManager.firstCard;
        if (card.color != top.color) return false;

        if (card.type == CardType.Number && top.type == CardType.Number)
            return card.Number == top.Number;

        return card.type == top.type && card.type != CardType.Number;
    }

    void PlayAnyCard(PlayerBase player, CardView cardView)
    {
        CardData card = cardView.cardData;

        player.cards.Remove(card);
        currentTableCard      = card;
        deckManager.firstCard = card;
        cardView.Setup(card, true);

        CardMover mover = cardView.GetComponent<CardMover>();
        mover.MoveToWorld(deckManager.tableCardPoint);
        cardView.transform.SetParent(deckManager.tableCardPoint);

        Debug.Log(player.name + " Jump-In!");

        int skippedPlayerIndex = currentPlayerIndex;
        currentPlayerIndex     = players.IndexOf(player);
        jumpInHappened         = true;

        HandleJumpInCardEffects(card);
        GameManager.instance.CheckWin(player);

        if (playerTurnRoutine != null) StopCoroutine(playerTurnRoutine);

        skipNextPlayerIndex = skippedPlayerIndex;
        currentPlayerIndex  = WrapIndex(currentPlayerIndex + turnDirection);
    }

    void HandleJumpInCardEffects(CardData card)
    {
        switch (card.type)
        {
            case CardType.Draw2:
                if (!easyMode) { drawStack += 2; stackOccurred = true; }
                break;
            case CardType.WildDraw4:
                if (!easyMode) { drawStack += 4; stackOccurred = true; }
                break;
            case CardType.Skip:
                skipNextTurn = true;
                break;
            case CardType.Reverse:
                turnDirection *= -1;
                break;
        }
    }

    void CheckBotJumpIn()
    {
        
        if (easyMode) return;

        PlayerBase current = players[currentPlayerIndex];

        foreach (PlayerBase bot in players)
        {
            if (!bot.isBot || bot == current) continue;

            foreach (CardData card in bot.cards)
            {
                if (!CanJumpIn(card)) continue;

                CardView view = bot.GetCardView(card);
                if (view != null)
                {
                    Debug.Log(bot.name + " Jump-In!");
                    PlayAnyCard(bot, view);
                    return;
                }
            }
        }
    }

    void ScheduleBotJumpInCheck()
    {
        if (botJumpInRoutine != null) StopCoroutine(botJumpInRoutine);
        botJumpInRoutine = StartCoroutine(CheckBotJumpInDelayed());
    }

    IEnumerator CheckBotJumpInDelayed()
    {
        yield return new WaitForSeconds(0.2f);
        CheckBotJumpIn();
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region UNO
    // ═════════════════════════════════════════════════════════════════

    public void CallUNO()
    {
        unoCalled = true;
        unoButton.SetActive(false);
        if (unoRoutine != null) StopCoroutine(unoRoutine);
        Debug.Log("UNO called!");
    }

    IEnumerator UNOCountdown(PlayerBase player)
    {
        yield return new WaitForSeconds(3f);
        if (!unoCalled)
        {
            Debug.Log("Player forgot UNO! Draw penalty.");
            deckManager.DrawCardForPlayer(player);
            unoButton.SetActive(false);
        }
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Clock Timer UI
    // ═════════════════════════════════════════════════════════════════

    IEnumerator RunClockTimer(PlayerBase player, float duration, System.Func<bool> stopCondition)
    {
        float timer = duration;
        if (player.turnTimerImage != null) player.turnTimerImage.fillAmount = 1f;

        while (timer > 0)
        {
            if (stopCondition != null && stopCondition()) break;
            timer -= Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);
            UpdateClockFill(player, t);
            UpdateClockColor(player, t);
            yield return null;
        }

        UpdateClockFill(player, 0f);
    }

    void UpdateClockFill(PlayerBase player, float t)
    {
        if (player.turnTimerImage != null)
            player.turnTimerImage.fillAmount = t;
    }

    void UpdateClockColor(PlayerBase player, float t)
    {
        if (player.turnTimerImage == null) return;
        Color c = t > 0.5f
            ? Color.Lerp(ClockYellow, ClockGreen,  (t - 0.5f) * 2f)
            : Color.Lerp(ClockRed,    ClockYellow,  t * 2f);
        player.turnTimerImage.color = c;
    }

    void SetClockColor(PlayerBase player, Color c)
    {
        if (player.turnTimerImage != null)
            player.turnTimerImage.color = c;
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Hints
    // ═════════════════════════════════════════════════════════════════

    IEnumerator ShowPlayableHint(PlayerBase player)
    {
        if (!easyMode) yield break;
        yield return new WaitForSeconds(hintDelay);
        if (playerPlayed) yield break;

        foreach (CardData card in player.cards)
        {
            if (!IsCardPlayable(card)) continue;
            CardView view = player.GetCardView(card);
            if (view != null)
            {
                var pos = view.transform.localPosition;
                pos.y = 30f;
                view.transform.localPosition = pos;
            }
        }
    }

    void ResetCardHints(PlayerBase player)
    {
        foreach (CardData card in player.cards)
        {
            CardView view = player.GetCardView(card);
            if (view == null) continue;
            var pos = view.transform.localPosition;
            pos.y = 0f;
            view.transform.localPosition = pos;
        }
    }

    void StopHintRoutine()
    {
        if (hintRoutine != null) { StopCoroutine(hintRoutine); hintRoutine = null; }
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Helpers
    // ═════════════════════════════════════════════════════════════════

    void AdvanceTurn() =>
        currentPlayerIndex = WrapIndex(currentPlayerIndex + turnDirection);

    int WrapIndex(int index)
    {
        int count = players.Count;
        return ((index % count) + count) % count;
    }

    public bool IsCardPlayable(CardData card)
    {
        if (card.color       == currentTableCard.color) return true;
        if (card.secondColor == currentTableCard.color) return true;

        if (card.type == CardType.Number && currentTableCard.type == CardType.Number)
            if (card.Number == currentTableCard.Number) return true;

        if (card.type != CardType.Number && card.type == currentTableCard.type) return true;
        if (card.type == CardType.Wild || card.type == CardType.WildDraw4) return true;

        return false;
    }

    PlayerBase GetPlayerFromCard(CardView cardView)
    {
        if (cardOwnerMap.TryGetValue(cardView.cardData, out PlayerBase owner))
            return owner;

        foreach (PlayerBase p in players)
        {
            if (p.cards.Contains(cardView.cardData))
            {
                cardOwnerMap[cardView.cardData] = p;
                return p;
            }
        }
        return null;
    }

    public void RebuildCardOwnerCache()
    {
        cardOwnerMap.Clear();
        foreach (PlayerBase p in players)
            foreach (CardData card in p.cards)
                cardOwnerMap[card] = p;
    }

    CardColor GetRandomColor() => (CardColor)Random.Range(0, 4);

    #endregion
}
