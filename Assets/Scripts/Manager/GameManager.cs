using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [SerializeField] private Button playerDrawButton, DiscardDrawButton;
    private DeckManager deckManager;
    private AI_DeckManager aiHand;
    [HideInInspector] public bool canStartRound = false, playerHasDrawnThisTurn = false;
    [SerializeField] private GameObject winUI;



    /// Awake is called when the script instance is being loaded.
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
        deckManager = GameObject.Find("DeckManager").GetComponent<DeckManager>();
        aiHand = GameObject.Find("AiDeckManager").GetComponent<AI_DeckManager>();
    }

    public void PlayerFinishedTurn()
    {
        // TODO: Used this as a head start
        // Disable player input
        FindFirstObjectByType<HandManager>().enabled = false;
        playerDrawButton.interactable = false;
        DiscardDrawButton.interactable = false;

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
        DiscardDrawButton.interactable = true;

        var discardSnap = FindFirstObjectByType<DiscardPile>();
        if (discardSnap != null) discardSnap.canSnap = true;

        playerHasDrawnThisTurn = false;
    }


    public void RoundOver()
    {
        // TODO: Create a result display UI, to access which player wont the round, regardless
        winUI.SetActive(true);
    }

    public void RestartRound()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }



}

/*

Yesterday's Deliverables
-   I added a withdraw from Dicard Pile.
-   Made spread splines as arrays, just like in the reference game, for player and all the AIs.
-   Added a win UI that displays the winner of the round.
-   Added a method to determine the winner based on the lowest hand value.

Today's Milestones
-   Work on finishing remaining sprint features 

(Recording here enclosed)

*/
