using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    [Header("Prefabs & Positions")]
    public GameObject [] cardPrefabs;
    public Transform deckPos;
    public Transform playerHandPos,  discardPilePos, deckTransform;

    private List<GameObject> deck = new List<GameObject>();
    private List<GameObject> aiCards = new List<GameObject>(); // AI hand

    private HandManager handManager;
    private TurnIndicatorLogic turnIndicator;
    AI_DeckManager AIManager;
    [SerializeField] private Text deckCountText;
    private AudioSource audioSource;
    public AudioClip dealClip;
    public AudioClip[] shuffleSound;  // Initially wanted making it play random clips during shuffle, this will be on pause for now
    public Button DrawButton;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        handManager = GameObject.Find("HandManager").GetComponent<HandManager>();
        AIManager = GameObject.Find("AiDeckManager").GetComponent<AI_DeckManager>();
        turnIndicator = FindFirstObjectByType <TurnIndicatorLogic>();  //  I like this newly learnt method of finding reference
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

        yield return new WaitForSeconds(0.25f);
        yield return StartCoroutine(DealCardsToAllPlayers(5));
        yield return new WaitForSeconds(0.5f);

        // Allow DiscardPile start snapping now, because initially the snap functionality did not allow cards to reach the second AI position
        var discard = FindFirstObjectByType<DiscardPile>();
        if (discard != null) discard.canSnap = true;
    }

    IEnumerator DealCardsToAllPlayers(int cardsEach)
    {
        for (int i = 0; i < cardsEach; i++)
        {
            yield return new WaitForSeconds(0.35f);

            audioSource.PlayOneShot(dealClip);
            handManager.DrawCardFromDeck();  // Function in in handManager.cs that enables player hand to draw card from deck

            yield return new WaitForSeconds(0.35f);

            AIManager.DrawCardToAI(0);  // Deal to first AI from the AIDeckManager.cs

            yield return new WaitForSeconds(0.35f);   // Brief wait

            AIManager.DrawCardToAI(1);   // Deal to second AI..... and could go on for the third, fourth, etc
        }

        yield return new WaitForSeconds(1f);  // Wait for a brief second

        // Deal one card to the discard pile - a public gameobject transform...
        var discardCard = DrawCardFromDeck();
        if (discardCard != null)
        {
            discardCard.transform.SetParent(null);
            discardCard.transform.DOMove(discardPilePos.position, 0.5f).SetEase(Ease.OutCubic);
            discardCard.GetComponent<Card>().FlipCard(true);
            audioSource.PlayOneShot(dealClip);
            yield return new WaitForSeconds(0.7f);
        }

        // After that Re-stack the remaining cards and move to new position - a public gameObject transform - deckTransform
        ReStackDeck();

        yield return new WaitForSeconds(1);

        handManager.FlipPlayerCards();  // Flip player cards ready to play

        yield return new WaitForSeconds(0.4f);
        turnIndicator.ShowIndicator();

        // Allow gameplay
        GameManager.Instance.canStartRound = true;
        handManager.DisableDiscarding();
    }
 
    // Method to move the deck pile back to the draw position with a speed of 0.8f, move gracefully
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

    public GameObject DrawCardFromDeck()
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
        for (int i = 0; i < cardsEach; i++)
        {
            turnIndicator.NextTurn();  // Bleeping indicator game object shows on the next player and deactivates on the rest

            float AIThinkingTime = Random.Range(2f, 6f);
            
            yield return new WaitForSeconds(1.5f); 

            StartCoroutine(AIManager.DrawAndDiscard(0));
            yield return new WaitForSeconds(1.8f); 
            
            turnIndicator.NextTurn();   // Bleeping indicator game object shows on the next player and deactivates on the rest

            yield return new WaitForSeconds(AIThinkingTime);   // Brief wait to simulate AI thinking capability

            // AIManager.DrawCardToAI(1);   // Deal to second AI.....
            audioSource.PlayOneShot(dealClip);
            StartCoroutine(AIManager.DrawAndDiscard(1));
            yield return new WaitForSeconds(1.8f); 

            turnIndicator.NextTurn();  // Bleeping indicator game object shows on the next player and deactivates on the rest

            yield return new WaitForSeconds(1.8f);  // Total time it takes for the AIs to think, evaluate discardable card, and eventually discard
 
        }

        GameManager.Instance.PlayerTurn();
    }


    public void Update()
    {
        deckCountText.text = deck.Count.ToString();  // I'll fix this somewhere so that it doesn't run everyframe
    }

}

