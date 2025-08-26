using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Handles integration with Monad Games ID for score submission
/// </summary>
public class MonadGamesIDIntegration : MonoBehaviour
{
    [Header("Server Configuration")]
    [SerializeField] private string serverUrl = "https://chogtanks-server.onrender.com";
    [SerializeField] private string monadGamesIdEndpoint = "/api/monad-games-id/update-player";
    
    [Header("Debug UI")]
    [SerializeField] private Text debugText;
    
    private static MonadGamesIDIntegration _instance;
    public static MonadGamesIDIntegration Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<MonadGamesIDIntegration>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MonadGamesIDIntegration");
                    _instance = go.AddComponent<MonadGamesIDIntegration>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    /// <summary>
    /// Submit score to Monad Games ID when a player mints an NFT
    /// </summary>
    /// <param name="playerAddress">Player's wallet address</param>
    /// <param name="scoreAmount">Score amount to submit</param>
    /// <param name="transactionAmount">Transaction amount in wei</param>
    public void SubmitScoreOnMint(string playerAddress, int scoreAmount, string transactionAmount)
    {
        Debug.Log($"[MonadGamesID] Submitting score on mint: Player={playerAddress}, Score={scoreAmount}, TxAmount={transactionAmount}");
        StartCoroutine(SubmitToServerCoroutine(playerAddress, scoreAmount, transactionAmount, "mint"));
    }
    
    /// <summary>
    /// Submit score to Monad Games ID when a player evolves an NFT
    /// </summary>
    /// <param name="playerAddress">Player's wallet address</param>
    /// <param name="scoreAmount">Score amount to submit</param>
    /// <param name="transactionAmount">Transaction amount in wei</param>
    public void SubmitScoreOnEvolve(string playerAddress, int scoreAmount, string transactionAmount)
    {
        Debug.Log($"[MonadGamesID] Submitting score on evolve: Player={playerAddress}, Score={scoreAmount}, TxAmount={transactionAmount}");
        StartCoroutine(SubmitToServerCoroutine(playerAddress, scoreAmount, transactionAmount, "evolve"));
    }
    
    /// <summary>
    /// Test method to simulate a mint score submission
    /// </summary>
    public void TestMintSubmission()
    {
        string testAddress = "0x123456789abcdef0123456789abcdef012345678";
        int testScore = 100;
        string testAmount = "1000000000000000000"; // 1 ETH in wei
        
        Debug.Log("[MonadGamesID] Testing mint submission with test data");
        SubmitScoreOnMint(testAddress, testScore, testAmount);
    }
    
    /// <summary>
    /// Test method to simulate an evolve score submission
    /// </summary>
    public void TestEvolveSubmission()
    {
        string testAddress = "0x123456789abcdef0123456789abcdef012345678";
        int testScore = 200;
        string testAmount = "2000000000000000000"; // 2 ETH in wei
        
        Debug.Log("[MonadGamesID] Testing evolve submission with test data");
        SubmitScoreOnEvolve(testAddress, testScore, testAmount);
    }
    
    /// <summary>
    /// Coroutine to submit data to the backend server
    /// </summary>
    private IEnumerator SubmitToServerCoroutine(string playerAddress, int scoreAmount, string transactionAmount, string actionType)
    {
        // Create the payload
        ScoreSubmissionPayload payload = new ScoreSubmissionPayload
        {
            playerAddress = playerAddress,
            scoreAmount = scoreAmount,
            transactionAmount = transactionAmount,
            actionType = actionType
        };
        
        string jsonPayload = JsonUtility.ToJson(payload);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        
        // Construct the full URL
        string url = $"{serverUrl}{monadGamesIdEndpoint}";
        
        // Create and send the request
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            Debug.Log($"[MonadGamesID] Sending request to {url} with payload: {jsonPayload}");
            UpdateDebugText($"Sending to Monad Games ID: {actionType}...");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[MonadGamesID] Success response: {request.downloadHandler.text}");
                UpdateDebugText($"Monad Games ID {actionType} success!");
            }
            else
            {
                Debug.LogError($"[MonadGamesID] Error: {request.error} - {request.downloadHandler.text}");
                UpdateDebugText($"Monad Games ID {actionType} failed: {request.error}");
            }
        }
    }
    
    private void UpdateDebugText(string message)
    {
        if (debugText != null)
        {
            debugText.text = message;
        }
    }
    
    /// <summary>
    /// Data structure for score submission payload
    /// </summary>
    [Serializable]
    private class ScoreSubmissionPayload
    {
        public string playerAddress;
        public int scoreAmount;
        public string transactionAmount;
        public string actionType;
    }
}