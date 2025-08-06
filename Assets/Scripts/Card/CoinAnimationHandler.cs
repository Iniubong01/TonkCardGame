using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

public class CoinAnimationHandler : MonoBehaviour
{
    [Header("Coins")]
    public RectTransform coinPlayer, coinAI1, coinAI2;

    [Header("Targets")]
    public RectTransform centerPoint, rewardTarget;

    [Header("FX")]
    public GameObject centerFX, rewardFX;

    public float moveDuration = 0.8f, delayBetweenCoins = 0.15f;
    private AudioSource audioSource;
    public AudioClip coinClip, stakeClip;
    public RectTransform betSlate;
    public CanvasGroup betHolderCanvasGroup;
    [SerializeField] float popScale = 1.07f, duration = 0.4f;
    private DeckManager deckManager;


    /// </summary>
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        deckManager = GameObject.Find("DeckManager").GetComponent<DeckManager>();
    }

    public void PlayCoinAnimation()
    {
        StartCoroutine(AnimateCoins());
        Debug.Log("Animating Coins!");
    }

    private IEnumerator AnimateCoins()
    {
        coinPlayer.gameObject.SetActive(true);
        coinAI1.gameObject.SetActive(true);
        coinAI2.gameObject.SetActive(true);

        // Move and spin to center
        AnimateCoin(coinPlayer, delay: 0f);
        AnimateCoin(coinAI1, delay: delayBetweenCoins);
        AnimateCoin(coinAI2, delay: delayBetweenCoins * 2);

        yield return new WaitForSeconds(moveDuration + delayBetweenCoins * 2 + 0.2f);

        if (centerFX) centerFX.SetActive(true);

        yield return new WaitForSeconds(0.5f);

        // Move all to reward spot
        Sequence rewardSeq = DOTween.Sequence();

        foreach (var coin in new[] { coinPlayer, coinAI1, coinAI2 })
        {
            rewardSeq.Join(coin.DOMove(rewardTarget.position, 0.6f).SetEase(Ease.InOutQuad));
            audioSource.PlayOneShot(coinClip);
        }

        rewardSeq.OnComplete(() =>
        {
            if (rewardFX) rewardFX.SetActive(true);

            foreach (var coin in new[] { coinPlayer, coinAI1, coinAI2 })
                coin.gameObject.SetActive(false);

            // Update texts
            GameManager.Instance.SetReward();
        });

        yield return new WaitForSeconds(0.6f);
        deckManager.BeginCountdown();
    }

    private void AnimateCoin(RectTransform coin, float delay)
    {
        Vector3 originalPos = coin.position;
        coin.localScale = Vector3.zero;
        coin.position = originalPos;

        Sequence seq = DOTween.Sequence();
        seq.AppendInterval(delay);
        seq.Append(coin.DOScale(Vector3.one, 0.4f));
        seq.Join(coin.DOMove(centerPoint.position, moveDuration).SetEase(Ease.InOutCubic));
        seq.Join(coin.DORotate(new Vector3(0, 360, 0), moveDuration, RotateMode.FastBeyond360));
    }

    public void CloseBetSlip()
    {
        Sequence closeSeq = DOTween.Sequence();

        audioSource.PlayOneShot(stakeClip);
        closeSeq.Append(betSlate.DOScale(popScale, duration * 0.5f).SetEase(Ease.OutQuad))
                .Append(betSlate.DOScale(0f, 0.5f).SetEase(Ease.InBack))
                .Append(betHolderCanvasGroup.DOFade(0, duration))
                .OnComplete(() =>
                {
                    betHolderCanvasGroup.gameObject.SetActive(false);
                });

        GameManager.Instance.SetReward();

        PlayCoinAnimation();
    }
}
