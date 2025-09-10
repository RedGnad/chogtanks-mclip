using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Linq; 
using System.Collections.Generic; 
using ExitGames.Client.Photon; // Hashtable for custom room props

public class PhotonLauncher : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] private GameObject gameOverUIPrefab;

    [Header("Gestion de déconnexion")]
    [SerializeField] private float autoReconnectDelay = 2f;
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private GameObject reconnectionNotificationPrefab;
    
    private bool isWaitingForReconnection = false;
    private bool wasDisconnected = false;

    private List<RoomInfo> cachedRoomList = new List<RoomInfo>();

    [PunRPC]
    public void RestartMatchSoftRPC()
    {
        foreach (var ui in GameObject.FindGameObjectsWithTag("GameOverUI"))
        {
            Destroy(ui);
        }

        var minimapCam = FindObjectOfType<MinimapCamera>();
        if (minimapCam != null)
        {
            minimapCam.ForceReset();
        }

        TankHealth2D myTank = null;
        foreach (var t in FindObjectsOfType<TankHealth2D>())
        {
            if (t.photonView.IsMine)
            {
                myTank = t;
                break;
            }
        }
        if (myTank != null)
        {
            PhotonNetwork.Destroy(myTank.gameObject);
        }

        var spawner = FindObjectOfType<PhotonTankSpawner>();
        if (spawner != null)
        {
            spawner.SpawnTank();
        }
        
        soundAlreadyPlayed = false;
    }

    public void RespawnTank()
    {
        TankHealth2D myTank = null;
        foreach (var t in FindObjectsOfType<TankHealth2D>())
        {
            if (t.photonView.IsMine)
            {
                myTank = t;
                break;
            }
        }
        if (myTank != null)
        {
            PhotonNetwork.Destroy(myTank.gameObject);
        }

        var spawner = FindObjectOfType<PhotonTankSpawner>();
        if (spawner != null)
        {
            spawner.SpawnTank();
        }
        
        soundAlreadyPlayed = false;
    }

    [Header("Winner Sound")]
    [SerializeField] private AudioClip[] winnerSoundClips;
    private static bool soundAlreadyPlayed = false;
    
    [PunRPC]
    public void ShowWinnerToAllRPC(string winnerName, int winnerActorNumber)
    {
        PlayWinnerSoundLocal();
        
        bool isWinner = PhotonNetwork.LocalPlayer.ActorNumber == winnerActorNumber;
        
        GameObject prefabToUse = gameOverUIPrefab;
        if (prefabToUse == null)
        {
            var tankHealth = FindObjectOfType<TankHealth2D>();
            if (tankHealth != null)
            {
                var field = typeof(TankHealth2D).GetField("gameOverUIPrefab", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    prefabToUse = field.GetValue(tankHealth) as GameObject;
                }
            }
        }
        
        Camera mainCam = Camera.main;
        if (mainCam != null && prefabToUse != null)
        {
            GameObject uiInstance = Instantiate(prefabToUse, mainCam.transform);
            RectTransform rt = uiInstance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localPosition = new Vector3(0f, 0f, 1f);
                rt.localRotation = Quaternion.identity;
                float baseScale = 1f;
                float dist = Vector3.Distance(mainCam.transform.position, rt.position);
                float scaleFactor = baseScale * (dist / mainCam.orthographicSize) * 0.1f;
                rt.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            }
            
            var controller = uiInstance.GetComponent<GameOverUIController>();
            if (controller != null)
            {
                if (isWinner)
                {
                    controller.ShowWin(winnerName);
                }
                else
                {
                    controller.ShowWinner(winnerName);
                }

                // Ne lancer la coroutine de leave que si on est encore dans la room (cas multi).
                if (PhotonNetwork.InRoom)
                {
                    StartCoroutine(ReturnToLobbyAfterDelay(6));
                }
            }
            // Soft restart (respawn in same room before real leave) provoquait des états incohérents
            // Désactivé par défaut; réactiver seulement si on implémente un vrai rematch in-room.
            if (enableSoftRestartFlow)
            {
                StartCoroutine(AutoDestroyAndRestart(uiInstance));
            }
        }
    }

    private void PlayWinnerSoundLocal()
    {
        
        if (soundAlreadyPlayed)
        {
            return;
        }
        
        if (winnerSoundClips == null) 
        {
            return;
        }
        
        if (winnerSoundClips.Length == 0)
        {
            return;
        }
        
        soundAlreadyPlayed = true;
        
        foreach (AudioClip clip in winnerSoundClips)
        {
            if (clip != null)
            {
                AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position);
            }
            else
            {
                //Debug.LogWarning("[PHOTON-WINNER-SOUND] Clip audio null détecté !");
            }
        }
        
    }

    private System.Collections.IEnumerator ReturnToLobbyAfterDelay(int seconds)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameOver();
        }
        
        yield return new WaitForSeconds(seconds);
        // Nouvelle boucle: on quitte réellement la room pour éviter ré-usage d'une room fermée / état sale
        if (PhotonNetwork.InRoom)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[MATCH LOOP] Leaving room after game over");
#endif
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            // Si déjà hors room (rare): fallback UI
            var lobbyUI = FindObjectOfType<LobbyUI>();
            if (lobbyUI != null)
            {
                lobbyUI.OnBackToLobby();
            }
        }
    }
    
    private System.Collections.IEnumerator AutoDestroyAndRestart(GameObject uiInstance)
    {
        yield return new WaitForSeconds(3f);
        if (uiInstance != null)
        {
            Destroy(uiInstance);
        }
        CallRestartMatchSoft();
    }

    public static void CallRestartMatchSoft()
    {
        var launcher = FindObjectOfType<PhotonLauncher>();
        if (launcher != null)
        {
            if (launcher.photonView != null)
            {
                launcher.photonView.RPC("RestartMatchSoftRPC", RpcTarget.All);
            }
            else
            {
                Debug.LogError("[PhotonLauncher] PhotonView manquant sur PhotonLauncher !");
            }
        }
        else
        {
            Debug.LogError("[PhotonLauncher] Impossible de trouver PhotonLauncher pour le reset soft!");
        }
    }

    public bool isConnectedAndReady = false;

    [Header("Room Settings")]
    public string roomName = "";
    public byte maxPlayers = 2; // Reduced for testing auto public room creation cascade

    [Header("Match Flow (Debug)")]
    [Tooltip("Réactive l'ancien flux de soft restart (respawn dans la room avant de la quitter). Désactiver pour stabilité.")]
    public bool enableSoftRestartFlow = false;

    public LobbyUI lobbyUI;

    private static readonly string chars = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
    private System.Random rng = new System.Random();

    public string GenerateRoomCode()
    {
        char[] code = new char[4];
        for (int i = 0; i < 4; i++)
        {
            code[i] = chars[rng.Next(chars.Length)];
        }
        return new string(code);
    }

    // ===== Auto-room matching (quick play) =====
    private const string ROOM_PREFIX = "BRWL-"; // interne
    private const int CREATE_RETRY_MAX = 2;
    private int createRetryCount = 0;
    private System.Random roomRng = new System.Random();
    private bool pendingDelayedDecision = false;
    private float lastRoomListUpdateRealtime = -999f;
    private bool lobbyReady = false; // true after first lobby room list
    private bool queuedQuickPlay = false; // user pressed quick play before lobby ready
    // --- New JoinRandom-based quick play state ---
    private int randomJoinAttempts = 0; // 0 initial, 1 retry
    private bool quickPlayInProgress = false;
    private const string ROOM_PROP_MODE_KEY = "mode";
    private const string ROOM_PROP_MODE_VALUE_PUBLIC = "public";
    private const string ROOM_PROP_ENDED_KEY = "ended"; // 0 = active, 1 = finished

    private string GenerateMatchRoomName()
    {
        string timePart = System.DateTime.UtcNow.ToString("HHmmss");
        string randPart = roomRng.Next(0, 4095).ToString("X3");
        return ROOM_PREFIX + timePart + "-" + randPart;
    }

    // Replaced old room-list based selection with JoinRandom strategy
    private void StartQuickPlay()
    {
    if (quickPlayInProgress)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[MATCHMAKER] QuickPlay already in progress");
#endif
        return;
    }
    if (!PhotonNetwork.IsConnectedAndReady)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[MATCHMAKER] Not connected yet – ignoring quick play");
#endif
        return;
    }
    if (PhotonNetwork.InRoom)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[MATCHMAKER] Already in a room -> no action");
#endif
        return;
    }
    quickPlayInProgress = true;
    randomJoinAttempts = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log("[MATCHMAKER] QuickPlay start -> JoinRandom attempt #0");
#endif
    AttemptJoinRandom();
    }

    private void AttemptJoinRandom()
    {
    Hashtable expected = new Hashtable { { ROOM_PROP_MODE_KEY, ROOM_PROP_MODE_VALUE_PUBLIC }, { ROOM_PROP_ENDED_KEY, 0 } };
    PhotonNetwork.JoinRandomRoom(expected, maxPlayers);
    }

    private System.Collections.IEnumerator RetryJoinRandomWithJitter()
    {
    float wait = UnityEngine.Random.Range(0.05f, 0.15f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[MATCHMAKER] Scheduling jitter retry in {wait:F2}s");
#endif
    yield return new WaitForSeconds(wait);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log("[MATCHMAKER] JoinRandom attempt #1");
#endif
    AttemptJoinRandom();
    }

    private void CreatePublicMatchRoom()
    {
    string name = GenerateMatchRoomName();
    roomName = name;
        RoomOptions options = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsVisible = true,
            IsOpen = true,
            CleanupCacheOnLeave = true,
            EmptyRoomTtl = 0,
            CustomRoomProperties = new Hashtable {
                { ROOM_PROP_MODE_KEY, ROOM_PROP_MODE_VALUE_PUBLIC },
                { ROOM_PROP_ENDED_KEY, 0 }
            },
            CustomRoomPropertiesForLobby = new string[] { ROOM_PROP_MODE_KEY, ROOM_PROP_ENDED_KEY }
        };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[MATCHMAKER] Creating new public room {name}");
#endif
    PhotonNetwork.CreateRoom(name, options, TypedLobby.Default);
    }

    // Old CreateNewRoom removed (replaced by CreatePublicMatchRoom)

    // Removed DelayedRoomDecision (legacy room list strategy)

    public void CreatePrivateRoom()
    {
        roomName = GenerateRoomCode();
        RoomOptions options = new RoomOptions { MaxPlayers = maxPlayers, IsVisible = true, IsOpen = true };
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
    }

    [Header("Admin System")]
    [SerializeField] private string[] adminWallets = { 
        " " 
    };
    
    public void JoinRoomByCode(string code)
    {
        roomName = code.ToUpper();
        
        if (IsAdminWallet(PlayerSession.WalletAddress))
        {
            PhotonNetwork.JoinRoom(roomName);
        }
        else
        {
            PhotonNetwork.JoinRoom(roomName);
        }
    }
    
    private bool IsAdminWallet(string walletAddress)
    {
        if (string.IsNullOrEmpty(walletAddress)) return false;
        
        foreach (string adminWallet in adminWallets)
        {
            if (walletAddress.Equals(adminWallet, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
    
    [PunRPC]
    private void RequestAdminAccess(string adminWallet)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        
        if (!IsAdminWallet(adminWallet))
        {
            return;
        }
        
        Player playerToKick = null;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            string playerWallet = player.CustomProperties.ContainsKey("wallet") ? 
                player.CustomProperties["wallet"].ToString() : "";
            
            if (!IsAdminWallet(playerWallet))
            {
                playerToKick = player;
            }
        }
        
        if (playerToKick != null)
        {
            PhotonNetwork.CloseConnection(playerToKick);
            
            photonView.RPC("NotifyAdminCanJoin", RpcTarget.All, adminWallet);
        }
        else
        {
            //Debug.LogWarning("[ADMIN] No non-admin players found to kick!");
        }
    }
    
    [PunRPC]
    private void NotifyAdminCanJoin(string adminWallet)
    {
        if (PlayerSession.WalletAddress == adminWallet)
        {
            StartCoroutine(DelayedJoinRoom());
        }
    }
    
    private System.Collections.IEnumerator DelayedJoinRoom()
    {
        yield return new WaitForSeconds(1f); 
        PhotonNetwork.JoinRoom(roomName);
    }

    public void SetPlayerName(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            PhotonNetwork.NickName = "Newbie_" + Random.Range(100, 999);
        }
        else
        {
            // Cap à 11 caractères par sécurité
            if (playerName.Length > 11)
            {
                playerName = playerName.Substring(0, 11);
            }
            PhotonNetwork.NickName = playerName;
        }
    }

    private void Start()
    {
        if (GetComponent<PhotonView>() == null)
        {
            //Debug.LogError("[PhotonLauncher] PhotonView manquant sur l'objet PhotonLauncher ! Merci d'ajouter un PhotonView dans l'inspecteur AVANT de lancer la scène.");
        }
        
    // Slightly higher rates improve remote movement smoothness without big bandwidth cost
    PhotonNetwork.SendRate = 30;          // default is 20
    PhotonNetwork.SerializationRate = 15; // default is 10

        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 300000; 
            PhotonNetwork.NetworkingClient.LoadBalancingPeer.TimePingInterval = 5000; 
            PhotonNetwork.KeepAliveInBackground = 60; 
            PhotonNetwork.ConnectUsingSettings();
        }
    else { /* already connected */ }
        
        StartCoroutine(ConnectionHeartbeat());
    }
    // EnsureLobby removed for JoinRandom strategy (kept commented out)
    
    private System.Collections.IEnumerator ConnectionHeartbeat()
    {
        WaitForSeconds wait = new WaitForSeconds(20f); 
        
        while (true)
        {
            yield return wait;
            
            if (PhotonNetwork.IsConnected)
            {
                
                if (PhotonNetwork.InRoom)
                {
                    photonView.RPC("HeartbeatPing", RpcTarget.MasterClient);
                }
            }
        }
    }
    
    [PunRPC]
    private void HeartbeatPing()
    {
        // ...
    }

    public override void OnConnectedToMaster()
    {
        isConnectedAndReady = true;
        wasDisconnected = false; 
    lobbyReady = false; // kept for legacy features; not required for quick play
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log("[MATCHMAKER] ConnectedToMaster (no lobby dependency)");
#endif
        
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnPhotonReady();
        }
        else
        {
            //Debug.LogError("[PHOTON LAUNCHER] lobbyUI est null dans OnConnectedToMaster !");
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        
        wasDisconnected = true;
        isConnectedAndReady = false;
        
        ShowReconnectionNotification();
        
        StartCoroutine(ReturnToLobby());
    }
    
    private void ShowReconnectionNotification()
    {
        if (reconnectionNotificationPrefab != null)
        {
            GameObject notif = Instantiate(reconnectionNotificationPrefab);
            Destroy(notif, 3f);
        }
        else
        {
            //Debug.LogWarning("[PhotonLauncher] reconnectionNotificationPrefab non assigné");
        }
    }
    
    private System.Collections.IEnumerator ReturnToLobby()
    {
        yield return new WaitForSeconds(autoReconnectDelay);
        
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
        
        // Cleanup enemies when disconnected
        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.CleanupAllEnemies();
        }
        
        LobbyUI lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnBackToLobby();
        }
        else
        {
            //Debug.LogWarning("[PHOTON] LobbyUI non trouvé pour le retour au lobby après déconnexion");
        }
    }

    public override void OnJoinedRoom()
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnJoinedRoomUI(PhotonNetwork.CurrentRoom.Name);
        }
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isGameOver = false;
        }
        
        soundAlreadyPlayed = false;
        
    // IMPORTANT: Ne plus appeler ResetManager ici pour ne pas annuler StartMatch() lancé par ScoreManager.OnJoinedRoom()
    // (Double reset supprimait la coroutine du timer => timer figé à 0 et plus de respawn)
    // ScoreManager gère maintenant seul son cycle join/reset.
        
        var spawner = FindObjectOfType<PhotonTankSpawner>();
        if (spawner != null)
        {
            spawner.SpawnTank();
        }
        else
        {
            //Debug.LogError("[PhotonLauncher] PhotonTankSpawner non trouvé dans la scène !");
        }
        // Nettoyer toute ancienne UI de fin de match éventuellement restée
        foreach (var ui in GameObject.FindGameObjectsWithTag("GameOverUI"))
        {
            GameObject.Destroy(ui);
        }
        // Reset matchmaking state allowing future quick plays
        quickPlayInProgress = false;
        randomJoinAttempts = 0;
    }

    public override void OnLeftRoom()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[MATCH LOOP] OnLeftRoom -> returning to lobby UI");
#endif
        quickPlayInProgress = false;
        randomJoinAttempts = 0;
        var lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnBackToLobby();
            // Optionally re-enable play button if it was disabled
            lobbyUI.EnablePlayButton(); // Requires method; add no-op if absent.
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (returnCode == 32765 && IsAdminWallet(PlayerSession.WalletAddress))
        {
            photonView.RPC("RequestAdminAccess", RpcTarget.MasterClient, PlayerSession.WalletAddress);
            return;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[MATCHMAKER] JoinRoomFailed code={returnCode} msg={message}");
#endif
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.OnJoinRoomFailedUI();
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.HideWaitingForPlayerTextIfRoomFull();
            lobbyUI.UpdatePlayerList();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null)
        {
            lobbyUI.ShowWaitingForPlayerTextIfNotFull();
            lobbyUI.UpdatePlayerList();
        }
    }

    public void JoinRandomPublicRoom()
    {
        StartQuickPlay();
    }
    
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[MATCHMAKER] JoinRandomFailed attempt={randomJoinAttempts} code={returnCode} msg={message}");
#endif
        if (randomJoinAttempts == 0 && returnCode == 32760) // No match found first try
        {
            randomJoinAttempts = 1;
            StartCoroutine(RetryJoinRandomWithJitter());
            return;
        }
        CreatePublicMatchRoom();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        cachedRoomList = roomList; // informational only now
        lastRoomListUpdateRealtime = Time.realtimeSinceStartup;
        if (!lobbyReady)
        {
            lobbyReady = true; // legacy flag, may be reused for UI
        }
    }


    public void JoinOrCreatePublicRoom()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[MATCHMAKER] QuickPlay requested");
#endif
        StartQuickPlay();
    }
}