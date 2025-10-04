using UnityEngine;
using System.Linq;

/// <summary>
/// Gestionnaire central pour distribuer les messages d'auto-mint à tous les NFTDisplayPanel actifs
/// </summary>
public class AutoMintManager : MonoBehaviour
{
    private static AutoMintManager _instance;
    public static AutoMintManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AutoMintManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("AutoMintManager");
                    _instance = go.AddComponent<AutoMintManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Méthode appelée par JavaScript pour distribuer les messages d'auto-mint
    /// </summary>
    /// <param name="jsonResponse">Réponse JSON de l'auto-mint check</param>
    public void OnHasMintedNFTChecked(string jsonResponse)
    {
        Debug.Log($"[AUTO-MINT-MANAGER] Distributing mint check to all active panels: {jsonResponse}");

        // Trouve TOUS les NFTDisplayPanel dans la scène (même inactifs dans la hiérarchie)
        NFTDisplayPanel[] allPanels = FindObjectsOfType<NFTDisplayPanel>(includeInactive: true);
        
        Debug.Log($"[AUTO-MINT-MANAGER] Found {allPanels.Length} NFTDisplayPanel(s) in scene");

        foreach (var panel in allPanels)
        {
            // Ne distribuer qu'aux panels dont le GameObject parent est actif
            if (panel.gameObject.activeInHierarchy)
            {
                Debug.Log($"[AUTO-MINT-MANAGER] Forwarding to active panel: {panel.gameObject.name}");
                panel.OnHasMintedNFTChecked(jsonResponse);
            }
            else
            {
                Debug.Log($"[AUTO-MINT-MANAGER] Skipping inactive panel: {panel.gameObject.name}");
            }
        }
    }

    /// <summary>
    /// Méthode pour forcer l'auto-mint sur le panel actuellement visible
    /// </summary>
    public void TriggerAutoMintOnVisiblePanel()
    {
        NFTDisplayPanel[] allPanels = FindObjectsOfType<NFTDisplayPanel>(includeInactive: true);
        
        foreach (var panel in allPanels)
        {
            if (panel.gameObject.activeInHierarchy)
            {
                Debug.Log($"[AUTO-MINT-MANAGER] Triggering auto-mint on visible panel: {panel.gameObject.name}");
                // Appel direct à la méthode de mint (si elle est publique)
                // panel.TriggerAutoMint(); // À implémenter si besoin
                return;
            }
        }
        
        Debug.LogWarning("[AUTO-MINT-MANAGER] No active NFTDisplayPanel found for auto-mint");
    }
}
