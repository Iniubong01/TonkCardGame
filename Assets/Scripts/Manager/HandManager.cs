using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HandManager : MonoBehaviour
{
    [SerializeField] private int maxHandSize; // max no. of cards allowed in player deck
    [SerializeField] private SplineContainer splineContainer;  // drawn spline shape
    [SerializeField] private Transform spawnPoint; 
    [SerializeField] private Text playerCardText;  // shows the reminaining no. of cards in player hand
    private List<GameObject> handCards = new();

    private DeckManager deckManager;
    private AudioSource audioSource;
    public AudioClip dealClip;
    public GameObject LastDrawnCard { get; private set; }  // getter to ensure that plae=yer card x axis scale is not messed with after initial card dealing

    [Header("UI & Prefabs")]
    [SerializeField] private GameObject inputIndicatorPrefab;
    [SerializeField] private Canvas canvas;

    private GameObject indicator;
    private RectTransform indicatorRect;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        deckManager = GameObject.Find("DeckManager").GetComponent<DeckManager>();

        indicator = Instantiate(inputIndicatorPrefab, canvas.transform);
        indicatorRect = indicator.GetComponent<RectTransform>();
        indicator.SetActive(false);
    }

    public void DrawCardFromDeck()
    {
        if (handCards.Count >= maxHandSize) return;

        GameObject card = deckManager.DrawCardFromDeck();
        if (card == null) return;

        LastDrawnCard = card;

        // Kill lingering tweens
        card.transform.DOKill(true);

        // Reset scale immediately
        card.transform.localScale = Vector3.one * 0.3f;

        // Set position & rotation to current deck position
        card.transform.position = deckManager.deckTransform.position;
        card.transform.rotation = deckManager.deckTransform.rotation;

        // Now flip and animate into hand
        card.GetComponent<Card>().Flip();

        handCards.Add(card);
        UpdateCardPositions();
        UpdateHandCountUI();
        audioSource.PlayOneShot(dealClip);

    }

    // method to enable card discarding
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
    }

    public void UpdateCardPositions()
    {
        if (handCards.Count == 0) return;  // If there is no card left, do not run

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

            // Kill all tweens and reset scale first
            card.transform.DOKill(true);
            card.transform.localScale = Vector3.one * 0.3f; // <- Make sure it starts right

            // Animate movement, rotation, and re-enforce scale
            card.transform.DOMove(splinePosition, 0.3f);
            card.transform.DOLocalRotateQuaternion(rotation, 0.3f);
            card.transform.DOScale(Vector3.one * 0.3f, 0.25f);
        }
    }


    public void UpdateHandCountUI()
    {
        playerCardText.text = handCards.Count.ToString();  
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
}


