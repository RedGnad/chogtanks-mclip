using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;
using System.Collections;
using Newtonsoft.Json;
using Photon.Pun;
using ExitGames.Client.Photon;
using Photon.Realtime;

namespace Sample
{
    public class MonadGamesIDManager : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button monadSignInButton;
        [SerializeField] private Button monadSignInButton2; 
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private TextMeshProUGUI statusText;
        
        [Header("Config")]
        [SerializeField] private string gameWalletAddress = "0x8107edd492E8201a286b163f38d896a779AFA6b9";
        [SerializeField] private string monadGamesContractAddress = "0x1234567890123456789012345678901234567890";
        [SerializeField] private string monadRpcUrl = "https://testnet-rpc.monad.xyz/";
        [SerializeField] private string monadChainId = "10143"; 
        
        [Header("Scoring Strategy - À définir")]
        [SerializeField] private bool useTransactionCount = false;
        [SerializeField] private bool useGameScore = true;
        
        [Header("UI Management")]
        [SerializeField] private TMP_Text mainScreenPlayerNameText;
        [SerializeField] private GameObject panelToHide;
        
        [Header("UI Elements to Disable When Connected")]
        [SerializeField] private TMP_InputField[] inputFieldsToDisable;
        [SerializeField] private Button[] buttonsToDisable;
        [SerializeField] private GameObject[] gameObjectsToDisable;
        
        private string currentUsername = "";
        private bool isSignedIn = false;
        
        private static MonadGamesIDManager _instance;
        public static MonadGamesIDManager Instance => _instance;
        
        public static event System.Action<string> OnUsernameChanged;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SetupUI();
            LoadSavedState();
            
            MonadGamesIDWebView.OnMonadGamesIDResultEvent += OnMonadWebViewResult;
            
            StartCoroutine(ForceRestoreUsernameAfterDelay());
        }
        
        private System.Collections.IEnumerator ForceRestoreUsernameAfterDelay()
        {
            yield return new WaitForSeconds(1f);
            
            string savedUsername = PlayerPrefs.GetString("MonadGamesID_Username", "");
            if (!string.IsNullOrEmpty(savedUsername))
            {
                SetMonadUsernameAsPlayerName(savedUsername);
                
                yield return new WaitForSeconds(2f);
                if (PhotonNetwork.NickName != savedUsername)
                {
                    PhotonNetwork.NickName = savedUsername;
                }
            }
        }

        private void SetupUI()
        {
            if (monadSignInButton != null)
            {
                monadSignInButton.onClick.AddListener(OnMonadSignInButtonClicked);
            }
            
            if (monadSignInButton2 != null)
            {
                monadSignInButton2.onClick.AddListener(OnMonadSignInButtonClicked);
            }
            
            UpdateUI();
        }
        
        private void OnMonadSignInButtonClicked()
        {
            
            if (MonadGamesIDWebView.Instance != null)
            {
                UpdateStatus("Ouverture WebView...");
                MonadGamesIDWebView.Instance.OpenMonadGamesIDLogin();
            }
            else
            {
                UpdateStatus("Erreur système");
            }
        }

        private void OnMonadWebViewResult(MonadGamesIDWebView.MonadGamesIDResult result)
        {
            if (result.success)
            {
                
                currentUsername = result.username;
                isSignedIn = true;
                SaveState();
                
                UpdateUI();
                UpdateStatus($"Connected: {result.username}");
                OnUsernameChanged?.Invoke(result.username);
                
                SetMonadUsernameAsPlayerName(result.username);
                
                // NOUVEAU: Marquer le joueur comme ayant un Monad ID verified
                SetPlayerMonadVerifiedStatus(true);
                
                PlayerPrefs.SetString("MonadGamesID_Username", result.username);
                PlayerPrefs.SetString("MonadGamesID_WalletAddress", result.walletAddress);
                
                PlayerPrefs.SetString("walletAddress", result.walletAddress);
                PlayerPrefs.Save();
                
                PlayerSession.SetWalletAddress(result.walletAddress);
                
                // Émettre l'adresse Privy dans la room Photon si connecté
                TryEmitPrivyAddressToPhoton();
                
                
                var connect = FindObjectOfType<Sample.ConnectWalletButton>();
                if (connect != null)
                {
                    connect.TriggerPersonalSignCompleted();
                }
            }
            else
            {
                UpdateStatus($"Erreur: {result.error}");
            }
        }

        public async Task CheckMonadGamesUsername(string walletAddress)
        {
            try
            {
                await Task.Delay(100);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MONAD-GAMES] Erreur auto-check: {e.Message}");
            }
        }
        
        public void OnMonadGamesIDFound(string username, string walletAddress)
        {
            
            currentUsername = username;
            isSignedIn = true;
            SaveState();
            
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                UpdateUI();
                UpdateStatus($"Monad Games ID: {username}");
                OnUsernameChanged?.Invoke(username);
                
                SetMonadUsernameAsPlayerName(username);
            });
            
            PlayerPrefs.SetString("MonadGamesID_Username", username);
            PlayerPrefs.SetString("MonadGamesID_WalletAddress", walletAddress);
            PlayerPrefs.Save();
            
            // Émettre l'adresse Privy dans la room Photon si connecté
            TryEmitPrivyAddressToPhoton();
        }
        
        public void OnMonadGamesIDNotFound(string walletAddress)
        {
            Debug.Log($"[MONAD GAMES ID] ⚠️ Aucun username pour wallet: {walletAddress}");
            
            currentUsername = "";
            isSignedIn = false;
            SaveState();
            
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                UpdateUI();
                UpdateStatus("Créer un username Monad Games ID");
            });
            
            PlayerPrefs.SetString("MonadGamesID_WalletAddress", walletAddress);
            PlayerPrefs.DeleteKey("MonadGamesID_Username");
            PlayerPrefs.Save();
        }

        private async Task<string> GetMonadGamesUsername()
        {
            try
            {
                string savedWallet = PlayerPrefs.GetString("MonadGamesID_WalletAddress", "");
                
                if (string.IsNullOrEmpty(savedWallet))
                {
                    return "";
                }

                await Task.Delay(100);
                
                return "";
            }
            catch (System.Exception e)
            {
                return "";
            }
        }

        private string GetUsernameCallData(string walletAddress)
        {
            string methodId = "0x12345678";
            string paddedAddress = walletAddress.Substring(2).PadLeft(64, '0');
            return methodId + paddedAddress;
        }

        private string DecodeUsernameFromHex(string hexData)
        {
            try
            {
                if (hexData.StartsWith("0x"))
                {
                    hexData = hexData.Substring(2);
                }

                byte[] bytes = new byte[hexData.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = System.Convert.ToByte(hexData.Substring(i * 2, 2), 16);
                }

                return System.Text.Encoding.UTF8.GetString(bytes).Trim('\0');
            }
            catch (System.Exception e)
            {
                return "";
            }
        }

        public async Task SubmitScore(int score, int transactionCount = 0)
        {
            if (!isSignedIn)
            {
                return;
            }

            try
            {
                UpdateStatus("Soumission score Monad Games...");
                
                var transaction = new
                {
                    to = monadGamesContractAddress,
                    data = GetSubmitScoreCallData(score, transactionCount),
                    value = "0x0",
                    gas = "0x7530"
                };

                var request = new
                {
                    method = "eth_sendTransaction",
                    @params = new object[] { transaction }
                };

                string savedWallet = PlayerPrefs.GetString("MonadGamesID_WalletAddress", "");
                if (!string.IsNullOrEmpty(savedWallet))
                {
                    await Task.Delay(1000);
                    UpdateStatus($"Score {score} soumis!");
                }
            }
            catch (System.Exception e)
            {
                UpdateStatus("Erreur soumission score");
            }
        }

        private string GetSubmitScoreCallData(int score, int transactionCount)
        {
            string methodId = "0x87654321";
            return methodId;
        }

        private void UpdateUI()
        {
            if (usernameText != null)
            {
                if (isSignedIn && !string.IsNullOrEmpty(currentUsername))
                {
                    usernameText.text = $"Monad ID: {currentUsername}";
                    usernameText.gameObject.SetActive(true);
                    
                    if (mainScreenPlayerNameText != null)
                    {
                        mainScreenPlayerNameText.text = " " + currentUsername;
                        mainScreenPlayerNameText.gameObject.SetActive(true);
                    }
                    
                    if (panelToHide != null)
                    {
                        panelToHide.SetActive(false);
                    }
                    
                    // Disable UI elements when connected
                    if (inputFieldsToDisable != null)
                    {
                        foreach (var inputField in inputFieldsToDisable)
                        {
                            if (inputField != null)
                            {
                                inputField.interactable = false;
                                Debug.Log($"[MONAD-UI] Disabled input field: {inputField.name}");
                            }
                        }
                    }
                    
                    if (buttonsToDisable != null)
                    {
                        foreach (var button in buttonsToDisable)
                        {
                            if (button != null)
                            {
                                button.interactable = false;
                                Debug.Log($"[MONAD-UI] Disabled button: {button.name}");
                            }
                        }
                    }
                    
                    if (gameObjectsToDisable != null)
                    {
                        foreach (var gameObject in gameObjectsToDisable)
                        {
                            if (gameObject != null)
                            {
                                gameObject.SetActive(false);
                                Debug.Log($"[MONAD-UI] Disabled GameObject: {gameObject.name}");
                            }
                        }
                    }
                }
                else
                {
                    usernameText.gameObject.SetActive(false);
                    
                    if (mainScreenPlayerNameText != null)
                    {
                        mainScreenPlayerNameText.gameObject.SetActive(true);
                    }
                    
                    if (panelToHide != null)
                    {
                        panelToHide.SetActive(true);
                    }
                    
                    // Enable UI elements when not connected
                    if (inputFieldsToDisable != null)
                    {
                        foreach (var inputField in inputFieldsToDisable)
                        {
                            if (inputField != null)
                            {
                                inputField.interactable = true;
                                Debug.Log($"[MONAD-UI] Enabled input field: {inputField.name}");
                            }
                        }
                    }
                    if (buttonsToDisable != null)
                    {
                        foreach (var button in buttonsToDisable)
                        {
                            if (button != null)
                            {
                                button.interactable = true;
                                Debug.Log($"[MONAD-UI] Enabled button: {button.name}");
                            }
                        }
                    }
                    if (gameObjectsToDisable != null)
                    {
                        foreach (var gameObject in gameObjectsToDisable)
                        {
                            if (gameObject != null)
                            {
                                gameObject.SetActive(true);
                                Debug.Log($"[MONAD-UI] Enabled GameObject: {gameObject.name}");
                            }
                        }
                    }
                }
            }

            if (monadSignInButton != null)
            {
                var buttonText = monadSignInButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = isSignedIn ? "Connected" : "Monad ID";
                }
            }
            
            if (monadSignInButton2 != null)
            {
                var buttonText = monadSignInButton2.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = isSignedIn ? "Connected" : "Monad ID";
                }
            }
        }

        private void UpdateStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = status;
            }
        }

        private void ShowMonadInfo()
        {
            UpdateStatus($"Monad Games ID: {currentUsername}");
        }

        private void SaveState()
        {
            PlayerPrefs.SetString("monadGamesUsername", currentUsername);
            PlayerPrefs.SetInt("monadGamesSignedIn", isSignedIn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void LoadSavedState()
        {
            currentUsername = PlayerPrefs.GetString("monadGamesUsername", "");
            isSignedIn = PlayerPrefs.GetInt("monadGamesSignedIn", 0) == 1;
            
            if (isSignedIn && !string.IsNullOrEmpty(currentUsername))
            {
                UpdateUI();
                OnUsernameChanged?.Invoke(currentUsername);
                
                SetMonadUsernameAsPlayerName(currentUsername);
                
                // IMPORTANT: Restaurer le statut de vérification Monad
                SetPlayerMonadVerifiedStatus(true);
            }
        }

        private void SetMonadUsernameAsPlayerName(string username)
        {
            if (string.IsNullOrEmpty(username)) return;
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MONAD-PLAYER-NAME] Setting '{username}' as PhotonNetwork.NickName");
            #endif
            
            // SIMPLE: Mettre à jour PhotonNetwork.NickName directement
            PhotonNetwork.NickName = username;
            try
            {
                if (PhotonNetwork.LocalPlayer != null)
                {
                    var props = new ExitGames.Client.Photon.Hashtable { { "privyUsername", username } };
                    PhotonNetwork.LocalPlayer.SetCustomProperties(props);
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[MONAD-PLAYER-NAME] Set CustomProperties privyUsername='{username}'");
                    #endif
                }
            }
            catch (System.Exception e)
            {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[MONAD-PLAYER-NAME] Failed to set privyUsername property: {e.Message}");
                    #endif
            }
            
            // Forcer la mise à jour du mainScreenPlayerNameText via LobbyUI
            var lobbyUI = FindObjectOfType<LobbyUI>();
            if (lobbyUI != null)
            {
                // Utiliser la méthode privée UpdateMainScreenPlayerName via reflection
                var method = typeof(LobbyUI).GetMethod("UpdateMainScreenPlayerName", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(lobbyUI, null);
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[MONAD-PLAYER-NAME] UpdateMainScreenPlayerName() called - should show '{username}'");
                    #endif
                }
            }
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MONAD-PLAYER-NAME] PhotonNetwork.NickName set to '{username}'");
            #endif
        }
        
        /// <summary>
        /// Marque le joueur comme ayant un Monad ID verified via Custom Properties
        /// </summary>
        private void SetPlayerMonadVerifiedStatus(bool isVerified)
        {
            // Ne plus écrire dans les Custom Properties Photon pour éviter les régressions
            PlayerPrefs.SetInt("MonadVerified", isVerified ? 1 : 0);
            PlayerPrefs.Save();
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MONAD-VERIFIED] Saved to PlayerPrefs only (no Photon props): {isVerified}");
            #endif
        }

        public string GetCurrentUsername() => currentUsername;
        
        /// <summary>
        /// Émet l'adresse Privy dans la room Photon courante de manière robuste et non-bloquante
        /// </summary>
        public void TryEmitPrivyAddressToPhoton()
        {
            StartCoroutine(EmitPrivyAddressCoroutine());
        }
        
        private IEnumerator EmitPrivyAddressCoroutine()
        {
            // Retries pour couvrir les latences de join/restore
            const int maxAttempts = 6;
            const float delaySeconds = 0.6f;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
                {
                    if (attempt == maxAttempts)
                    {
                        yield break;
                    }
                    yield return new WaitForSeconds(delaySeconds);
                    continue;
                }

                string privyAddress = PlayerPrefs.GetString("MonadGamesID_WalletAddress", "");
                if (string.IsNullOrEmpty(privyAddress) || privyAddress == "anonymous")
                {
                    yield break;
                }

                var eventData = new ExitGames.Client.Photon.Hashtable();
                string lowered = privyAddress.ToLower();
                eventData["privyWallet"] = lowered;
                eventData["userId"] = PhotonNetwork.AuthValues?.UserId ?? "";
                eventData["timestamp"] = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Option B: publier aussi via GameProperties (forwardées par PathGameProperties)
                try
                {
                    var roomProps = new ExitGames.Client.Photon.Hashtable();
                    roomProps["privyWallet"] = lowered;
                    // Demander à Photon de forward l'update de propriétés vers le WebHook
                    var webFlags = new Photon.Realtime.WebFlags(0);
                    webFlags.HttpForward = true;
                    PhotonNetwork.CurrentRoom?.SetCustomProperties(roomProps, null, webFlags);
                }
                catch (System.Exception e)
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[PRIVY-EMIT] SetCustomProperties failed: {e.Message}");
                    #endif
                }

                bool sent = false;
                try
                {
                    sent = PhotonNetwork.RaiseEvent(171, eventData, RaiseEventOptions.Default, SendOptions.SendReliable);
                }
                catch (System.Exception e)
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[PRIVY-EMIT] RaiseEvent exception: {e.Message}");
                    #endif
                }

                if (sent)
                {
                    yield break;
                }

                if (attempt < maxAttempts)
                {
                    yield return new WaitForSeconds(delaySeconds);
                }
                else
                {
                    #if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[PRIVY-EMIT] ⚠️ Échec envoi event Privy après retries - pas critique");
                    #endif
                }
            }
        }
        public bool IsSignedIn() => isSignedIn;
    }

    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private System.Collections.Generic.Queue<System.Action> _executionQueue = new System.Collections.Generic.Queue<System.Action>();

        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                var go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }

        public void Enqueue(System.Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }
    }
}
