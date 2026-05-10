using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TurnManager : MonoBehaviour
{
    // ─── Public References ───────────────────────────────────────────
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

    // ─── Turn Settings ────────────────────────────────────────────────
    public bool  useTurnTimer      = true;
    public float playerTurnTime    = 10f;
    public float botThinkTime      = 2f;
    public float hintDelay         = 5f;
    public bool  easyMode          = true;
    public float stackWindowTime   = 3f;   

    // ─── State ────────────────────────────────────────────────────────
    public int      currentPlayerIndex = 0;
    public bool     isPlayerTurn       = false;
    public CardData currentTableCard;

    public static TurnManager instance;

    // ─── Private State ────────────────────────────────────────────────
    int  turnDirection     = 1;
    bool playerPlayed      = false;
    bool jumpInHappened    = false;
    bool skipNextTurn      = false;
    bool unoCalled         = false;
    bool isTurnLoopRunning = false;

    // ── Stack System ──────────────────────────────────────────────────
   
    int  pendingDrawCount  = 0;
    int  pendingDrawTarget = -1;   
    bool inStackWindow     = false;

    Coroutine currentBotRoutine;
    Coroutine playerTurnRoutine;
    Coroutine stackWindowRoutine;
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

            // Reset timers
            foreach (var p in players)
                if (p.turnTimerImage != null)
                    p.turnTimerImage.fillAmount = 0;

            PlayerBase currentPlayer = players[currentPlayerIndex];

            if (currentPlayer.isBot)
            {
                currentBotRoutine = StartCoroutine(BotTurn(currentPlayer));
                yield return currentBotRoutine;
                currentBotRoutine = null;
            }
            else
            {
                playerTurnRoutine = StartCoroutine(PlayerTurn(currentPlayer));
                yield return playerTurnRoutine;
                playerTurnRoutine = null;
            }

            if (jumpInHappened)
                jumpInHappened = false;
            else
                AdvanceTurn();

            yield return new WaitForSeconds(0.1f);
            CheckBotJumpIn();
        }
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Stack Window
    // ═════════════════════════════════════════════════════════════════


    void OpenStackWindow(int targetIndex, int addedCards)
    {
        if (easyMode)
        {
            PlayerBase target = players[targetIndex];
            for (int i = 0; i < addedCards; i++)
                deckManager.DrawCardForPlayer(target);
            Debug.Log($"{target.name} drew {addedCards} cards (Easy, no stacking).");
            skipNextTurn = true;
            return;
        }

        pendingDrawCount  += addedCards;
        pendingDrawTarget  = targetIndex;
        inStackWindow      = true;

        Debug.Log($"Stack window open — target: {players[targetIndex].name}, total: {pendingDrawCount}");

        if (stackWindowRoutine != null) StopCoroutine(stackWindowRoutine);
        stackWindowRoutine = StartCoroutine(StackWindowCountdown());
    }

    IEnumerator StackWindowCountdown()
    {
        float timer = stackWindowTime;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            yield return null;

            if (!inStackWindow) yield break;
        }

        inStackWindow = false;
        if (pendingDrawTarget >= 0 && pendingDrawTarget < players.Count)
        {
            PlayerBase target = players[pendingDrawTarget];
            Debug.Log($"Window expired — {target.name} draws {pendingDrawCount} cards.");

            for (int i = 0; i < pendingDrawCount; i++)
                deckManager.DrawCardForPlayer(target);

            currentPlayerIndex = WrapIndex(pendingDrawTarget + turnDirection);
            pendingDrawCount   = 0;
            pendingDrawTarget  = -1;

            RestartTurnLoop();
        }
    }

    void OnPlayerStacked(PlayerBase stacker, int addedCards)
    {
        if (stackWindowRoutine != null) StopCoroutine(stackWindowRoutine);
        inStackWindow = false;

        int stackerIndex   = players.IndexOf(stacker);
        int newTargetIndex = WrapIndex(stackerIndex + turnDirection);

        Debug.Log($"{stacker.name} stacked +{addedCards}! New target: {players[newTargetIndex].name}, total: {pendingDrawCount + addedCards}");

        OpenStackWindow(newTargetIndex, addedCards);
    }

    void RestartTurnLoop()
    {
        StopAllCoroutines();
        isTurnLoopRunning = false;
        playerPlayed      = false;
        inStackWindow     = false;
        StartCoroutine(TurnLoop());
    }

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Player Turn
    // ═════════════════════════════════════════════════════════════════

    IEnumerator PlayerTurn(PlayerBase player)
    {
        playerPlayed = false;

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

    public bool CanDrawNow() => true;

    public void EndPlayerTurn() => playerPlayed = true;

    #endregion

    // ═════════════════════════════════════════════════════════════════
    #region Bot Turn
    // ═════════════════════════════════════════════════════════════════

    IEnumerator BotTurn(PlayerBase bot)
    {
        SetClockColor(bot, ClockGreen);
        yield return StartCoroutine(RunClockTimer(bot, botThinkTime, null));

        if (bot.turnTimerImage != null) bot.turnTimerImage.fillAmount = 0f;
        yield return new WaitForSeconds(0.3f);

        // If there's an open stack window, bot tries to stack
        if (inStackWindow && pendingDrawTarget == players.IndexOf(bot))
        {
            CardData stackCard = GetBotStackableCard(bot);
            if (stackCard != null)
            {
                PlayBotCard(bot, stackCard);
                yield break;
            }
            yield break;
        }

        CardData playable = GetBotPlayableCard(bot);

        if (playable != null)
        {
            if (playable.type == CardType.Draw2 || playable.type == CardType.Wild || playable.type == CardType.WildDraw4)
            {
                CardColor chosen = (playable.secondColor != playable.color && Random.value > 0.5f)
                    ? playable.secondColor
                    : playable.color;
                playable.color = chosen;
                SetColorIndicatorForMode(chosen);
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

    CardData GetBotStackableCard(PlayerBase bot)
    {
        foreach (CardData card in bot.cards)
            if (IsIdenticalPlusCard(card, currentTableCard)) return card;
        return null;
    }

    void PlayBotCard(PlayerBase bot, CardData card)
    {
        bot.cards.Remove(card);
        currentTableCard      = card;
        deckManager.firstCard = currentTableCard;

        Debug.Log("Bot played: " + card.name);

        CardView cardView = bot.GetCardView(card);
        if (cardView == null) return;

        cardView.Setup(card, true);
        soundManager.PlaySoundclipOneShot(soundManager.CardClip, audioSource);

        CardMover mover = cardView.GetComponent<CardMover>();
        mover.MoveToWorld(deckManager.tableCardPoint);
        cardView.transform.SetParent(deckManager.tableCardPoint);

        ApplyCardEffects(card, isBot: true, player: bot);
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

        if (!easyMode && inStackWindow && IsIdenticalPlusCard(cardView.cardData, currentTableCard))
        {
            if (currentBotRoutine  != null) { StopCoroutine(currentBotRoutine);  currentBotRoutine  = null; }
            if (playerTurnRoutine  != null) { StopCoroutine(playerTurnRoutine);  playerTurnRoutine  = null; }
            if (stackWindowRoutine != null) { StopCoroutine(stackWindowRoutine); stackWindowRoutine = null; }

            CardData card = cardView.cardData;
            player.cards.Remove(card);
            currentTableCard      = card;
            deckManager.firstCard = card;

            soundManager.PlaySoundclipOneShot(soundManager.CardClip, audioSource);
            CardMover mover = cardView.GetComponent<CardMover>();
            mover.MoveToWorld(deckManager.tableCardPoint);
            cardView.transform.SetParent(deckManager.tableCardPoint);

            Debug.Log($"{player.name} stacked {card.name}!");

            if (card.type == CardType.Draw2)
            {
                if (!player.isBot)
                    OpenTwoColorPicker(card.color, card.secondColor);
                else
                {
                    CardColor chosen = Random.value > 0.5f ? card.color : card.secondColor;
                    currentTableCard.color = chosen;
                    SetColorIndicatorForMode(chosen);
                }
            }
            else if (card.type == CardType.WildDraw4)
            {
                if (!player.isBot)
                    OpenColorPicker();
                else
                {
                    CardColor chosen = GetRandomColor();
                    currentTableCard.color = chosen;
                    SetColorIndicatorForMode(chosen);
                }
            }

            GameManager.instance.CheckWin(player);

            OnPlayerStacked(player, card.type == CardType.Draw2 ? 2 : 4);

            if (!player.isBot)
                playerPlayed = true;

            return;
        }

        if (isMyTurn)
        {
            if (playerPlayed) return;

            if (!IsCardPlayable(cardView.cardData))
            {
                if (!easyMode)
                {
                    Debug.Log("Invalid card! Player draws 1 penalty card.");
                    deckManager.DrawCardForPlayer(player);
                }
                return;
            }

            PlayPlayerCard(player, cardView);
        }
        else if (!easyMode && CanJumpIn(cardView.cardData))
        {
            if (currentBotRoutine  != null) { StopCoroutine(currentBotRoutine);  currentBotRoutine  = null; }
            if (playerTurnRoutine  != null) { StopCoroutine(playerTurnRoutine);  playerTurnRoutine  = null; }

            PlayAnyCard(player, cardView);
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

        ApplyCardEffects(card, isBot: false, player: player);

        ResetCardHints(player);
        playerPlayed = true;

        GameManager.instance.CheckWin(player);
        ScheduleBotJumpInCheck();
    }

    void ApplyCardEffects(CardData card, bool isBot, PlayerBase player)
    {
        switch (card.type)
        {
            case CardType.Skip:
                skipNextTurn = true;
                break;

            case CardType.Reverse:
                turnDirection *= -1;
                if (players.Count == 2)
                    skipNextTurn = true;
                break;

            case CardType.Draw2:
            {
                int addedCards  = 2;
                int playerIndex = players.IndexOf(player);
                int targetIndex = WrapIndex(playerIndex + turnDirection);

                if (isBot)
                {
                    CardColor chosen = Random.value > 0.5f ? card.color : card.secondColor;
                    currentTableCard.color = chosen;
                    card.color = chosen;  
                    SetColorIndicatorForMode(chosen);
                    ShowColorPopup(chosen);
                }
                else
                {
                    OpenTwoColorPicker(card.color, card.secondColor);
                }

                if (!easyMode && inStackWindow)
                    OnPlayerStacked(player, addedCards);
                else
                    OpenStackWindow(targetIndex, addedCards); 
                break;
            }

            case CardType.Wild:
                if (isBot)
                {
                    CardColor newColor = GetRandomColor();
                    currentTableCard.color = newColor;
                    card.color = newColor;
                    SetColorIndicatorForMode(newColor);
                    ShowColorPopup(newColor);
                    Debug.Log("Bot Wild → " + newColor);
                }
                break;

            case CardType.WildDraw4:
            {
                int addedCards  = 4;
                int playerIndex = players.IndexOf(player);
                int targetIndex = WrapIndex(playerIndex + turnDirection);

                if (isBot)
                {
                    CardColor randomColor = GetRandomColor();
                    currentTableCard.color = randomColor;
                    card.color = randomColor;  
                    SetColorIndicatorForMode(randomColor);
                    ShowColorPopup(randomColor);

                    if (!easyMode && inStackWindow)
                        OnPlayerStacked(player, addedCards);
                    else
                        OpenStackWindow(targetIndex, addedCards);
                }
                break;
            }
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
        SetColorIndicatorForMode(color);

        if (currentTableCard.type == CardType.WildDraw4)
        {
            PlayerBase human   = players.Find(p => !p.isBot);
            int humanIndex     = players.IndexOf(human);
            int targetIndex    = WrapIndex(humanIndex + turnDirection);
            if (!easyMode && inStackWindow)
                OnPlayerStacked(human, 4);
            else
                OpenStackWindow(targetIndex, 4);
        }

        StartCoroutine(DelayedReset(7f));
        playerPlayed = true;

        GameManager.instance.CheckWin(players.Find(p => !p.isBot));
        ScheduleBotJumpInCheck();
    }

    void SetColorIndicatorForMode(CardColor color)
    {
        if (easyMode)
            currentColorImage.color = ColorFromCardColor(color);
        else
        {
            ShowColorPopup(color);
            currentColorImage.color = Color.clear;
        }
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

    public void UpdateColorIndicator(CardColor color) => SetColorIndicatorForMode(color);

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
        redButton.SetActive(true); blueButton.SetActive(true);
        greenButton.SetActive(true); yellowButton.SetActive(true);
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
    #region Color Popup
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
    #region Jump-In (number cards only)
    // ═════════════════════════════════════════════════════════════════

    bool CanJumpIn(CardData card)
    {
        if (easyMode) return false;
        if (card.type != CardType.Number) return false;

        CardData top = deckManager.firstCard;
        if (card.color != top.color) return false;
        if (top.type != CardType.Number) return false;
        return card.Number == top.Number;
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

        int jumpIndex      = players.IndexOf(player);
        currentPlayerIndex = WrapIndex(jumpIndex + turnDirection);
        jumpInHappened     = true;

        HandleJumpInCardEffects(card);
        GameManager.instance.CheckWin(player);

        RestartTurnLoop();
    }

    void HandleJumpInCardEffects(CardData card)
    {
        switch (card.type)
        {
            case CardType.Skip:    skipNextTurn   = true;  break;
            case CardType.Reverse: turnDirection *= -1;    break;
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
                    if (currentBotRoutine != null) { StopCoroutine(currentBotRoutine); currentBotRoutine = null; }
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
        if (player.turnTimerImage != null) player.turnTimerImage.fillAmount = t;
    }

    void UpdateClockColor(PlayerBase player, float t)
    {
        if (player.turnTimerImage == null) return;
        Color c = t > 0.5f
            ? Color.Lerp(ClockYellow, ClockGreen, (t - 0.5f) * 2f)
            : Color.Lerp(ClockRed, ClockYellow, t * 2f);
        player.turnTimerImage.color = c;
    }

    void SetClockColor(PlayerBase player, Color c)
    {
        if (player.turnTimerImage != null) player.turnTimerImage.color = c;
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

    bool IsIdenticalPlusCard(CardData candidate, CardData table)
    {
        if (candidate.type != table.type) return false;
        if (candidate.type != CardType.Draw2 && candidate.type != CardType.WildDraw4) return false;
        bool sameColors =
            (candidate.color == table.color && candidate.secondColor == table.secondColor) ||
            (candidate.color == table.secondColor && candidate.secondColor == table.color);
        return sameColors;
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
