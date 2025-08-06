using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

public class AI_DeckManager : MonoBehaviour
{
    [System.Serializable]
    public class AIHand
    {
        public string aiName;
        public SplineContainer splineContainer;
        public List<SplineContainer> spreadContainers; // <-- now supports multiple spreads
        public Text handCountText, DeckTotal, AITotalCardsValue;
        public List<GameObject> handCards = new();

        private int currentSpreadIndex = 0;

        public SplineContainer GetNextSpreadContainer()
        {
            if (currentSpreadIndex >= spreadContainers.Count)
            {
                Debug.LogWarning($"{aiName} has no more spread containers left!");
                return null;
            }

            return spreadContainers[currentSpreadIndex++];
        }

        public void ResetSpreadIndex() => currentSpreadIndex = 0;

        public int GetTotalHandValue()
        {
            int total = 0;
            foreach (GameObject card in handCards)
            {
                if (card.TryGetComponent(out Card cardComponent))
                    total += cardComponent.cardValue;
            }
            return total;
        }

        public void LogHandValues()
        {
            Debug.Log($" {aiName}'s Hand Values:");
            foreach (GameObject card in handCards)
            {
                if (card.TryGetComponent(out Card cardComponent))
                    Debug.Log($"Card: {cardComponent.name} Value: {cardComponent.cardValue}");
            }
            Debug.Log($"Total Hand Value: {GetTotalHandValue()}");
        }
    }


    [Space(10), Header("Gameplay Settings"), Tooltip("Gameplay Settings")]
    [SerializeField] public int maxHandSize = 5;
    [SerializeField] private AudioClip dealClip, spreadClip, knockClip;

    [Space(10), Header("AI Hands"), Tooltip("Gameplay Settings")]
    [SerializeField] private List<AIHand> aiHands = new();

    [Header("Anchoring (Match Order with AIHands)"), Tooltip("Anchoring (Match Order with AIHands)")]
    [SerializeField] private List<RectTransform> aiScreenAnchors;
    [SerializeField] private List<Transform> aiSplineContainers;
    private int aiCardSortingOrder = 0;
    private DeckManager deckManager;
    [SerializeField] private DiscardPile discardPile;
    private HandManager handManager;
    [SerializeField] private Transform discardTargetPos;

    private AudioSource audioSource;
    private Vector2 lastScreenSize;


    [Header("Game Over Settings!"), Tooltip("Game Over Settings!")]
    [SerializeField] private Text WinnerText, displayText;
    public GameObject KnockSymbol;
    private int AI1WinCount = 0, AI2WinCount = 0, PlayerWinCount = 0;
    [SerializeField] public GameObject[] AIWinDisplay, AI2WinDisplay, PlayerWinDisplay;
    private bool alreadyWon = false;
    private float Difficulty => GameManager.Instance.AiDifficultyLevel;



    void Start()
    {
        deckManager = GameObject.Find("DeckManager").GetComponent<DeckManager>();
        discardPile = GameObject.Find("DiscardPilePoint").GetComponent<DiscardPile>();
        handManager = GameObject.Find("HandManager").GetComponent<HandManager>();
        audioSource = GetComponent<AudioSource>();

        UpdateSplinePositionsToScreen(); // Initial position
        lastScreenSize = new Vector2(Screen.width, Screen.height);
        LoadWinData();

        // ResetWinData();
    }

    void Update()
    {
        // Check for screen resize
        if (Screen.width != lastScreenSize.x || Screen.height != lastScreenSize.y)
        {
            lastScreenSize = new Vector2(Screen.width, Screen.height);
            UpdateSplinePositionsToScreen();
        }
    }

    public void DrawCardToAI(int aiIndex)
    {
        if (aiIndex < 0 || aiIndex >= aiHands.Count) return;

        AIHand ai = aiHands[aiIndex];
        if (ai.handCards.Count >= maxHandSize) return;

        GameObject card = deckManager.RemoveCardFromDeck();
        
        if (card == null) return;

        card.transform.DOKill(true);
        // card.transform.localScale = Vector3.one * 0.8f;

        var cardComponent = card.GetComponent<Card>();
        cardComponent.isAICard = true;
        cardComponent.FlipCard(true);

        ai.handCards.Add(card);

        // Make this card render on top
        var sr = card.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = ++aiCardSortingOrder;
        }

        cardComponent.ShowBack();
        UpdateAIHand(ai);
        audioSource.PlayOneShot(dealClip);
        UpdateAllAIHands();

        if (GameManager.Instance.canStartRound == true)
        {
            CheckAITonk(ai);      // Declare Tonk if valid
            GameManager.Instance.HasNotCheckedTonk = false;
        }
    }

    public IEnumerator DrawAndDiscard(int aiIndex)
    {
        if (GameManager.Instance.canStartRound == true)
        {
            Card topCard = discardPile.GetLastCard(discardPile.discardedCards);

            // if topcard is going to easily form a valid spread, either book or run, then call Draw from Discard method

            if (Difficulty >= 0.75f && CanFormSetOrRun(topCard, aiIndex))  // First difficulty check
            {
                AIDrawFromDiscard(aiIndex);
                audioSource.PlayOneShot(dealClip);
                Debug.Log($"{aiHands[aiIndex].aiName} drew {topCard.cardValue} of {topCard.cardSuit} from discard.");
            }
            else
            {
                DrawCardToAI(aiIndex);
                audioSource.PlayOneShot(dealClip);
                Debug.Log($"{aiHands[aiIndex].aiName} skipped drawing {topCard.cardValue} of {topCard.cardSuit} from discard and drew from Deck.");
            }

            // Either ways AI draws a card first before discarding
            yield return new WaitForSeconds(1f);

            EvaluateAndDiscardFromAI(aiIndex);
        }
    }

    public void EvaluateAndDiscardFromAI(int aiIndex)
    {
        if (aiIndex < 0 || aiIndex >= aiHands.Count) return;

        AIHand ai = aiHands[aiIndex];

        // PHASE 1: Lay spreads FIRST
        SendValidSpreadsToSpline(aiIndex);

        StartCoroutine(WaitAndDiscard(aiIndex));
    }

    private IEnumerator WaitAndDiscard(int aiIndex)
    {
        yield return new WaitForSeconds(0.5f); // Let cards settle

        AIHand ai = aiHands[aiIndex];
        var allCards = ai.handCards.Select(c => c.GetComponent<Card>()).Where(c => c != null).ToList();

        HashSet<Card> keepers = new();

        // === PHASE 2: Detect NEAR BOOKS ===
        var valueGroups = allCards.GroupBy(c => c.cardValue);
        foreach (var group in valueGroups)
        {
            if (group.Count() >= 2)
            {
                if (Difficulty >= 0.7f || (group.Count() == 2 && Difficulty >= 0.4f))
                {
                    foreach (var card in group)
                        keepers.Add(card);

                    Debug.Log($"{ai.aiName} identified NEAR BOOK: {group.Key}s");
                }
            }
        }

        // === PHASE 3: Detect NEAR RUNS ===
        var suitGroups = allCards.GroupBy(c => c.cardSuit);
        foreach (var group in suitGroups)
        {
            var sorted = group.OrderBy(c => c.cardValue).ToList();

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                int valA = sorted[i].cardValue;
                int valB = sorted[i + 1].cardValue;

                // Check if cards are within 2 values of each other (e.g., 4 & 6)
                if (valB - valA <= 2)
                {
                    if (Difficulty >= 0.7f || (valB - valA == 1 && Difficulty >= 0.4f))
                    {
                        keepers.Add(sorted[i]);
                        keepers.Add(sorted[i + 1]);

                        Debug.Log($"{ai.aiName} identified NEAR RUN: {valA} & {valB} of {group.Key}");
                    }
                }
            }
        }

        // === PHASE 4: Determine discard options ===
        List<Card> discardables;

        if (Difficulty < 0.4f)
        {
            // Dumb AI just picks randomly from hand
            discardables = allCards.OrderBy(c => Random.value).ToList();
            Debug.Log($"{ai.aiName} is low IQ, discarding randomly 😵");
        }
        else
        {
            discardables = allCards.Except(keepers).ToList();

            if (discardables.Count == 0)
            {
                Debug.Log($"{ai.aiName} has no obvious discard. Picking highest value card.");
                discardables = allCards.OrderByDescending(c => c.cardValue).ToList();
            }
        }

        if (!GameManager.Instance.canStartRound)
        {
            Debug.Log("Game already won — no discard.");
            yield break;
        }

        // === PHASE 5: Choose discard ===
        var suitCounts = allCards.GroupBy(c => c.cardSuit).ToDictionary(g => g.Key, g => g.Count());

        Card discardCard = discardables
            .OrderBy(c => suitCounts[c.cardSuit])
            .ThenByDescending(c => c.cardValue)
            .First();

        StartCoroutine(DiscardCardFromAI(aiIndex, discardCard));
    }

    private IEnumerator DiscardCardFromAI(int aiIndex, Card discardCard)
    {
        yield return new WaitForSeconds(0.3f);

        GameObject cardObj = discardCard.gameObject;
        cardObj.transform.DOKill(true);
        discardCard.FlipCard(true, 0.5f);

        yield return new WaitForSeconds(0.6f);

        cardObj.transform.SetParent(null);
        cardObj.transform.DOMove(discardTargetPos.position, 0.5f).SetEase(Ease.OutCubic);

        yield return new WaitForSeconds(0.6f);
        discardPile.SnapCardToPile(discardCard);

        aiHands[aiIndex].handCards.Remove(cardObj);
        UpdateAIHand(aiHands[aiIndex]);

        Debug.Log($"{aiHands[aiIndex].aiName} discarded {discardCard.cardValue} of {discardCard.cardSuit}");
    }

    public int GetAIHandCount(int aiIndex)
    {
        if (aiIndex < 0 || aiIndex >= aiHands.Count) return 0;
        return aiHands[aiIndex].handCards.Count;
    }

    public void UpdateAIHand(AIHand ai)   // Update each AI hands
    {
        ai.AITotalCardsValue.text = ai.GetTotalHandValue().ToString();
        ai.DeckTotal.text = ai.GetTotalHandValue().ToString();

        if (ai.handCards.Count == 0 && GameManager.Instance.canStartRound && !alreadyWon)
        {
            DetermineWinner();  // <-- Round over
            Debug.Log("AI won by finishing cards in hand!");
        }

        else if (ai.handCards.Count > 0)
        {
            float cardSpacing = 1f / maxHandSize;
            float firstCardPosition = 0.5f - (ai.handCards.Count - 1) * cardSpacing / 2;
            Spline spline = ai.splineContainer.Spline;

            for (int i = 0; i < ai.handCards.Count; i++)
            {
                GameObject card = ai.handCards[i];
                Card cardComponent = card.GetComponent<Card>();

                float p = firstCardPosition + i * cardSpacing;
                Vector3 localSplinePos = spline.EvaluatePosition(p);
                Vector3 splinePosition = ai.splineContainer.transform.TransformPoint(localSplinePos);

                Vector3 forward = spline.EvaluateTangent(p);
                Vector3 up = spline.EvaluateUpVector(p);
                Quaternion rotation = Quaternion.LookRotation(up, Vector3.Cross(up, forward).normalized);

                card.transform.DOKill(true);

                card.transform.DOMove(splinePosition, 0.3f);
                card.transform.DORotateQuaternion(rotation, 0.3f);

                // Set sorting order based on index (card i = sortingOrder i)
                if (cardComponent != null)
                {
                    int order = i + 10; // +10 to avoid conflict with background elements
                    if (cardComponent.frontRenderer != null) cardComponent.frontRenderer.sortingOrder = order;
                    if (cardComponent.backRenderer != null) cardComponent.backRenderer.sortingOrder = order;
                }
            }
        }
        // deckManager.startReshuffling();

        TryKnock(ai);
        ai.handCountText.text = ai.handCards.Count.ToString();
    }

    public void UpdateAllAIHands()
    {
        foreach (var ai in aiHands)
            UpdateAIHand(ai);
    }

    // This method allows the AI deck position to be postitioned according to screen resolution this happens in start alone, doesn't happen mid-game
    private void UpdateSplinePositionsToScreen()
    {
        Canvas.ForceUpdateCanvases(); // Force UI to update positions/layouts

        for (int i = 0; i < Mathf.Min(aiScreenAnchors.Count, aiSplineContainers.Count); i++)
        {
            Vector3 worldPos = GetWorldPositionFromRect(aiScreenAnchors[i]);
            aiSplineContainers[i].position = worldPos;
        }
    }

    // Returns a Vector3 for repositioning
    private Vector3 GetWorldPositionFromRect(RectTransform rect)
    {
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, rect.position);
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);
        worldPos.z = 0f; // Since it's 2D
        return worldPos;
    }

    public void SendValidSpreadsToSpline(int aiIndex)
    {
        if (aiIndex < 0 || aiIndex >= aiHands.Count) return;

        AIHand ai = aiHands[aiIndex];
        var cards = ai.handCards.Select(c => c.GetComponent<Card>()).Where(c => c != null).ToList();

        List<List<Card>> validSpreads = new();

        HashSet<Card> usedCards = new(); // prevent overlap (same card in multiple spreads)

        // === 1. Books (3+ of same value) ===
        var valueGroups = cards
            .Where(c => !usedCards.Contains(c))
            .GroupBy(c => c.cardValue);

        foreach (var group in valueGroups)
        {
            var available = group.Where(c => !usedCards.Contains(c)).ToList();
            if (available.Count >= 3)
            {
                var spread = available.Take(3).ToList();
                validSpreads.Add(spread);
                foreach (var c in spread) usedCards.Add(c);
            }
        }

        // === 2. Runs (same suit, 3+ consecutive values) ===
        var suitGroups = cards
            .Where(c => !usedCards.Contains(c))
            .GroupBy(c => c.cardSuit);

        foreach (var suitGroup in suitGroups)
        {
            var sorted = suitGroup.OrderBy(c => c.cardValue).ToList();
            for (int i = 0; i < sorted.Count - 2; i++)
            {
                var c1 = sorted[i];
                var c2 = sorted[i + 1];
                var c3 = sorted[i + 2];

                if (usedCards.Contains(c1) || usedCards.Contains(c2) || usedCards.Contains(c3)) continue;

                if (c2.cardValue == c1.cardValue + 1 && c3.cardValue == c2.cardValue + 1)
                {
                    var run = new List<Card> { c1, c2, c3 };
                    validSpreads.Add(run);
                    usedCards.Add(c1);
                    usedCards.Add(c2);
                    usedCards.Add(c3);
                }
            }
        }

        // === Lay each spread ===
        foreach (var spread in validSpreads)
        {
            SplineContainer targetSpread = ai.GetNextSpreadContainer();
            if (targetSpread == null)
            {
                Debug.LogWarning($"{ai.aiName} has no more spread containers left.");
                continue;
            }

            Spline spline = targetSpread.Spline;
            Transform spreadTransform = targetSpread.transform;

            float spacing = 1f / maxHandSize;
            float start = 0.5f - (spread.Count - 1) * spacing / 2;

            for (int i = 0; i < spread.Count; i++)
            {
                var card = spread[i];
                GameObject cardObj = card.gameObject;

                // Remove from AI hand
                ai.handCards.Remove(cardObj);

                float p = Mathf.Clamp01(start + i * spacing);
                Vector3 pos = spline.EvaluatePosition(p);
                Vector3 forward = spline.EvaluateTangent(p);
                Vector3 up = spline.EvaluateUpVector(p);
                Quaternion rotation = Quaternion.LookRotation(up, Vector3.Cross(up, forward));

                cardObj.transform.DOKill(true);
                cardObj.transform.DOMove(spreadTransform.TransformPoint(pos), 0.4f);
                cardObj.transform.DORotateQuaternion(rotation, 0.4f);
                cardObj.transform.DOScale(Vector3.one * 0.6f, 0.3f);

                int order = 200 + i;
                card.frontRenderer.sortingOrder = order;
                card.backRenderer.sortingOrder = order;
                card.outlineRenderer.sortingOrder = order + 1;
            }

            Debug.Log($"{ai.aiName} laid a spread: {string.Join(", ", spread.Select(c => $"{c.cardValue} of {c.cardSuit}"))}");
            audioSource.PlayOneShot(spreadClip);
        }

        UpdateAIHand(ai);
    }

    private bool CanFormSetOrRun(Card card, int aiIndex)
    {
        var ai = aiHands[aiIndex];
        int sameValueCount = 0;
        int suitStreak = 0;

        foreach (var cObj in ai.handCards)
        {
            var c = cObj.GetComponent<Card>();
            if (c == null) continue;

            if (c.cardValue == card.cardValue) sameValueCount++;
            if (c.cardSuit == card.cardSuit && Mathf.Abs(c.cardValue - card.cardValue) <= 1)
                suitStreak++;
        }

        return sameValueCount >= 2 || suitStreak >= 2;
    }

    public void AIDrawFromDiscard(int aiIndex)
    {
        if (aiIndex < 0 || aiIndex >= aiHands.Count && discardPile.discardedCards.Count <= 0) return;

        AIHand ai = aiHands[aiIndex];
        Card topCard = discardPile.GetLastCard(discardPile.discardedCards);

        if (topCard != null)
        {
            GameObject cardObj = topCard.gameObject;

            ai.handCards.Add(cardObj); // Add to AI's hand list

            topCard.transform.SetParent(transform); // Reparent under AI manager
            topCard.isAICard = true;
            topCard.isDiscarded = false;

            // Optional: Flip or animate if needed
            topCard.FlipCard(true);
            topCard.ShowBack();

            // Remove it from discard pile
            discardPile.discardedCards.Remove(topCard);

            UpdateAIHand(ai);
        }
        else
        {
            Debug.LogWarning("AI tried to draw from an empty discard pile.");
        }
    }  

    // Check individual AI Tonk
    public void CheckAITonk(AIHand ai)
    {
        int total = ai.GetTotalHandValue();

        if ( Difficulty >= 0.5f && total >= 49 && !GameManager.Instance.canStartRound == true && GameManager.Instance.HasNotCheckedTonk == true)
        {
            Debug.Log($"{ai.aiName} can declare TONK! Total value: {total}");
            GameManager.Instance.TonkWin();

            WinnerText.text = ai.aiName + "has declared Tonk";
            GameManager.Instance.PlayerLost();
        }
    }
    
    void TryKnock(AIHand ai)
    {
        if (!GameManager.Instance.canStartRound) return;

        int handValue = ai.GetTotalHandValue();
        int RandomValueForKnock = Random.Range(2, 7);

        if (handValue <= RandomValueForKnock)
        {
            StartCoroutine(CallKnock());
            Debug.LogWarning("AI just knocked!");
        }
    }

    // Check all AI Tonk
    public void CheckAllAITonks()
    {
        foreach (var ai in aiHands)
        {
            CheckAITonk(ai);
        }
    }

    public void DetermineWinner()
    {
        if (alreadyWon == true) return;

        int playerHandValue = handManager.GetTotalHandValue();
        string winnerName = "Player";
        int lowestValue = playerHandValue;

        foreach (AIHand ai in aiHands)
        {
            int aiValue = ai.GetTotalHandValue();
            if (aiValue < lowestValue)
            {
                lowestValue = aiValue;
                winnerName = ai.aiName;
            }
        }

        Debug.Log($"{winnerName} wins!");
        WinnerText.text = $"{winnerName} wins!";
        WinnerText.transform.localScale = Vector3.zero;
        WinnerText.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBounce);

        // Time slow
        Time.timeScale = 0.5f;
        DOVirtual.DelayedCall(2f, () => Time.timeScale = 1f);

        // Handle Win Count
        if (winnerName == "Player")
        {
            PlayerWinCount++;
            PlayerPrefs.SetInt("PlayerWins", PlayerWinCount);
            if (PlayerWinCount <= PlayerWinDisplay.Length)
                PlayerWinDisplay[PlayerWinCount - 1].SetActive(true);

            GameManager.Instance.PlayerWon();
        }
        else if (winnerName == "Miles_Morales")
        {
            AI1WinCount++;
            PlayerPrefs.SetInt("AI1Wins", AI1WinCount);
            if (AI1WinCount <= AIWinDisplay.Length)
                AIWinDisplay[AI1WinCount - 1].SetActive(true);

            GameManager.Instance.PlayerLost();
        }
        else if (winnerName == "Vegas_Dealer_001")
        {
            AI2WinCount++;
            PlayerPrefs.SetInt("AI2Wins", AI2WinCount);
            if (AI2WinCount <= AI2WinDisplay.Length)
                AI2WinDisplay[AI2WinCount - 1].SetActive(true);

            GameManager.Instance.PlayerLost();
        }

        PlayerPrefs.Save();
        alreadyWon = false;
    }

    private void LoadWinData()
    {
        AI1WinCount = PlayerPrefs.GetInt("AI1Wins", 0);
        AI2WinCount = PlayerPrefs.GetInt("AI2Wins", 0);
        PlayerWinCount = PlayerPrefs.GetInt("PlayerWins", 0);

        for (int i = 0; i < AI1WinCount && i < AIWinDisplay.Length; i++)
            AIWinDisplay[i].SetActive(true);

        for (int i = 0; i < AI2WinCount && i < AI2WinDisplay.Length; i++)
            AI2WinDisplay[i].SetActive(true);

        for (int i = 0; i < PlayerWinCount && i < PlayerWinDisplay.Length; i++)
            PlayerWinDisplay[i].SetActive(true);
    }

    public void ResetWinData()
    {
        AI1WinCount = 0;
        AI2WinCount = 0;
        PlayerWinCount = 0;

        PlayerPrefs.DeleteKey("AI1Wins");
        PlayerPrefs.DeleteKey("AI2Wins");
        PlayerPrefs.DeleteKey("PlayerWins");

        foreach (var go in AIWinDisplay)
            go.SetActive(false);
        foreach (var go in AI2WinDisplay)
            go.SetActive(false);
        foreach (var go in PlayerWinDisplay)
            go.SetActive(false);

        PlayerPrefs.Save();
    }

    public void PlayerKnock()
    {
        StartCoroutine(CallKnock());
    }

    public IEnumerator CallKnock()
    {
        displayText.text = "KNOCK";
        KnockSymbol.transform.localScale = Vector3.zero;
        yield return KnockSymbol.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBounce);
        yield return KnockSymbol.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBounce);
        audioSource.PlayOneShot(knockClip);
        Time.timeScale = 0.5f;
        DOVirtual.DelayedCall(2f, () => Time.timeScale = 1f);

        yield return new WaitForSeconds(3f);

        DetermineWinner();
    }

}
