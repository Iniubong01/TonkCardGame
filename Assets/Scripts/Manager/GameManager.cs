using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [SerializeField] private Button playerDrawButton;
    private DeckManager deckManager;

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    
    void Start()
    {
        deckManager =  GameObject.Find("DeckManager").GetComponent<DeckManager>();
    }

    public void PlayerFinishedTurn()
    {
        // Used this as a head start
        // Disable player input
        FindFirstObjectByType<HandManager>().enabled = false;
        playerDrawButton.interactable = false;

        // Disable discard snapping
        var discard = FindFirstObjectByType<DiscardPile>();
        if (discard != null) discard.canSnap = false;

        // Start AI turns
        deckManager.AIDrawCard();
    }

    public void PlayerTurn()
    {
        // After all AIs are done, re-enable player input
        FindFirstObjectByType<HandManager>().enabled = true;
        playerDrawButton.interactable = true;

        var discardSnap = FindFirstObjectByType<DiscardPile>();
        if (discardSnap != null) discardSnap.canSnap = true;
    }
}
