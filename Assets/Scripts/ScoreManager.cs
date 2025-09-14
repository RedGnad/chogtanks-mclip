using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;
using System.Collections;
using System.Runtime.InteropServices;
using TMPro;
using System.Linq;

[UnityEngine.Scripting.Preserve]
public class ScoreManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const float ROOM_LIFETIME = 180f; 
    private const float RESPAWN_TIME = 5f;
    private const float COIN_SPAWN_INTERVAL = 20f; 
    
    private const byte SCORE_UPDATE_EVENT = 1;
    private const byte MATCH_END_EVENT = 2;
    private const byte MATCH_START_TIME_EVENT = 5;
    private const byte SYNC_TIMER_EVENT = 6;
    
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern bool SubmitScoreJS(string score, string bonus, string walletAddress, string matchId);
    [DllImport("__Internal")]
    private static extern bool RequestMatchTokenJS();
#endif
    
    private Dictionary<int, int> playerScores = new Dictionary<int, int>(); 
    private Dictionary<string, string> playerWallets = new Dictionary<string, string>();
    private Dictionary<string, string> playerPrivyUsernames = new Dictionary<string, string>();
    private bool privyUsernamePropagated = false;
    private float matchStartTime;
    private bool matchEnded = false;
    private static int matchCycle = 0; // compteur de cycles (chaque JoinRoom -> StartMatch)
    private bool startCoroutineLaunched = false;
    
    [Header("Coin System")]
    public GameObject coinPrefab; 
    public Transform[] coinSpawnPoints; 
    private float nextCoinSpawnTime;
    
    [Header("Power-up System")]
    public GameObject[] powerupPrefabs; 
    public float powerupSpawnInterval = 15f;
    private float nextPowerupSpawnTime;
    
    public static ScoreManager Instance { get; private set; }

    public bool HasMatchEnded => matchEnded; // Exposé pour PhotonLauncher (détection boucle)
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            StartMatch();
        }
    }
    
    public void ResetManager()
    {
        playerScores.Clear();
        playerWallets.Clear();
        playerPrivyUsernames.Clear();
        matchStartTime = Time.time; 
        matchEnded = false;
        
        StopAllCoroutines();
    startCoroutineLaunched = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[SM] ResetManager cycle={matchCycle} t={Time.time:F1}");
#endif
    }
    
    public override void OnJoinedRoom()
    {
    matchCycle++;
    ResetManager(); // attendu une seule fois (PhotonLauncher n'appelle plus Reset ici)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[SM] OnJoinedRoom cycle={matchCycle} master={PhotonNetwork.IsMasterClient}");
#endif
    StartMatch();
        
        string walletAddress = GetWalletAddress();
        if (!string.IsNullOrEmpty(walletAddress) && walletAddress != "anonymous")
        {
            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            
            Debug.Log($"[ANTI-SELF-KILL] Envoi wallet AppKit via Event 3: ActorNumber={actorNumber}, Wallet={walletAddress}");
            
            object[] walletData = new object[] { actorNumber.ToString(), walletAddress };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.AddToRoomCache };
            PhotonNetwork.RaiseEvent(3, walletData, options, SendOptions.SendReliable);
            
            playerWallets[actorNumber.ToString()] = walletAddress;

            var props = new ExitGames.Client.Photon.Hashtable { { "wallet", walletAddress } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }
        else
        {
            Debug.LogWarning($"[ANTI-SELF-KILL] Adresse AppKit non trouvée: {walletAddress}");
        }

        // Propager le username Privy réel via CustomProperties (source: MonadGamesIDManager / PlayerPrefs)
        TryPropagatePrivyUsername();
        StartCoroutine(DelayPropagatePrivyUsername());
    }
    
    private void StartMatch()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            matchStartTime = Time.time;
            nextCoinSpawnTime = Time.time + COIN_SPAWN_INTERVAL; // Premier coin dans 20s
            matchEnded = false;
            
            playerScores.Clear();
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                playerScores[player.ActorNumber] = 0;
            }
            
            StartCoroutine(MatchTimer());
            startCoroutineLaunched = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SM] StartMatch(master) cycle={matchCycle} matchStartTime={matchStartTime:F1}");
#endif
            
            SyncMatchTime(ROOM_LIFETIME);
            
            SyncScores();
        }
        else
        {            
            matchStartTime = Time.time - 1;
            
            StartCoroutine(MatchTimer());
            startCoroutineLaunched = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SM] StartMatch(client) cycle={matchCycle} provisional matchStartTime={matchStartTime:F1}");
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try { RequestMatchTokenJS(); } catch {}
#endif
    }
    
    private IEnumerator MatchTimer()
    {
        float timeLeft = ROOM_LIFETIME;
        bool waitingForSync = !PhotonNetwork.IsMasterClient;
        int lastCountdownSecond = -1; 
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdateRoomStatus("Ongoing Match                       ");
            
            if (waitingForSync)
            {
                LobbyUI.Instance.UpdateTimer((int)timeLeft);
            }
        }
        
        float nextSyncTime = 0f;
        
        while (timeLeft > 0 && !matchEnded)
        {
            if (!PhotonNetwork.InRoom)
            {
                yield break; // Room left or closed: stop timer cleanly
            }
            timeLeft = ROOM_LIFETIME - (Time.time - matchStartTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (timeLeft <= 0 && !matchEnded && PhotonNetwork.IsMasterClient)
            {
                Debug.Log($"[SM][WARN] timeLeft<=0 avant EndMatch cycle={matchCycle} dt={(Time.time - matchStartTime):F1}");
            }
#endif
            int currentSecond = Mathf.Max(0, (int)timeLeft);
            
            if (LobbyUI.Instance != null)
            {
                LobbyUI.Instance.UpdateTimer(currentSecond);
            }
            
            if (currentSecond <= 5 && currentSecond >= 1 && currentSecond != lastCountdownSecond)
            {
                if (SFXManager.Instance != null)
                {
                    SFXManager.Instance.PlayCountdownBeep();
                }
                lastCountdownSecond = currentSecond;
            }
            
            if (PhotonNetwork.IsMasterClient && Time.time >= nextCoinSpawnTime && !matchEnded)
            {
                try
                {
                    SpawnCoin();
                    nextCoinSpawnTime = Time.time + COIN_SPAWN_INTERVAL;
                }
                catch (System.Exception e)
                {
                    nextCoinSpawnTime = Time.time + COIN_SPAWN_INTERVAL;
                }
            }
            
            if (PhotonNetwork.IsMasterClient && Time.time >= nextPowerupSpawnTime && !matchEnded)
            {
                try
                {
                    SpawnPowerup();
                    nextPowerupSpawnTime = Time.time + powerupSpawnInterval;
                }
                catch (System.Exception e)
                {
                    nextPowerupSpawnTime = Time.time + powerupSpawnInterval;
                }
            }
            
            if (!matchEnded && PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom && Time.time > nextSyncTime)
            {
                SyncMatchTime(timeLeft);
                nextSyncTime = Time.time + 10f;
            }
            
            yield return null;
            
            if (timeLeft <= 0 && PhotonNetwork.IsMasterClient && PhotonNetwork.InRoom && !matchEnded)
            {
                EndMatch();
            }
        }
    }
    
    public void AddKill(int killerActorNumber)
    {
        AddScore(killerActorNumber, 1);
    }
    
    public void AddKill(int killerActorNumber, int victimActorNumber)
    {
        // Vérification anti-self-kill avec logs de debug
        string killerWallet = GetPlayerWallet(killerActorNumber);
        string victimWallet = GetPlayerWallet(victimActorNumber);
        string killerPrivy = GetPlayerPrivyUsername(killerActorNumber);
        string victimPrivy = GetPlayerPrivyUsername(victimActorNumber);
        
        Debug.Log($"[ANTI-SELF-KILL] Killer {killerActorNumber} (wallet: {killerWallet}, privy: {killerPrivy}) vs Victim {victimActorNumber} (wallet: {victimWallet}, privy: {victimPrivy})");
        
        if (SameWalletIdentity(killerActorNumber, victimActorNumber))
        {
            Debug.Log($"[ANTI-SELF-KILL] BLOQUÉ: même wallet détecté!");
            return; // Pas de points pour self-kill
        }
        
        if (SamePrivyUsernameIdentity(killerActorNumber, victimActorNumber))
        {
            Debug.Log($"[ANTI-SELF-KILL] BLOQUÉ: même username Privy détecté!");
            return; // Pas de points si même username Privy (global wallet)
        }
        
        Debug.Log($"[ANTI-SELF-KILL] Wallets différents, kill autorisé");
        AddScore(killerActorNumber, 1);
    }
    
    public void AddScore(int playerActorNumber, int points)
    {
        if (matchEnded) 
        {
            return;
        }
        
    
        int scoreBefore = playerScores.ContainsKey(playerActorNumber) ? playerScores[playerActorNumber] : 0;
        if (playerScores.ContainsKey(playerActorNumber))
        {
            playerScores[playerActorNumber] += points;
        }
        else
        {
            playerScores[playerActorNumber] = points;
        }
        int scoreAfter = playerScores[playerActorNumber];
        
        
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        object[] content = new object[] { playerActorNumber, playerScores[playerActorNumber] };
        PhotonNetwork.RaiseEvent(SCORE_UPDATE_EVENT, content, options, SendOptions.SendUnreliable); // Optimisé pour mobile

        
        if (PhotonNetwork.IsMasterClient)
        {
            SyncScores();
        }

        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdatePlayerList();
        }
        // LobbyUI null check supprimé pour optimisation mobile
    }
    
    private void HandleScoreUpdate(int actorNumber, int score)
    {
        int before = playerScores.ContainsKey(actorNumber) ? playerScores[actorNumber] : -1;
        playerScores[actorNumber] = score;
        
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdatePlayerList();
        }
    }
    
    public void PlayerDied(int victimActorNumber, int killerActorNumber, int victimViewID)
    {
        
        if (!PhotonNetwork.IsMasterClient) return;

        if (killerActorNumber > 0 && killerActorNumber != victimActorNumber)
        {
            AddKill(killerActorNumber, victimActorNumber);

            string killerName = GetPlayerName(killerActorNumber);
            string victimName = GetPlayerName(victimActorNumber);
            if (LobbyUI.Instance != null && LobbyUI.Instance.killFeedText != null)
            {
                LobbyUI.Instance.killFeedText.text = $"{killerName} a tué {victimName} !";
                LobbyUI.Instance.StartCoroutine(HideKillFeedAfterDelay(3f));
                
                photonView.RPC("PlayKillFeedSoundRPC", RpcTarget.All);
            }
        }
        else
        {
            if (killerActorNumber <= 0);
            if (killerActorNumber == victimActorNumber);
        }

        PhotonView victimView = PhotonView.Find(victimViewID);
        if (victimView != null)
        {
            PhotonNetwork.Destroy(victimView.gameObject);
            
            StartCoroutine(RespawnPlayer(victimActorNumber));
        }
        else
        {
        }
    }

    private string GetPlayerName(int actorNumber)
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == actorNumber)
                return string.IsNullOrEmpty(player.NickName) ? $"Player {actorNumber}" : player.NickName;
        }
        return $"Player {actorNumber}";
    }

    private bool SameWalletIdentity(int actorNumber1, int actorNumber2)
    {
        string wallet1 = GetPlayerWallet(actorNumber1);
        string wallet2 = GetPlayerWallet(actorNumber2);
        
        if (string.IsNullOrEmpty(wallet1) || string.IsNullOrEmpty(wallet2))
            return false; // Si pas de wallet, on considère comme différent
        
        string n1 = NormalizeEthereumAddress(wallet1);
        string n2 = NormalizeEthereumAddress(wallet2);
        return !string.IsNullOrEmpty(n1) && n1 == n2;
    }

    private static string NormalizeEthereumAddress(string address)
    {
        if (string.IsNullOrEmpty(address)) return string.Empty;
        string a = address.Trim();
        if (a.StartsWith("0x") || a.StartsWith("0X"))
        {
            a = a.Substring(2);
        }
        // Doit être 40 hex
        if (a.Length != 40) return string.Empty;
        for (int i = 0; i < a.Length; i++)
        {
            char c = a[i];
            bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHex) return string.Empty;
        }
        // Normalisation bas-de-casse (comparaison case-insensitive). EIP-55 non requis pour égalité.
        return "0x" + a.ToLowerInvariant();
    }

    private bool SamePrivyUsernameIdentity(int actorNumber1, int actorNumber2)
    {
        string u1 = GetPlayerPrivyUsername(actorNumber1);
        string u2 = GetPlayerPrivyUsername(actorNumber2);
        if (string.IsNullOrEmpty(u1) || string.IsNullOrEmpty(u2)) return false;
        string n1 = u1.Trim().ToLowerInvariant();
        string n2 = u2.Trim().ToLowerInvariant();
        // Ignore les valeurs génériques vides après trim
        if (n1.Length == 0 || n2.Length == 0) return false;
        return n1 == n2;
    }

    private string GetPlayerPrivyUsername(int actorNumber)
    {
        // D'abord dans le cache local alimenté par CustomProperties
        if (playerPrivyUsernames.ContainsKey(actorNumber.ToString()))
        {
            return playerPrivyUsernames[actorNumber.ToString()];
        }
        // Sinon, lire directement les CustomProperties du joueur
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == actorNumber)
            {
                if (player.CustomProperties != null && player.CustomProperties.ContainsKey("privyUsername"))
                {
                    var uname = player.CustomProperties["privyUsername"] as string;
                    if (!string.IsNullOrEmpty(uname))
                    {
                        playerPrivyUsernames[actorNumber.ToString()] = uname;
                        return uname;
                    }
                }
                // Fallback: utiliser le Photon NickName si disponible (dans ce projet, il est renseigné par MonadGamesIDManager)
                if (!string.IsNullOrEmpty(player.NickName))
                {
                    return player.NickName;
                }
                break;
            }
        }
        return string.Empty;
    }

    private string GetLocalPrivyUsername()
    {
        try
        {
            // Tenter via réflexion sans référence de type directe (namespace: Sample)
            var managerType = System.Type.GetType("Sample.MonadGamesIDManager, Assembly-CSharp")
                              ?? System.Type.GetType("Sample.MonadGamesIDManager");
            if (managerType != null)
            {
                var mgrObj = UnityEngine.Object.FindObjectOfType(managerType);
                if (mgrObj != null)
                {
                    var method = managerType.GetMethod("GetCurrentUsername", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (method != null)
                    {
                        var result = method.Invoke(mgrObj, null) as string;
                        if (!string.IsNullOrEmpty(result)) return result;
                    }
                }
            }
        }
        catch { }
        // Fallbacks persistés
        string u1 = PlayerPrefs.GetString("MonadGamesID_Username", "");
        if (!string.IsNullOrEmpty(u1)) return u1;
        string u2 = PlayerPrefs.GetString("monadGamesUsername", "");
        return u2;
    }

    private string GetPlayerWallet(int actorNumber)
    {
        Debug.Log($"[ANTI-SELF-KILL] GetPlayerWallet({actorNumber}) appelé");
        Debug.Log($"[ANTI-SELF-KILL] playerWallets contient: {string.Join(", ", playerWallets.Select(kv => $"{kv.Key}={kv.Value}"))}");
        
        // D'abord chercher dans playerWallets (envoyé via event 3)
        if (playerWallets.ContainsKey(actorNumber.ToString()))
        {
            string wallet = playerWallets[actorNumber.ToString()];
            Debug.Log($"[ANTI-SELF-KILL] Wallet trouvé dans playerWallets: {wallet}");
            return wallet;
        }
        
        Debug.Log($"[ANTI-SELF-KILL] Wallet non trouvé dans playerWallets, cherchant dans CustomProperties");
        
        // Sinon chercher dans les CustomProperties du joueur
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == actorNumber)
            {
                if (player.CustomProperties.ContainsKey("wallet"))
                {
                    string wallet = player.CustomProperties["wallet"] as string;
                    Debug.Log($"[ANTI-SELF-KILL] Wallet trouvé dans CustomProperties: {wallet}");
                    return wallet;
                }
                break;
            }
        }
        
        Debug.Log($"[ANTI-SELF-KILL] Aucun wallet trouvé pour ActorNumber {actorNumber}");
        return "";
    }

    private IEnumerator HideKillFeedAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (LobbyUI.Instance != null && LobbyUI.Instance.killFeedText != null)
            LobbyUI.Instance.killFeedText.text = "";
    }
    
    private void SpawnPowerup()
    {
        if (!PhotonNetwork.IsMasterClient) 
        {
            return;
        }
        
        if (powerupPrefabs == null || powerupPrefabs.Length == 0)
        {
            return;
        }
        
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            return;
        }
        
        int randomPowerupIndex = Random.Range(0, powerupPrefabs.Length);
        GameObject selectedPowerup = powerupPrefabs[randomPowerupIndex];
        
        if (selectedPowerup == null)
        {
            return;
        }
        
        Vector3 spawnPosition;
        
        if (coinSpawnPoints != null && coinSpawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, coinSpawnPoints.Length);
            Transform spawnPoint = coinSpawnPoints[randomIndex];
            spawnPosition = spawnPoint.position;
            
        }
        else
        {
            spawnPosition = new Vector3(
                Random.Range(-8f, 8f), 
                Random.Range(-4f, 4f), 
                0f                     
            );
        }
        
        try
        {
            GameObject powerup = PhotonNetwork.Instantiate(selectedPowerup.name, spawnPosition, Quaternion.identity);
            if (powerup != null)
            {
                // Debug.Log($"[POWERUP] Power-up {selectedPowerup.name} spawné avec succès ! NetworkID: {powerup.GetComponent<PhotonView>()?.ViewID}");
            }
        }
        catch (System.Exception e)
        {
            // Debug.LogError($"[POWERUP] Erreur lors de PhotonNetwork.Instantiate: {e.Message}");
        }
    }
    
    [PunRPC]
    void PlayKillFeedSoundRPC()
    {
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayRandomKillFeedSoundLocal();
        }
        else
        {
            Debug.LogError("[KILLFEED] SFXManager.Instance est null sur ce client !");
        }
    }
    
    private void SpawnCoin()
    {
        if (!PhotonNetwork.IsMasterClient) 
        {
            return;
        }
        
        if (coinPrefab == null)
        {
            return;
        }
        
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            return;
        }
        
        Vector3 spawnPosition;
        
        if (coinSpawnPoints != null && coinSpawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, coinSpawnPoints.Length);
            Transform spawnPoint = coinSpawnPoints[randomIndex];
            spawnPosition = spawnPoint.position;
            
        }
        else
        {
            spawnPosition = new Vector3(
                Random.Range(-8f, 8f), 
                Random.Range(-4f, 4f), 
                0f                    
            );
        }
        
        try
        {
            GameObject coin = PhotonNetwork.Instantiate(coinPrefab.name, spawnPosition, Quaternion.identity);
            if (coin != null)
            {
                // Debug.Log($"[COIN] Coin spawné avec succès ! NetworkID: {coin.GetComponent<PhotonView>()?.ViewID}");
            }
        }
        catch (System.Exception e)
        {
            // Debug.LogError($"[COIN] Erreur lors de PhotonNetwork.Instantiate: {e.Message}");
        }
    }
    
    private IEnumerator RespawnPlayer(int actorNumber)
    {
        yield return new WaitForSeconds(RESPAWN_TIME);
        
        if (PhotonNetwork.LocalPlayer.ActorNumber == actorNumber)
        {
            
            foreach (var ui in GameObject.FindGameObjectsWithTag("GameOverUI"))
            {
                Destroy(ui);
            }
            
            var spawner = FindObjectOfType<PhotonTankSpawner>();
            if (spawner != null)
            {
                spawner.SpawnTank();        }
        else
        {
        }
    }
        else
        {
        }
    }
    
    public void EndMatch()
    {
        if (matchEnded) return;
        matchEnded = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[SM] EndMatch cycle={matchCycle} t={Time.time - matchStartTime:F1}s scores={playerScores.Count}");
#endif
        
        // Marquer la room comme terminée et la retirer du matchmaking (master uniquement)
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            try
            {
                PhotonNetwork.CurrentRoom.IsOpen = false;
                PhotonNetwork.CurrentRoom.IsVisible = false;
                var props = new ExitGames.Client.Photon.Hashtable { { "ended", 1 } };
                PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            }
            catch (System.Exception e)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[SM] Failed to flag room ended: {e.Message}");
#endif
            }
        }

        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdateRoomStatus("Match ended!");
        }
        
        int highestScore = -1;
        int winnerActorNumber = -1;
        string winnerName = "Unknown Player";
        
        if (_playerNames == null)
        {
            _playerNames = new Dictionary<int, string>();
        }
        
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            string playerNickname = string.IsNullOrEmpty(player.NickName) ? 
                $"Player {player.ActorNumber}" : player.NickName;
            _playerNames[player.ActorNumber] = playerNickname;
        }
        
        foreach (var pair in playerScores)
        {
            if (pair.Value > highestScore)
            {
                highestScore = pair.Value;
            }
        }
        
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (playerScores.ContainsKey(player.ActorNumber) && playerScores[player.ActorNumber] == highestScore)
            {
                winnerActorNumber = player.ActorNumber;
                winnerName = _playerNames[player.ActorNumber];
                break;
            }
        }
        
        if (winnerActorNumber == -1)
        {
            foreach (var pair in playerScores)
            {
                if (pair.Value == highestScore)
                {
                    winnerActorNumber = pair.Key;
                    winnerName = _playerNames.ContainsKey(winnerActorNumber) ? 
                        _playerNames[winnerActorNumber] : $"Player {winnerActorNumber}";
                    break;
                }
            }
        }
        
        if (winnerActorNumber != -1)
        {
            playerScores[winnerActorNumber]++;
            highestScore++;
            
            if (LobbyUI.Instance != null)
            {
                LobbyUI.Instance.UpdatePlayerList();
            }
        }
        
        if (PhotonNetwork.IsMasterClient)
        {
            object[] content = new object[] { winnerActorNumber, winnerName, highestScore };
            RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            PhotonNetwork.RaiseEvent(MATCH_END_EVENT, content, options, SendOptions.SendReliable);
            
            StartCoroutine(FallbackShowWinner(winnerActorNumber, winnerName, highestScore));
        }
        else
        {
            return;
        }
        
        ShowWinnerAndSubmitScores(winnerActorNumber, winnerName, highestScore);
    }
    
    private static Dictionary<int, string> _playerNames = new Dictionary<int, string>();
    
    public void ShowWinnerAndSubmitScores(int winnerActorNumber, string winnerName, int highestScore)
    {
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdateRoomStatus($"Victory: {winnerName} with {highestScore} points!");
        }
        
        GameObject[] gameOverUIs = GameObject.FindGameObjectsWithTag("GameOverUI");
        
        if (gameOverUIs.Length == 0)
        {
            PhotonLauncher launcher = FindObjectOfType<PhotonLauncher>();
            if (launcher != null)
            {
                launcher.ShowWinnerToAllRPC(winnerName, winnerActorNumber);
            }
            else
            {
                Debug.LogError("[WINNER-DEBUG] PhotonLauncher NOT FOUND!");
            }
        }
        
        int localPlayerScore = 0;
        if (playerScores.ContainsKey(PhotonNetwork.LocalPlayer.ActorNumber))
        {
            localPlayerScore = playerScores[PhotonNetwork.LocalPlayer.ActorNumber];
        }
        
        int bonus = 0;
        
        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            SubmitScoreToFirebase(localPlayerScore, bonus);
        }
        
        ChogTanksNFTManager nftManager = FindObjectOfType<ChogTanksNFTManager>();
        if (nftManager != null)
        {
            nftManager.ForceRefreshAfterMatch(localPlayerScore);
        }
    }
    
    public void SubmitScoreToFirebase(int score, int bonus)
    {
        string walletAddress = GetWalletAddress();
        
        if (string.IsNullOrEmpty(walletAddress) || walletAddress == "anonymous")
        {
            Debug.LogWarning("[SCOREMANAGER] Pas de wallet, pas de soumission");
            return;
        }
        
#if UNITY_WEBGL && !UNITY_EDITOR
        SubmitScoreJS(score.ToString(), bonus.ToString(), walletAddress, GetCurrentMatchId());
#else
        // Score simulé en mode Editor
#endif
    }
    
    // Callbacks du serveur sécurisé
    [UnityEngine.Scripting.Preserve]
    public void OnScoreSubmitted(string newScore)
    {
        if (int.TryParse(newScore, out int score))
        {
            // Score confirmé par le serveur
            UpdateScoreDisplay(score);
        }
    }
    
    [UnityEngine.Scripting.Preserve]
    public void OnScoreRejected(string error)
    {
        Debug.LogWarning($"[SCOREMANAGER] ⚠️ Score rejeté par le serveur: {error}");
        // Optionnel: afficher un message à l'utilisateur
    }
    
    [UnityEngine.Scripting.Preserve]
    public void OnScoreFailed(string error)
    {
        Debug.LogError($"[SCOREMANAGER] ❌ Échec soumission score: {error}");
        // Optionnel: afficher un message d'erreur
    }
    
    private void UpdateScoreDisplay(int score)
    {
        // Mise à jour de l'UI si nécessaire
        if (LobbyUI.Instance != null)
        {
            LobbyUI.Instance.UpdatePlayerList();
        }
    }
    
    private string GetWalletAddress()
    {
        // Utiliser l'adresse AppKit stockée dans PlayerPrefs (comme dans ChogTanksNFTManager)
        string appKitAddress = PlayerPrefs.GetString("walletAddress", "");
        if (!string.IsNullOrEmpty(appKitAddress))
        {
            Debug.Log($"[ANTI-SELF-KILL] Adresse AppKit depuis PlayerPrefs: {appKitAddress}");
            return appKitAddress;
        }
        
        // Fallback: récupérer directement depuis AppKit si pas dans PlayerPrefs
        try
        {
            if (Reown.AppKit.Unity.AppKit.IsInitialized && 
                Reown.AppKit.Unity.AppKit.IsAccountConnected && 
                Reown.AppKit.Unity.AppKit.Account != null)
            {
                string directAppKitAddress = Reown.AppKit.Unity.AppKit.Account.Address;
                if (!string.IsNullOrEmpty(directAppKitAddress))
                {
                    Debug.Log($"[ANTI-SELF-KILL] Adresse AppKit directe: {directAppKitAddress}");
                    return directAppKitAddress;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ANTI-SELF-KILL] Erreur récupération AppKit: {ex.Message}");
        }
        
        Debug.LogWarning($"[ANTI-SELF-KILL] Aucune adresse AppKit trouvée");
        return "anonymous";
    }

    private string GetCurrentMatchId()
    {
        // MatchId simple basé sur l'heure et l'ActorNumber local pour corréler côté serveur
        string actor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber.ToString() : "0";
        return $"match_{actor}_{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }
    
    
    private void SyncScores()
    {
        if (!PhotonNetwork.IsMasterClient || matchEnded || !PhotonNetwork.InRoom) return;
        
        List<object> scoreList = new List<object>();
        foreach (var pair in playerScores)
        {
            scoreList.Add(pair.Key);
            scoreList.Add(pair.Value);
        }
        
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(4, scoreList.ToArray(), options, SendOptions.SendReliable);
    }
    
    private void SyncMatchTime(float timeLeft)
    {
        if (!PhotonNetwork.IsMasterClient || matchEnded || !PhotonNetwork.InRoom) return;
        
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(SYNC_TIMER_EVENT, timeLeft, options, SendOptions.SendReliable);
    }
    
    private IEnumerator FallbackShowWinner(int winnerActorNumber, string winnerName, int highestScore)
    {
        yield return new WaitForSeconds(2f);
        
        GameObject[] gameOverUIs = GameObject.FindGameObjectsWithTag("GameOverUI");
        if (gameOverUIs.Length == 0)
        {
            PhotonLauncher launcher = FindObjectOfType<PhotonLauncher>();
            if (launcher != null && launcher.photonView != null)
            {
                launcher.photonView.RPC("ShowWinnerToAllRPC", RpcTarget.All, winnerName, winnerActorNumber);
            }
        }
    }
    
    public void OnEvent(EventData photonEvent)
    {
        byte eventCode = photonEvent.Code;
        
        if (eventCode == SCORE_UPDATE_EVENT)
        {
            object[] data = (object[])photonEvent.CustomData;
            int actorNumber = (int)data[0];
            int score = (int)data[1];
            
            HandleScoreUpdate(actorNumber, score);
        }
        else if (eventCode == MATCH_END_EVENT)
        {
            object[] data = (object[])photonEvent.CustomData;
            int winnerActorNumber = (int)data[0];
            string winnerName = (string)data[1];
            int highestScore = (int)data[2];
            
            // Aligne le cache local avec le score final (+1)
            playerScores[winnerActorNumber] = highestScore;
            
            // Mise à jour visuelle immédiate de la player list
            if (LobbyUI.Instance != null)
            {
                LobbyUI.Instance.UpdatePlayerList();
            }
            
            ShowWinnerAndSubmitScores(winnerActorNumber, winnerName, highestScore);
        }
        else if (eventCode == 3) 
        {
            object[] data = (object[])photonEvent.CustomData;
            string actorIdStr = (string)data[0];
            string walletAddress = (string)data[1];
            
            Debug.Log($"[ANTI-SELF-KILL] Event 3 reçu: ActorNumber={actorIdStr}, Wallet={walletAddress}");
            playerWallets[actorIdStr] = walletAddress;
            Debug.Log($"[ANTI-SELF-KILL] playerWallets mis à jour. Contenu: {string.Join(", ", playerWallets.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
        else if (eventCode == 4) 
        {
            object[] data = (object[])photonEvent.CustomData;
            
            playerScores.Clear();
            for (int i = 0; i < data.Length; i += 2)
            {
                int actorNumber = (int)data[i];
                int score = (int)data[i + 1];
                playerScores[actorNumber] = score;
            }
            
            if (LobbyUI.Instance != null)
            {
                LobbyUI.Instance.UpdatePlayerList();
            }
        }
        else if (eventCode == SYNC_TIMER_EVENT)
        {
            float timeRemaining = (float)photonEvent.CustomData;
            matchStartTime = Time.time - (ROOM_LIFETIME - timeRemaining);
            
            if (LobbyUI.Instance != null)
            {
                LobbyUI.Instance.UpdateTimer(Mathf.Max(0, (int)timeRemaining));
            }
        }
    }
    
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        
        if (newMasterClient.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            if (!matchEnded)
            {
                SyncMatchTime(ROOM_LIFETIME - (Time.time - matchStartTime));
            }
        }
    }
    
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (!playerScores.ContainsKey(newPlayer.ActorNumber))
        {
            playerScores[newPlayer.ActorNumber] = 0;
        }
        
        if (PhotonNetwork.IsMasterClient)
        {
            SyncScores();
            
            float timeLeft = ROOM_LIFETIME - (Time.time - matchStartTime);
            SyncMatchTime(timeLeft);
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps == null) return;
        if (changedProps.ContainsKey("wallet"))
        {
            string wallet = changedProps["wallet"] as string;
            if (!string.IsNullOrEmpty(wallet))
            {
                playerWallets[targetPlayer.ActorNumber.ToString()] = wallet;
                Debug.Log($"[ANTI-SELF-KILL] Properties update: ActorNumber={targetPlayer.ActorNumber}, Wallet={wallet}");
            }
        }
        if (changedProps.ContainsKey("privyUsername"))
        {
            string uname = changedProps["privyUsername"] as string;
            if (!string.IsNullOrEmpty(uname))
            {
                playerPrivyUsernames[targetPlayer.ActorNumber.ToString()] = uname;
                Debug.Log($"[ANTI-SELF-KILL] Properties update: ActorNumber={targetPlayer.ActorNumber}, PrivyUsername={uname}");
            }
        }
    }

    private void TryPropagatePrivyUsername()
    {
        if (privyUsernamePropagated) return;
        string privyUsername = GetLocalPrivyUsername();
        if (!string.IsNullOrEmpty(privyUsername) && PhotonNetwork.LocalPlayer != null)
        {
            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            playerPrivyUsernames[actorNumber.ToString()] = privyUsername;
            var props2 = new ExitGames.Client.Photon.Hashtable { { "privyUsername", privyUsername } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props2);
            Debug.Log($"[ANTI-SELF-KILL] Propagation privyUsername via CustomProperties: ActorNumber={actorNumber}, Username={privyUsername}");
            privyUsernamePropagated = true;
        }
    }

    private IEnumerator DelayPropagatePrivyUsername()
    {
        // Retente quelques fois (ex: si MonadGamesIDManager initialise après l'entrée dans la room)
        for (int i = 0; i < 5 && !privyUsernamePropagated; i++)
        {
            yield return new WaitForSeconds(1f);
            TryPropagatePrivyUsername();
        }
    }
    
    public override void OnLeftRoom()
    {
        ResetManager();
    }
    
    public override void OnDisconnected(DisconnectCause cause)
    {
        ResetManager();
    }
    
    public Dictionary<int, int> GetPlayerScores()
    {
        return playerScores;
    }
    
    public int GetPlayerScore(int actorNumber)
    {
        return playerScores.ContainsKey(actorNumber) ? playerScores[actorNumber] : 0;
    }
    
    public bool IsMatchEnded()
    {
    bool endedTime = (Time.time - matchStartTime) >= ROOM_LIFETIME;
    bool result = matchEnded || endedTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    if (result && !matchEnded && endedTime && startCoroutineLaunched)
    {
        // Timer naturally elapsed; ok.
    }
#endif
    return result;
    }
}