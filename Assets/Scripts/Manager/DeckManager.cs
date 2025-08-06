using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    [Header("Prefabs & Positions")]
    public GameObject[] cardPrefabs;
    public Transform deckPos;
    public Transform playerHandPos, discardPilePos, deckTransform;

    private List<GameObject> deck = new List<GameObject>();
    private List<GameObject> aiCards = new List<GameObject>(); // AI hand

    private HandManager handManager;
    private TurnIndicatorLogic turnIndicator;
    private DiscardPile discardPile;
    AI_DeckManager AIManager;
    private int counterDownTime = 3;
    [SerializeField] private Text deckCountText, displayText;
    private AudioSource audioSource;
    public AudioClip dealClip;
    public AudioClip[] shuffleSound;  // Initially wanted making it play random clips during shuffle, this will be on pause for now

    [SerializeField] private GameObject BetSlip;


    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        handManager = GameObject.Find("HandManager").GetComponent<HandManager>();
        AIManager = GameObject.Find("AiDeckManager").GetComponent<AI_DeckManager>();
        discardPile = FindFirstObjectByType<DiscardPile>();

        turnIndicator = FindFirstObjectByType<TurnIndicatorLogic>();   //  Cool reference finding 

        BetSlip.SetActive(true);
    }

    public void BeginCountdown()
    {
        StartCoroutine(StartCountdown());
    }

    IEnumerator StartCountdown()
    {
        float currentTime = counterDownTime;

        while (currentTime > 0)
        {
            currentTime -= Time.deltaTime;
            yield return null;

            displayText.text = Mathf.CeilToInt(currentTime).ToString();
        }

        displayText.text = "";
        CreateFullDeck();
        ShuffleDeck();
        StartCoroutine(AnimateShuffle());
    }

    void CreateFullDeck()
    {
        foreach (var prefab in cardPrefabs)
        {
            var card = Instantiate(prefab, deckPos.position, Quaternion.identity);
            card.transform.localScale = Vector3.one;
            deck.Add(card);

            // Optional: Ensure it shows back first
            var cs = card.GetComponent<Card>();
            if (cs != null)
            {
                cs.ShowBack(); // assumes card prefab already knows its face sprite
            }
        }
    }

    void ShuffleDeck()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            var r = Random.Range(i, deck.Count);
            (deck[i], deck[r]) = (deck[r], deck[i]);
        }
    }

    IEnumerator AnimateShuffle()
    {
        float radius = 1.4f;  // The card shuffle radius size during the spread animation
        var center = deckPos.position;
        audioSource.PlayOneShot(shuffleSound[Random.Range(0, shuffleSound.Length)]);

        for (int i = 0; i < deck.Count; i++)
        {
            var angle = i / (float)deck.Count * Mathf.PI * 2;
            var pos = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius;
            deck[i].transform.DOMove(pos, 0.5f).SetEase(Ease.OutQuad);
            yield return new WaitForSeconds(0.005f);
        }

        yield return new WaitForSeconds(0.5f);

        foreach (var card in deck)
        {
            card.transform.DOMove(deckPos.position, 0.4f).SetEase(Ease.InBack);
            card.transform.DORotate(Vector3.zero, 0.3f);
        }

        yield return new WaitForSeconds(0.4f);
        yield return StartCoroutine(DealCardsToAllPlayers(5));
        yield return new WaitForSeconds(0.5f);

        handManager.DisableDiscarding();
        GameManager.Instance.canStartRound = true;      // Allow gameplay

        // Allow DiscardPile start snapping now, because initially the snap functionality did not allow cards to reach the second AI position
        var discard = FindFirstObjectByType<DiscardPile>();
        if (discard != null) discard.canSnap = true;
    }

    IEnumerator ReShuffleDeck()
    {
        yield return new WaitForSeconds(1f);

        // Step 1: Retain the last discarded card
        Card lastCard = discardPile.RetainLastCardAndClearRest();
        if (discardPile == null || lastCard == null) yield break;

        // Step 2: Find all cards in the scene that are marked as discarded but not the last one
        List<Card> reshuffleCandidates = new List<Card>();

        foreach (Card card in FindObjectsByType<Card>(FindObjectsSortMode.None))
        {
            if (card != lastCard && card.isDiscarded)
            {
                reshuffleCandidates.Add(card);
            }
        }

        // Step 3: Animate them toward the deck pile
        for (int i = 0; i < reshuffleCandidates.Count; i++)
        {
            Card card = reshuffleCandidates[i];
            card.transform.SetParent(null); // Detach from previous parent

            float z = -0.01f * (deck.Count + i); // So they stack behind the current deck
            Vector3 targetPos = deckTransform.position;
            discardPile.PlayDiscardClip();

            card.transform.DOMove(targetPos, 0.5f).SetEase(Ease.InOutSine);
            card.transform.DORotate(Vector3.zero, 0.3f);
            card.ShowBack();

            yield return new WaitForSeconds(0.05f);
        }

        yield return new WaitForSeconds(0.6f);

        // Step 4: Parent to deckTransform and stack properly
        for (int i = 0; i < reshuffleCandidates.Count; i++)
        {
            Card card = reshuffleCandidates[i];
            GameObject cardObj = card.gameObject;

            card.transform.SetParent(deckTransform, true);

            float z = -0.01f * (deck.Count + i); // Continue layering behind
            Vector3 stackedPos = deckTransform.position + new Vector3(0, 0, z);

            card.transform.DOMove(stackedPos, 0.8f).SetEase(Ease.InOutSine);
            card.transform.DORotate(Vector3.zero, 0.5f);

            deck.Add(cardObj);
            card.isDiscarded = false;
        }

        Debug.Log("Reshuffle complete! Cards returned to deck.");
    }


    IEnumerator DealCardsToAllPlayers(int cardsEach)
    {
        for (int i = 0; i < cardsEach; i++)
        {
            AIManager.DrawCardToAI(1);   // Second AI
            audioSource.PlayOneShot(dealClip);

            yield return new WaitForSeconds(0.4f);

            AIManager.DrawCardToAI(0);   // First AI
            audioSource.PlayOneShot(dealClip);

            yield return new WaitForSeconds(0.4f);

            handManager.DrawCardFromDeck();  // Player
            audioSource.PlayOneShot(dealClip);

            yield return new WaitForSeconds(0.4f);
        }

        yield return new WaitForSeconds(1f);

        // Deal one to discard pile
        var discardCard = RemoveCardFromDeck();
        if (discardCard != null)
        {
            discardCard.transform.SetParent(discardPilePos); // Reparent the discard card to the discardPilePos
            discardCard.transform.DOMove(discardPilePos.position, 0.5f).SetEase(Ease.OutCubic);
            discardCard.GetComponent<Card>().FlipCard(true);
            audioSource.PlayOneShot(dealClip);
            discardPile.discardedCards.Add(discardCard.GetComponent<Card>()); // Just fixed this, it was omitted initially, meaning...
                                                                              // ...I only added animated a card to that position without actually adding that card to the discard List<>
            
            yield return new WaitForSeconds(0.7f);
        }

        ReStackDeck();

        yield return new WaitForSeconds(1f);

        handManager.FlipPlayerCards();
        yield return new WaitForSeconds(0.4f);

        turnIndicator.ShowIndicator();
    }


    // Method to move the deck pile back to the draw position with a speed of 0.8f
    private void ReStackDeck()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            var card = deck[i];
            card.transform.SetParent(deckTransform, true);
            var z = -0.01f * i;
            var target = deckTransform.position + new Vector3(0, 0, z);
            card.transform.DOMove(target, 0.8f).SetEase(Ease.InOutSine);
            card.transform.DORotate(Vector3.zero, 0.5f);
        }
    }

    public GameObject RemoveCardFromDeck()
    {
        if (deck.Count == 0) return null;
        var card = deck[0];
        deck.RemoveAt(0);
        return card;
    }

    public void AIDrawCard()    // This method enables AIs to take turns - depending on their choice however, as if they have one, haha
    {
        StartCoroutine(AITurn(1));
    }

    IEnumerator AITurn(int cardsEach)   //  Temporary method to enable enemy drawing
    {
        startReshuffling();

        for (int i = 0; i < cardsEach; i++)
        {
            float AIThinkingTime = Random.Range(1f, 3.5f);

            turnIndicator.NextTurn();  // Bleeping indicator game object shows on the next player and deactivates on the rest
            yield return new WaitForSeconds(AIThinkingTime);
            StartCoroutine(AIManager.DrawAndDiscard(0));
            yield return new WaitForSeconds(3f);

            turnIndicator.NextTurn();   // Bleeping indicator game object shows on the next player and deactivates on the rest
            yield return new WaitForSeconds(AIThinkingTime);   // Brief wait to simulate AI thinking capability
            StartCoroutine(AIManager.DrawAndDiscard(1));
            yield return new WaitForSeconds(3f);  // Total time it takes for the AIs to think, evaluate discardable card, and eventually discard

            turnIndicator.NextTurn();  // Bleeping indicator game object shows on the next player and deactivates on the rest
        }

        GameManager.Instance.PlayerTurn();
    }

    //
    public void Update()
    {
        deckCountText.text = deck.Count.ToString();  // I'll fix this somewhere so that it doesn't run everyframe
    }

    public void startReshuffling()
    {
        if (deck.Count == 0)
        {
            StartCoroutine(ReShuffleDeck());
        }
    }
}