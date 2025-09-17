using UnityEngine;
using UnityEngine.UI;

public class SettingsPanelManager : MonoBehaviour
{
    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public Button settingsButton;
    
    [Tooltip("Boutons additionnels qui basculent le panneau comme le Settings Button")]
    public Button[] additionalSettingsButtons;
    
    [Header("Panel Visibility")]
    [Tooltip("Si décoché, le panneau sera caché au démarrage")]
    public bool showPanelAtStartup = false;
    
    [Header("Close Buttons")]
    [Tooltip("Bouton principal pour fermer le panel")]
    public Button closeButton;
    
    [Tooltip("Boutons additionnels pour fermer le panel")]
    public Button[] additionalCloseButtons;
    
    [Header("Powerup SFX Volume")]
    [Tooltip("Slider pour régler le volume des SFX pickup powerup")]
    public Slider powerupVolumeSlider;
    
    [Tooltip("Texte pour afficher la valeur du volume")]
    public Text powerupVolumeText;
    
    void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(showPanelAtStartup);
            
        if (settingsButton != null)
            settingsButton.onClick.AddListener(ToggleSettingsPanel);
        
        // Boutons additionnels qui partagent le même Toggle
        if (additionalSettingsButtons != null)
        {
            foreach (Button button in additionalSettingsButtons)
            {
                if (button != null)
                    button.onClick.AddListener(ToggleSettingsPanel);
            }
        }
            
        // Configuration du bouton de fermeture principal
        if (closeButton != null)
            closeButton.onClick.AddListener(HideSettingsPanel);
            
        // Configuration des boutons de fermeture additionnels
        if (additionalCloseButtons != null)
        {
            foreach (Button button in additionalCloseButtons)
            {
                if (button != null)
                    button.onClick.AddListener(HideSettingsPanel);
            }
        }
        
    }
    
    public void ToggleSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(!settingsPanel.activeSelf);
        }
    }
    
    public void HideSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
}