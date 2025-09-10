using UnityEngine;
using UnityEngine.Audio;

public class VolumeController : MonoBehaviour
{
    public static VolumeController Instance { get; private set; }

    [Header("Audio Mixer")]
    public AudioMixer masterMixer; 

    public const string MUSIC_VOLUME_KEY = "MusicVolume";
    public const string SFX_VOLUME_KEY = "SfxVolume";

    private const string MUSIC_PREF_KEY = "MusicVolumePreference";
    private const string SFX_PREF_KEY = "SfxVolumePreference";

    // Centralised defaults (adjust here only)
    private const float DEFAULT_MUSIC_VOLUME = 0.33f;
    private const float DEFAULT_SFX_VOLUME = 0.33f; // Wanted lower than old 0.75

    // If earlier builds saved a louder default (e.g. 0.75) we can treat it as a legacy value
    private const float LEGACY_HIGH_SFX_THRESHOLD = 0.74f; // anything >= this assumed legacy default

    void Awake()
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

    void Start()
    {
        LoadVolumeSettings();
    }

    private void LoadVolumeSettings()
    {
        // Music
        float musicVolume = PlayerPrefs.HasKey(MUSIC_PREF_KEY)
            ? PlayerPrefs.GetFloat(MUSIC_PREF_KEY)
            : DEFAULT_MUSIC_VOLUME;
        SetMusicVolume(musicVolume, false);

        // SFX (migration: if an existing saved value looks like the old loud default, override once)
        float sfxVolume;
        if (!PlayerPrefs.HasKey(SFX_PREF_KEY))
        {
            sfxVolume = DEFAULT_SFX_VOLUME;
        }
        else
        {
            float stored = PlayerPrefs.GetFloat(SFX_PREF_KEY);
            sfxVolume = (stored >= LEGACY_HIGH_SFX_THRESHOLD) ? DEFAULT_SFX_VOLUME : stored;
            if (stored != sfxVolume)
            {
                PlayerPrefs.SetFloat(SFX_PREF_KEY, sfxVolume);
                PlayerPrefs.Save();
            }
        }
        SetSfxVolume(sfxVolume, false);
    }

    private float ConvertToDecibel(float volume)
    {
        return Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20;
    }

    public void SetMusicVolume(float volume, bool savePreference = true)
    {
        if (masterMixer == null) return;
        masterMixer.SetFloat(MUSIC_VOLUME_KEY, ConvertToDecibel(volume));
        if (savePreference)
        {
            PlayerPrefs.SetFloat(MUSIC_PREF_KEY, volume);
            PlayerPrefs.Save();
        }
    }

    public void SetSfxVolume(float volume, bool savePreference = true)
    {
        if (masterMixer == null) return;
        masterMixer.SetFloat(SFX_VOLUME_KEY, ConvertToDecibel(volume));
        if (savePreference)
        {
            PlayerPrefs.SetFloat(SFX_PREF_KEY, volume);
            PlayerPrefs.Save();
        }
    }

    public float GetMusicVolume()
    {
        return PlayerPrefs.GetFloat(MUSIC_PREF_KEY, DEFAULT_MUSIC_VOLUME);
    }

    public float GetSfxVolume()
    {
        return PlayerPrefs.GetFloat(SFX_PREF_KEY, DEFAULT_SFX_VOLUME);
    }

    // Optional: call this from a settings UI button to force reset.
    public void ResetToDefaultVolumes()
    {
        SetMusicVolume(DEFAULT_MUSIC_VOLUME, true);
        SetSfxVolume(DEFAULT_SFX_VOLUME, true);
    }
}
