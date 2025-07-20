using UnityEngine;
using DG.Tweening;

public class TurnIndicatorLogic : MonoBehaviour
{
    public GameObject[] turnIndicators;
    private int currentPlayerIndex = 0;
    private Tween currentPulseTween;

    void Start()
    {
        SetAllIndicatorsInactive();
        // I have commented this out so that the turn indicator only shows when gameCanStart
        // ActivateIndicator(currentPlayerIndex);    
    }

    public void NextTurn()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % turnIndicators.Length;
        SetAllIndicatorsInactive();
        ActivateIndicator(currentPlayerIndex);
    }

    void SetAllIndicatorsInactive()
    {
        foreach (GameObject indicator in turnIndicators)
        {
            indicator.SetActive(false);
            indicator.transform.localScale = Vector3.one;

            // Kill any existing pulse tweens on them
            DOTween.Kill(indicator.transform);
        }
    }

    public void ActivateIndicator(int index)
    {
        if (index >= 0 && index < turnIndicators.Length)
        {
            GameObject indicator = turnIndicators[index];
            indicator.SetActive(true);

            // Pulse effect: I could use Unity recording animations on them, but this is okay
            indicator.transform.DOScale(1.2f, 0.3f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
    }

    public void ShowIndicator()
    {
        ActivateIndicator(currentPlayerIndex); 
    }
}
