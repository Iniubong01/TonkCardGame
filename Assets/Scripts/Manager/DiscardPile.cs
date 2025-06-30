using UnityEngine;
using DG.Tweening;

public class DiscardPile : MonoBehaviour
{
    private int topSortingOrder = 20;

    [Header("Highlight Settings")]
    public SpriteRenderer highlightGlow;  
    public bool canSnap = false;  // Handles the snapping of cards
    private HandManager handManager;

    void Awake()
    {
       // Card.discardPile = this;
        if (highlightGlow != null)
            highlightGlow.enabled = false;
    }

    private void Start() {
        handManager = GameObject.Find("HandManager").GetComponent<HandManager>();
    }

    public void SetHighlight(bool state)
    {
        if (highlightGlow != null)
            highlightGlow.enabled = state;
    }

    public void SnapCardToPile(Card card)
    {
        Transform CT = card.transform;
        CT.DOKill(); // cancel previous tweens

        // float zOffset = -topSortingOrder * 0.01f;
        Vector3 targetPos = new Vector3(transform.position.x, transform.position.y, 0);

        CT.DOMove(targetPos, 0.3f).SetEase(Ease.OutCubic).Play();
        CT.DORotate(transform.eulerAngles, 0.3f).SetEase(Ease.OutCubic).Play();

        // Sorting order
        var SR = card.GetComponent<SpriteRenderer>();  
        if (SR != null) SR.sortingOrder = topSortingOrder++;   // Makes cards to stay ontop of the previous

        DOVirtual.DelayedCall(0.3f, card.ShowFront);
        // SetHighlight(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Card card = other.GetComponent<Card>();

        if (!canSnap) return;  

        if (card != null)
        {
            // SetHighlight(true);
            SnapCardToPile(card);
            card.MarkDiscarded(); // Mark the card as discarded
            handManager.RemoveCard(card);
        }
    }


}
