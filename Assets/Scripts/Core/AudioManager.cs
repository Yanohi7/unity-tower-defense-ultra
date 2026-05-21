using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    // Manager that keeps menu music playing and handles button click sounds.
    // It is not destroyed when another scene loads and safely prevents NullReferenceException.

    private static AudioManager instance;

    public static AudioManager Instance
    {
        get
        {
            // If the instance doesn't exist yet, try to find it in the scene
            if (instance == null)
            {
                instance = FindFirstObjectByType<AudioManager>();

                // If it still doesn't exist (e.g., game started from a test scene), create it automatically
                if (instance == null)
                {
                    GameObject go = new GameObject("Runtime_AudioManager");
                    instance = go.AddComponent<AudioManager>();
                    Debug.LogWarning("AudioManager was automatically created because the game was started from a test scene, not the Main Menu.");
                }
            }
            return instance;
        }
        private set
        {
            instance = value;
        }
    }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip clickSound;

    [Header("Scene Rules")]
    [SerializeField] private string[] menuScenes;

    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        // If another AudioManager already exists, destroy this duplicate
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Keep this object alive when changing scenes
        DontDestroyOnLoad(gameObject);

        // Fallback check if AudioSources were not assigned in the inspector
        ValidateAudioSources();

        SceneManager.sceneLoaded += OnSceneLoaded;

        PlayMenuMusic();
    }

    // Called when this object is destroyed
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Method called whenever a new scene is loaded
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsMenuScene(scene.name))
        {
            PlayMenuMusic();
        }
        else
        {
            StopMusic();
        }
    }

    // Method to play menu music
    private void PlayMenuMusic()
    {
        if (musicSource == null || menuMusic == null)
            return;

        if (musicSource.clip == menuMusic && musicSource.isPlaying)
            return;

        musicSource.clip = menuMusic;
        musicSource.Play();
    }

    // Method to stop music
    private void StopMusic()
    {
        if (musicSource == null)
            return;

        musicSource.Stop();
    }

    // Method to play button click sound
    public void PlayClick()
    {
        if (sfxSource != null && clickSound != null)
        {
            // PlayOneShot allows sounds to overlap if clicked quickly
            sfxSource.PlayOneShot(clickSound);
        }
    }

    // Method to check if current scene should have menu music
    private bool IsMenuScene(string sceneName)
    {
        if (menuScenes == null)
            return false;

        foreach (string menuScene in menuScenes)
        {
            if (sceneName == menuScene)
                return true;
        }

        return false;
    }

    // Automatically setups AudioSources if they are missing in inspector
    private void ValidateAudioSources()
    {
        AudioSource[] sources = GetComponents<AudioSource>();

        // If fields are empty in Inspector, look for existing AudioSources on GameObject
        if (musicSource == null && sources.Length > 0) musicSource = sources[0];

        // If there is no second AudioSource for SFX, add one dynamically to prevent issues
        if (sfxSource == null)
        {
            sfxSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        }

        if (musicSource != null)
        {
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
        }
    }
}