using UnityEngine;
using DG.Tweening;

public class Card : MonoBehaviour
{
    private Vector3 offset;
    private bool isDragging = false;
    private Vector3 originalPos;
    public SpriteRenderer frontRenderer, backRenderer, outlineRenderer;
    private HandManager handManager;
    public bool isPlayerCard, isAICard, isSpread;
    [HideInInspector] public bool isDiscarded = false;

    [Header("Card ID"), Tooltip("Card ID")]
    public int cardValue;
    public enum Suit { Clubs, Spades, Hearts, Diamonds }
    public Suit cardSuit;
    public bool canDiscard = true;



    void Awake()
    {
        handManager = GameObject.Find("HandManager").GetComponent<HandManager>();
        ShowBack();
        Highlight(false); // Highlight is off by default
        transform.localScale = Vector3.zero * 0.6f;
    }

    void OnMouseDown()
    {
        // Prevent dragging if card is already discarded or in AI hand
        if (isDiscarded && !isPlayerCard && GameManager.Instance.canStartRound == false) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        offset = transform.position - mouseWorld;

        originalPos = transform.position;
        isDragging = true;

        GetComponent<SpriteRenderer>().sortingOrder = 50;    // Once you've touched on a card that is not AIs, it will come to the front, using 10 for now
        SetFace(frontRenderer.sprite);    // Show the front sprite and disable the back according to the SetFace method

        handManager.OnCardClicked(this);
    }

    void OnMouseDrag()
    {
        if (isDragging && !isDiscarded && isPlayerCard)
        {
            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            transform.position = mouseWorld + offset;
        }
    }

    void OnMouseUp()
    {
        if (isDiscarded)
            return;  // If released on discard pile — ignore

        isDragging = false;
        GetComponent<SpriteRenderer>().sortingOrder = 0;

        if (isPlayerCard)
            handManager.UpdatePosAndUI();

    }

    public void SetFace(Sprite faceSprite) => frontRenderer.sprite = faceSprite;

    // methods to handle card back and front rendering, flipping...
    public void ShowFront()
    {
        frontRenderer.enabled = true;
        backRenderer.enabled = false;
    }

    public void ShowBack()
    {
        frontRenderer.enabled = false;
        backRenderer.enabled = true;
    }

    public void Highlight(bool state)
    {
        if (outlineRenderer != null)  // Check first, safer codin'
            outlineRenderer.enabled = state; // this is the sprite way 
        else
            Debug.LogWarning($"{gameObject.name} is missing highlight Sprite!");
    }


    public void FlipCard(bool showFront, float duration = 0.5f)
    {
        float halfDuration = duration / 2f;

        // Rotate to halfway (Y 90°)
        transform.DORotate(new Vector3(0, 90, 0), halfDuration)
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                // Mid-flip — switch visibility
                frontRenderer.enabled = showFront;
                backRenderer.enabled = !showFront;

                // Instantly reset to zero
                transform.DORotate(new Vector3(0, 0, 0), 0f);

                // Optional: slight bounce for flair
                transform.DOPunchScale(Vector3.one * 0.1f, 0.2f, 5, 1)
                        .SetDelay(0.01f);
            });
    }

    public void MarkDiscarded() => isDiscarded = true;

    public void ResetDiscard() => isDiscarded = false;

    
}
