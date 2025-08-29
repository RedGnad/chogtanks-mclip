using UnityEngine;
using TMPro;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using Photon.Realtime;

public class KillNotificationManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text killNotificationText;
    [SerializeField] private float notificationDuration = 3f; 
    
    private static KillNotificationManager _instance;
    public static KillNotificationManager Instance 
    { 
        get 
        {
            if (_instance == null)
                _instance = FindObjectOfType<KillNotificationManager>();
            return _instance;
        }
    }
    
    private Queue<string> notificationQueue = new Queue<string>();
    private bool isShowingNotification = false;
    private LobbyUI cachedLobbyUI;
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(this.gameObject);

        CacheLobbyUIReference();

        if (killNotificationText != null)
            killNotificationText.gameObject.SetActive(false);
    }

    private void CacheLobbyUIReference()
    {
        if (cachedLobbyUI == null)
        {
            cachedLobbyUI = FindObjectOfType<LobbyUI>();
        }
        
        if (killNotificationText == null && cachedLobbyUI != null)
        {
            killNotificationText = cachedLobbyUI.killFeedText;
        }
    }    private void Start()
    {
        CacheLobbyUIReference();
    }
    
    public void SetKillNotificationText(TMP_Text text)
    {
        killNotificationText = text;
    }
    
    public void ShowKillNotification(int killerActorNumber, int killedActorNumber)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("ShowKillNotificationRPC", RpcTarget.All, killerActorNumber, killedActorNumber);
        }
        else
        {
            ShowKillNotificationLocal(killerActorNumber, killedActorNumber);
        }
    }
    
    [PunRPC]
    private void ShowKillNotificationRPC(int killerActorNumber, int killedActorNumber)
    {
        ShowKillNotificationLocal(killerActorNumber, killedActorNumber);
    }
    
    private void ShowKillNotificationLocal(int killerActorNumber, int killedActorNumber)
    {
        string killerName = "Unknown";
        string killedName = "Unknown";
        
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == killerActorNumber)
            {
                killerName = string.IsNullOrEmpty(player.NickName) ? $"Player {killerActorNumber}" : player.NickName;
            }
            if (player.ActorNumber == killedActorNumber)
            {
                killedName = string.IsNullOrEmpty(player.NickName) ? $"Player {killedActorNumber}" : player.NickName;
            }
        }
        
        string notificationText = $"{killerName} shot {killedName}";
        
        // Jouer le son killfeed pour tous les joueurs
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayRandomKillFeedSoundLocal();
        }
        else
        {
            Debug.LogWarning("[KILLFEED] ⚠️ SFXManager.Instance est null, impossible de jouer le son killfeed");
        }
        
        notificationQueue.Enqueue(notificationText);
        if (!isShowingNotification)
        {
            StartCoroutine(ProcessNotificationQueue());
        }
    }
    
    private IEnumerator ProcessNotificationQueue()
    {
        isShowingNotification = true;
        
        if (killNotificationText == null)
        {
            CacheLobbyUIReference();
        }
        
        while (notificationQueue.Count > 0)
        {
            string currentNotification = notificationQueue.Dequeue();
            
            if (killNotificationText != null)
            {
                killNotificationText.gameObject.SetActive(true);
                killNotificationText.text = currentNotification;
                
                yield return new WaitForSeconds(notificationDuration);
                
                killNotificationText.gameObject.SetActive(false);
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        isShowingNotification = false;
    }
}
