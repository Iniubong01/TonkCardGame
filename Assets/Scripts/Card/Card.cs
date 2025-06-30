using UnityEngine;
using DG.Tweening;

public class Card : MonoBehaviour
{
    private Vector3 offset;
    private bool isDragging = false;
    private Vector3 originalPos;

    public SpriteRenderer frontRenderer;
    public SpriteRenderer backRenderer;

    private static readonly Vector3 LockedScale = Vector3.one * 0.3f;  // to disable card rescaling from another script
    private HandManager handManager;
    public bool isPlayerCard, isAICard;

    private bool isDiscarded = false;

    // [SerializeField] private int cardValue;

    void Awake()
    {
        EnforceScale();
        handManager = GameObject.Find("HandManager").GetComponent<HandManager>();
    }

    void LateUpdate()
    {
        if (transform.localScale != LockedScale)
            transform.localScale = LockedScale;
    }

    void OnMouseDown()
    {
        // 🚫 Prevent dragging if card is already discarded
        if (isDiscarded) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        offset = transform.position - mouseWorld;

        originalPos = transform.position;
        isDragging = true;

        GetComponent<SpriteRenderer>().sortingOrder = 10;
    }

    void OnMouseDrag()
    {
        if (isDragging && !isDiscarded)
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

    public void Flip(float duration = 0.5f)
    {
        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOScaleX(0f, duration / 2))
           .AppendCallback(ShowFront)
           .Append(transform.DOScaleX(LockedScale.x, duration / 2));
    }

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

    public void EnforceScale() => transform.localScale = LockedScale;

    public void MarkDiscarded() => isDiscarded = true;

    public void ResetDiscard() => isDiscarded = false;
}
