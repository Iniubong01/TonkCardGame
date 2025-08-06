using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine.UI;
using System.Collections;

public class HandManager : MonoBehaviour
{
    [SerializeField] private int maxHandSize, spreadSize; // max no. of cards allowed in player deck
    [SerializeField] private SplineContainer splineContainer;  // drawn spline shape
    public SplineContainer[] spreadSplines; // Assign these in Inspector
    private int currentSpreadIndex = 0;     // Tracks which spread we're laying now
    [SerializeField] private Text playerCardText, DeckTotal, playerTotalCardsValue;  // shows the reminaining no. of cards in player hand
    private List<GameObject> handCards = new();
    private DeckManager deckManager;
    private DiscardPile discardPile; // Reference to the discard pile
    private AudioSource audioSource;
    public AudioClip dealClip, spreadClip;
    public GameObject LastDrawnCard { get; private set; }  // getter to ensure that plae=yer card x axis scale is not messed with after initial card dealing

    [Header("UI & Prefabs")]
    [SerializeField] private GameObject inputIndicatorPrefab, drawFirstMessage;
    [SerializeField] private Canvas canvas;

    private GameObject indicator;
    private RectTransform indicatorRect;
    public List<Card> currentlySelectedCards = new();
    public Button spreadButton, DiscardDrawButton; // Assign in inspector
    public Button DrawButton;
    bool HasNotCheckedTonk = true;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        deckManager = GameObject.Find("DeckManager").GetComponent<DeckManager>();

        spreadButton.interactable = false;

        indicator = Instantiate(inputIndicatorPrefab, canvas.transform);
        indicatorRect = indicator.GetComponent<RectTransform>();
        indicator.SetActive(false);
        DisableDiscarding();

        if (discardPile == null)
            discardPile = FindFirstObjectByType<DiscardPile>();
    }

    public void DrawCardFromDeck()
    {
        if (handCards.Count >= maxHandSize) return;

        GameObject card = deckManager.RemoveCardFromDeck();
        if (card == null) return;

        LastDrawnCard = card;

        // Kill lingering tweens
        card.transform.DOKill(true);

        var cardComp = card.GetComponent<Card>();
        cardComp.isPlayerCard = true;
        cardComp.canDiscard = true;

        Vector3 targetPos = transform.position;
        card.transform.DOMove(targetPos, 0.4f)
            .SetEase(Ease.OutCubic)
            .OnComplete(() =>
            {
                // Reparent after move so it doesn’t jump mid-animation
                card.transform.SetParent(transform);
                handCards.Add(card);
                UpdateCardPositions();
                UpdateHandCountUI();

                if (GameManager.Instance.canStartRound == true)
                {
                    EnableDiscarding();
                    discardPile.canSnap = true;
                    cardComp.ShowFront();
                    DiscardDrawButton.interactable = false;
                    DrawButton.interactable = false;
                }
            });
    }

    public void DrawFromDiscard()
    {
        if (!GameManager.Instance.canStartRound == true) return;

        Card topCard = discardPile.GetLastCard(discardPile.discardedCards);

        if (topCard != null)
        {
            handCards.Add(topCard.gameObject); // Add to player's hand list

            topCard.transform.SetParent(transform); // Reparent under player hand UI
            topCard.isPlayerCard = true;
            topCard.isDiscarded = false;

            // Remove it from discard pile
            discardPile.discardedCards.Remove(topCard);

            UpdateCardPositions();
            UpdateHandCountUI();
            DiscardDrawButton.interactable = false;
            DrawButton.interactable = false;
            EnableDiscarding();
        }

        else
        {
            Debug.Log("Discard pile is empty.");
        }
    }


    public void DisableDiscarding()
    {
        foreach (var obj in handCards)
        {
            if (obj.TryGetComponent(out Card c))
                c.canDiscard = false;
        }
    }

    public void EnableDiscarding()
    {
        foreach (var obj in handCards)
        {
            if (obj.TryGetComponent(out Card c))
                c.canDiscard = true;
        }

        discardPile.canSnap = true;
        Debug.Log("Enabled Discarding again");
    }

    public IEnumerator DrawBeforeDiscarding()
    {
        drawFirstMessage.SetActive(true);
        yield return new WaitForSeconds(2f);
        drawFirstMessage.SetActive(false);
    }

    // Method to enable card discarding
    public void RemoveCard(Card card)
    {
        if (card == null) return;

        if (handCards.Contains(card.gameObject))
        {
            handCards.Remove(card.gameObject);
            UpdateCardPositions();
            UpdateHandCountUI();
        }

        GameManager.Instance.PlayerFinishedTurn();

        card.GetComponent<Card>().canDiscard = false;  // Stop player from making further discards untill he draws
        DisableDiscarding();
    }

    public void UpdateCardPositions()
    {
        if (handCards.Count > 0)
        {
            float cardSpacing = 1f / maxHandSize;
            float firstCardPosition = 0.5f - (handCards.Count - 1) * cardSpacing / 2;
            Spline spline = splineContainer.Spline;

            for (int i = 0; i < handCards.Count; i++)
            {
                GameObject card = handCards[i];

                float p = firstCardPosition + i * cardSpacing;
                Vector3 splinePosition = spline.EvaluatePosition(p);
                Vector3 forward = spline.EvaluateTangent(p);
                Vector3 up = spline.EvaluateUpVector(p);
                Quaternion rotation = Quaternion.LookRotation(up, Vector3.Cross(up, forward).normalized);

                // Kill any tweens first
                card.transform.DOKill(true);

                // Animate position and rotation
                card.transform.DOMove(splinePosition, 0.3f);
                card.transform.DOLocalRotateQuaternion(rotation, 0.3f);

                // Sorting layer fixed here
                Card cardComponent = card.GetComponent<Card>();
                if (cardComponent != null)
                {
                    int sortOrder = 10 + i; // Base + index

                    if (cardComponent.frontRenderer != null)
                        cardComponent.frontRenderer.sortingOrder = sortOrder;

                    if (cardComponent.backRenderer != null)
                        cardComponent.backRenderer.sortingOrder = sortOrder;

                    if (cardComponent.outlineRenderer != null)
                        cardComponent.outlineRenderer.sortingOrder = sortOrder + 1; // Outline slightly higher
                }
            }
        }

        else
        {
            if (GameManager.Instance.canStartRound)
                Debug.Log("Player has won!");
                GameManager.Instance.PlayerWon();
        }
    }

    public void UpdateHandCountUI()
    {
        playerCardText.text = handCards.Count.ToString();
        playerTotalCardsValue.text = GetTotalHandValue().ToString();  // Get total value for win UI display
        DeckTotal.text = GetTotalHandValue().ToString();  // Get total value for win UI display
    }

    void Update()
    {
        // Check touch
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            HandleInput(touch.position, touch.phase == TouchPhase.Began, touch.phase == TouchPhase.Ended);
        }
        else if (Input.GetMouseButtonDown(0))
        {
            HandleInput(Input.mousePosition, true, false);
        }
        else if (Input.GetMouseButton(0))
        {
            HandleInput(Input.mousePosition, false, false);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            HandleInput(Input.mousePosition, false, true);
        }
    }

    private void HandleInput(Vector2 screenPos, bool started, bool ended)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPos,
            canvas.worldCamera,
            out Vector2 localPos
        );

        if (started)
        {
            indicator.SetActive(true);
            indicatorRect.anchoredPosition = localPos;
            indicatorRect.localScale = Vector3.zero;

            // Animate in
            Vector3 indicatorSize = Vector3.one * 0.45f;
            indicatorRect.DOScale(indicatorSize, 0.25f).SetEase(Ease.OutBack);
            indicator.GetComponent<Image>().DOFade(0.6f, 0.2f);
        }
        else if (!ended)
        {
            indicatorRect.anchoredPosition = localPos;
        }
        else if (ended)
        {
            // Animate out
            indicatorRect.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack);
            indicator.GetComponent<Image>().DOFade(0f, 0.2f)
                .OnComplete(() => indicator.SetActive(false));
        }
    }

    public void UpdatePosAndUI()
    {
        UpdateCardPositions();
        UpdateHandCountUI();
    }

    public int GetTotalHandValue()
    {
        int total = 0;
        foreach (GameObject card in handCards)
        {
            Card cardComponent = card.GetComponent<Card>();
            if (cardComponent != null)
            {
                total += cardComponent.cardValue;
            }
        }
        return total;
    }

    public void LogHandValues()
    {
        Debug.Log("Player Hand Values:");
        foreach (GameObject card in handCards)
        {
            Card cardComponent = card.GetComponent<Card>();  // A nice way to get the card component from the game object
            if (cardComponent != null)
            {
                Debug.Log($"Card: {cardComponent.gameObject.name} | Value: {cardComponent.cardValue}");
            }
        }

        Debug.Log($"Total Hand Value: {GetTotalHandValue()}");
    }

    private void DeclareTonk()
    // Check in player hand so that if their card value is up to 49 or 50
    {
        int totalValue = GetTotalHandValue();

        // For example, Tonk can be declared if total >= 49
        if (totalValue >= 49 && HasNotCheckedTonk)
        {
            Debug.Log("Player can declare TONK! Total hand value: " + totalValue);
            GameManager.Instance.TonkWin();
            HasNotCheckedTonk = false;
        }
    }

    // Flip all player cards, method to handle what I was telling you about the card flipping after dealing
    public void FlipPlayerCards()
    {
        foreach (var obj in handCards)
        {
            Card card = obj.GetComponent<Card>();

            card.FlipCard(true, 0.4f);
        }
    }

    /// <param name="PLAYER HANDLING OF SPREADS"></param> <summary>
    public void OnCardClicked(Card clickedCard)
    {
        if (!clickedCard.isPlayerCard) return;

        // If this card is already selected, do nothing
        if (currentlySelectedCards.Contains(clickedCard)) return;

        // If no card is selected yet, start new selection
        if (currentlySelectedCards.Count == 0)
        {
            AddCardToSelection(clickedCard);
            return;
        }

        // Try to add card to existing valid selection
        if (CanAddToSpread(clickedCard))
        {
            AddCardToSelection(clickedCard);
        }
        else
        {
            // Disruption: Reset selection and start with new card
            ClearSelection();
            AddCardToSelection(clickedCard);
        }
    }

    private void AddCardToSelection(Card card)
    {
        card.Highlight(true);
        card.isSpread = true;
        currentlySelectedCards.Add(card);

        // Enable spread button if 3 or more cards selected
        spreadButton.interactable = currentlySelectedCards.Count >= 3;

        Debug.Log($"Selected: {card.name} | Value: {card.cardValue} | Suit: {card.cardSuit}");
    }

    private void ClearSelection()
    {
        foreach (var card in currentlySelectedCards)
        {
            card.Highlight(false);
            card.isSpread = false;
        }

        currentlySelectedCards.Clear();
        spreadButton.interactable = false;
    }

    private bool CanAddToSpread(Card newCard)
    {
        if (currentlySelectedCards.Count == 0) return false;

        // Check for Book (same value, any suit)
        bool allSameValue = currentlySelectedCards.All(c => c.cardValue == currentlySelectedCards[0].cardValue);
        if (allSameValue && newCard.cardValue == currentlySelectedCards[0].cardValue)
            return true;

        // Check for Run (same suit, consecutive values)
        bool allSameSuit = currentlySelectedCards.All(c => c.cardSuit == currentlySelectedCards[0].cardSuit);
        if (allSameSuit && newCard.cardSuit == currentlySelectedCards[0].cardSuit)
        {
            var values = currentlySelectedCards.Select(c => c.cardValue).ToList();
            values.Add(newCard.cardValue);
            values.Sort();

            // Check if values are consecutive
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] != values[i - 1] + 1)
                    return false;
            }
            return true;
        }

        return false;
    }

    public void UpdateSpreadPositions()
    {
        if (currentSpreadIndex >= spreadSplines.Length)
        {
            Debug.LogWarning("No more spline containers available for spreads!");
            return;
        }

        // Get current spline container to use
        SplineContainer currentSplineContainer = spreadSplines[currentSpreadIndex];
        Transform splineParent = currentSplineContainer.transform;

        int laidCardCount = splineParent.childCount;
        float cardSpacing = 1f / spreadSize;
        float firstCardPosition = 0.5f - (currentlySelectedCards.Count - 1) * cardSpacing / 2;
        firstCardPosition += laidCardCount * cardSpacing;

        Spline spline = currentSplineContainer.Spline;

        for (int i = 0; i < currentlySelectedCards.Count; i++)
        {
            GameObject card = currentlySelectedCards[i].gameObject;

            float p = Mathf.Clamp01(firstCardPosition + i * cardSpacing);
            Vector3 splinePosition = spline.EvaluatePosition(p);
            Vector3 forward = spline.EvaluateTangent(p);
            Vector3 up = spline.EvaluateUpVector(p);
            Quaternion rotation = Quaternion.LookRotation(up, Vector3.Cross(up, forward).normalized);

            // Move to the spread spline container's transform without reparenting
            card.transform.DOKill(true);
            card.transform.DOMove(splinePosition, 0.3f);
            card.transform.DOLocalRotateQuaternion(rotation, 0.3f);

            // Sorting order so newer cards stack visually on top
            var cardComp = card.GetComponent<Card>();
            if (cardComp != null)
            {
                int sortOrder = 100 + laidCardCount + i;
                cardComp.frontRenderer.sortingOrder = sortOrder;
                cardComp.backRenderer.sortingOrder = sortOrder;
                cardComp.outlineRenderer.sortingOrder = sortOrder + 1;
            }
        }

        currentSpreadIndex++; // Move to next spline for future spreads
    }


    public void LaySpread()
    {
        if (currentlySelectedCards.Count < 3)
        {
            Debug.LogWarning("Not enough cards to lay a spread.");
            return;
        }

        foreach (var card in currentlySelectedCards)
        {
            // Detach from hand list
            if (handCards.Contains(card.gameObject))
                handCards.Remove(card.gameObject);

            // Reparent to spread container
            // card.transform.SetParent(spreadContainer.transform);

            // Optional: visually scale to distinguish from hand
            card.transform.DOScale(Vector3.one * 0.6f, 1f);

            card.isPlayerCard = false;  // No longer a player card, else cannot be dragged againk, woulda used a different bool, but no need to complicaate it further
            card.Highlight(false);
        }

        // Call this after setting up the spread
        UpdateSpreadPositions();
        UpdatePosAndUI();
        audioSource.PlayOneShot(spreadClip);

        // Clear selection for next move
        // currentlySelectedCards.Clear();

        Debug.Log("Spread laid successfully!");
    }


}


