using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [SerializeField] private Button playerDrawButton, DiscardDrawButton, KnockButton;
    private DeckManager deckManager;
    private AI_DeckManager aiHand;
    private CoinAnimationHandler coinAnimationHandler;
    [HideInInspector] public bool canStartRound = false;
    public bool HasNotCheckedTonk = true;
    [SerializeField] private GameObject winUI;
    [SerializeField] Text DisplayText;

    public int StakedCoins, RewardCoins, TotalCoins;
    public Text StakedCoinsText, RewardCoinsText, TotalCoinsText;
    [SerializeField] private InputField StakeInputField;
    public Button stakeButton;
    private AudioSource audioSource;
    [SerializeField] AudioClip jubilationClip;

    public float AiDifficultyLevel;


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
        coinAnimationHandler = FindFirstObjectByType<CoinAnimationHandler>();
        audioSource = GetComponent<AudioSource>();

        LoadCoinData();
        UpdateCoinUI();

        stakeButton.interactable = false;
        KnockButton.interactable = false;

        StakeInputField.contentType = InputField.ContentType.IntegerNumber;
        StakeInputField.ForceLabelUpdate();
        StakeInputField.onValueChanged.AddListener(OnStakeInputChanged);

        // Reset();
    }

    public void PlayerFinishedTurn()
    {
        FindFirstObjectByType<HandManager>().enabled = false;
        FindFirstObjectByType<HandManager>().DisableDiscarding();
        playerDrawButton.interactable = false;
        DiscardDrawButton.interactable = false;

        var discard = FindFirstObjectByType<DiscardPile>();
        if (discard != null) discard.canSnap = false;

        deckManager.AIDrawCard();
        deckManager.startReshuffling();
        KnockButton.interactable = false;
    }

    public void PlayerTurn()
    {
        FindFirstObjectByType<HandManager>().enabled = true;
        playerDrawButton.interactable = true;
        DiscardDrawButton.interactable = true;
        KnockButton.interactable = true;
    }

    public void RoundOver()
    {
        winUI.SetActive(true);
        canStartRound = false;
        audioSource.PlayOneShot(jubilationClip);
    }

    public void RestartRound()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void TonkWin()
    {
        DisplayText.text = "TONK";
        DisplayText.transform.localScale = Vector3.zero;
        DisplayText.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBounce);

        Time.timeScale = 0.5f;
        DOVirtual.DelayedCall(2f, () => Time.timeScale = 1f);
        winUI.SetActive(true);
        canStartRound = false;
    }

    public void LoadCoinData()
    {
        StakedCoins = PlayerPrefs.GetInt("StakedCoins", 0);
        RewardCoins = PlayerPrefs.GetInt("RewardCoins", 0);
        TotalCoins = PlayerPrefs.GetInt("TotalCoins", 5000); // Default starting coins
    }

    public void SaveCoinData()
    {
        PlayerPrefs.SetInt("StakedCoins", StakedCoins);
        PlayerPrefs.SetInt("RewardCoins", RewardCoins);
        PlayerPrefs.SetInt("TotalCoins", TotalCoins);
        PlayerPrefs.Save();
    }

    public void Reset()
    {
        PlayerPrefs.DeleteAll();
    }

    public void UpdateCoinUI()
    {
        StakedCoinsText.text = StakedCoins.ToString();
        RewardCoinsText.text = RewardCoins.ToString();
        TotalCoinsText.text = TotalCoins.ToString();
    }

    public void PlayerWon()
    {
        TotalCoins += RewardCoins;
        SaveCoinData();
        UpdateCoinUI();
        Debug.Log("Player won this round!");
        RoundOver();
    }

    public void PlayerLost()
    {
        TotalCoins -= StakedCoins;
        SaveCoinData();
        UpdateCoinUI();
        Debug.Log("Player lost this round!");
        RoundOver();
    }

    private void OnStakeInputChanged(string input)
    {
        if (int.TryParse(input, out int stakeAmount))
        {
            if (stakeAmount > 0 && stakeAmount <= TotalCoins)
            {
                stakeButton.interactable = true;
                StakedCoins = stakeAmount;
                SaveCoinData();
                UpdateCoinUI();
                return;
            }
        }

        stakeButton.interactable = false;
    }

    public void SetReward()
    {
        RewardCoins = StakedCoins * 3;
        SaveCoinData();
        UpdateCoinUI();
        Debug.Log("Reward set");
    }

    // Removed Update() to prevent constant redundant UI updates
}
