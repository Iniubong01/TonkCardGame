using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;

public class DiscardPile : MonoBehaviour
{
    private int topSortingOrder = 20;

    [Header("Highlight Settings")]
    public SpriteRenderer highlightGlow;
    public bool canSnap = false;  // Enables snapping functionality

    private HandManager handManager;
    [HideInInspector] public List<Card> discardedCards = new List<Card>();

    void Awake()
    {
        if (highlightGlow != null)
            highlightGlow.enabled = false;
    }

    void Start()
    {
        handManager = GameObject.Find("HandManager").GetComponent<HandManager>();
    }


    /// Call this to visually show the glow behind the discard pile
    /// </summary>
    public void SetHighlight(bool state)
    {
        if (highlightGlow != null)
            highlightGlow.enabled = state;
    }


    /// Moves the card to the discard pile with smooth animation and updates sorting
    /// </summary>
    public void SnapCardToPile(Card card)
    {
        if (card == null) return;

        Transform cardTransform = card.transform;
        cardTransform.DOKill(); // Cancel any current tweens

        // Move and rotate to discard pile position
        Vector3 targetPos = new Vector3(transform.position.x, transform.position.y, 0);
        cardTransform.DOMove(targetPos, 0.3f).SetEase(Ease.OutCubic);
        cardTransform.DORotate(transform.eulerAngles, 0.3f).SetEase(Ease.OutCubic);

        // Update sorting order on child sprite renderers
        SetCardSorting(card, topSortingOrder++);

        // Reveal front side after move
        DOVirtual.DelayedCall(0.3f, card.ShowFront);

        // Mark as discarded and remove from hand
        card.MarkDiscarded();

        if (card.isPlayerCard)
        {
            handManager.RemoveCard(card);
            card.Highlight(false);
        }

        discardedCards.Add(card);
    }

    public void CheckNumberOfDiscardedCards()
    {
        if (discardedCards.Count > 0)
        {
            Card lastCard = discardedCards[discardedCards.Count - 1];
            Debug.Log("Last discarded card: " + lastCard.name);
        }
        else
        {
            Debug.Log("No cards have been discarded yet.");
        }

        string message = discardedCards.Count > 0 
            ? "Discarded cards available." 
            : "No discarded cards available.";

        Debug.Log(message);
    }

    public Card GetLastCard(List<Card> discardedCards)
    {
        return discardedCards.Count > 0 ? discardedCards.Last() : null;
    }

    /// Applies sorting order to both front and back renderers
    private void SetCardSorting(Card card, int sortingOrder)
    {
        if (card.frontRenderer != null)
            card.frontRenderer.sortingOrder = sortingOrder;

        if (card.backRenderer != null)
            card.backRenderer.sortingOrder = sortingOrder;
    }


    /// Can be called externally to reset sorting on a card
    public void ResetCardSorting(Card card)
    {
        const int defaultOrder = 0;

        if (card.frontRenderer != null)
            card.frontRenderer.sortingOrder = defaultOrder;

        if (card.backRenderer != null)
            card.backRenderer.sortingOrder = defaultOrder;
    }


    /// Automatically handles when a card enters the discard pile zone
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!canSnap) return;

        Card card = other.GetComponent<Card>();
        if (card == null) return;

        if (card.isAICard || (card.isPlayerCard && card.canDiscard))
        {
            SnapCardToPile(card);
            if (card.isPlayerCard) handManager.DisableDiscarding();
        }
        else if (card.isPlayerCard)
        {
            Debug.Log("Player tried to discard without drawing.");
            StartCoroutine(handManager.DrawBeforeDiscarding());
        }
    }

}
