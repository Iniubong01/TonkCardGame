using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;

public class DeckManager : MonoBehaviour
{
    [Header("Prefabs & Positions")]
    public GameObject cardPrefab;
    public Transform deckPos;
    public Transform playerHandPos, deckTransform, discardPilePos;
    public List<Transform> aiHandPositions;

    [Header("Card Faces")]
    public List<Sprite> cardFaces;  // Face of cards, shouldbe 52

    private List<GameObject> deck = new List<GameObject>();
    private List<GameObject> aiCards = new List<GameObject>(); // AI hand

    private HandManager handManager;
    [SerializeField] private Text deckCountText;
    private AudioSource audioSource;
    public AudioClip dealClip;
    public AudioClip[] shuffleSound;  // Initially wanted making it play random clips during shuffle, this will be on pause fn

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        handManager = GameObject.Find("HandManager").GetComponent<HandManager>();
        CreateFullDeck();
        ShuffleDeck();
        StartCoroutine(AnimateShuffle());
    }

    void CreateFullDeck()
    {
        foreach (var face in cardFaces)
        {
            var card = Instantiate(cardPrefab, deckPos.position, Quaternion.identity);
            var cs = card.GetComponent<Card>();
            cs.SetFace(face);
            cs.ShowBack();
            card.transform.localScale = Vector3.one * 0.3f;
            card.transform.SetParent(deckTransform, true);
            deck.Add(card);
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
        float radius = 1.4f;  // The card spread radius sixe during the spread animation
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

        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(DealCardsToAllPlayers(5));
        yield return new WaitForSeconds(0.5f);

        // Re-stack the remaining cards
        ReStackDeck();

        // Allow DiscardPile start snapping now, because initially the snap functionality did not allow cards to reach the second AI position
        var discard = FindObjectOfType<DiscardPile>();
        if (discard != null) discard.canSnap = true;
    }

    IEnumerator DealCardsToAllPlayers(int cardsEach)
    {
        for (int i = 0; i < cardsEach; i++)
        {
            handManager.DrawCardFromDeck();  // Function in in handManager.cs that enables player hand to draw card from deck
            audioSource.PlayOneShot(dealClip);
            yield return new WaitForSeconds(0.25f);

            foreach (var aiPos in aiHandPositions)
            {
                var card = DrawCardFromDeck();
                if (card == null) yield break;

                card.transform.SetParent(null);

                // Tag as AI card
                var cardComp = card.GetComponent<Card>();
                cardComp.isAICard = true;
                cardComp.isPlayerCard = false;

                aiCards.Add(card); // Track card

                var offset = new Vector3(i * 0.2f, i * 0.05f, 0);  // Might get this arrangement offset out, depending tho
                card.transform.DOMove(aiPos.position + offset, 0.5f).SetEase(Ease.OutCubic);
                card.transform.DOScale(Vector3.one * 0.3f, 0.3f);
                cardComp.ShowBack();
                audioSource.PlayOneShot(dealClip);
                yield return new WaitForSeconds(0.25f);
            }
        }

        yield return new WaitForSeconds(0.5f);

        var discardCard = DrawCardFromDeck();
        if (discardCard != null)
        {
            discardCard.transform.SetParent(null);
            discardCard.transform.position = deckTransform.position;
            discardCard.transform.DOMove(discardPilePos.position, 0.5f).SetEase(Ease.OutCubic);
            discardCard.GetComponent<Card>().ShowFront();
            audioSource.PlayOneShot(dealClip);
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void ReStackDeck()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            var card = deck[i];
            card.transform.SetParent(deckTransform, true);
            var z = -0.01f * i;
            var target = deckTransform.position + new Vector3(0, 0, z);
            card.transform.DOMove(target, 0.5f).SetEase(Ease.InOutSine);
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

    public void AIDrawCard()
    {
        StartCoroutine(AITurn(1));
    }

    IEnumerator AITurn(int cardsEach)   //  Temporary method to enable enemy drawing
    {
        for (int i = 0; i < cardsEach; i++)
        {
            audioSource.PlayOneShot(dealClip);

            float AIThinkingTime = Random.Range(0.7f, 3.2f);

            foreach (var aiPos in aiHandPositions)
            {
                yield return new WaitForSeconds(AIThinkingTime);

                var card = DrawCardFromDeck();
                if (card == null) yield break;

                card.transform.SetParent(null);

                // Tag as AI card
                var cardComp = card.GetComponent<Card>();
                cardComp.isAICard = true;
                cardComp.isPlayerCard = false;

                aiCards.Add(card); // Track card

                var offset = new Vector3(i * 0.2f, i * 0.05f, 0);
                card.transform.DOMove(aiPos.position + offset, 0.5f).SetEase(Ease.OutCubic);
                card.transform.DOScale(Vector3.one * 0.3f, 0.3f);
                cardComp.ShowBack();
                audioSource.PlayOneShot(dealClip);
            }
        }

        GameManager.Instance.PlayerTurn();
    }

    public void Update()
    {
        deckCountText.text = deck.Count.ToString();  // I'll fix this somewhere so that it doesn't run everyframe
    }

}

