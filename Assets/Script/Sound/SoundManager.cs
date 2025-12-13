using UnityEngine;

public enum SoundType
{
    musicBG,
    moveCard,
    pickCard,
    dealCard,
}

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    [SerializeField] private AudioClip[] soundList;
    private static SoundManager instance;
    private AudioSource audioSource;
    private AudioSource sfxSource;  

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        audioSource = GetComponent<AudioSource>();

        audioSource.loop = true;
    }

    // --- HÀM PHÁT NHẠC NỀN (BGM) ---
    public static void PlayMusic(SoundType sound, float volume = 1)
    {
        if (instance == null) return;

        AudioClip clipToPlay = instance.soundList[(int)sound];

        if (instance.audioSource.clip == clipToPlay && instance.audioSource.isPlaying)
        {
            return;
        }

        // Nếu khác bài, hoặc chưa hát -> Bắt đầu phát bài mới
        instance.audioSource.clip = clipToPlay;
        instance.audioSource.volume = volume;
        instance.audioSource.loop = true; // Bắt buộc loop
        instance.audioSource.Play();
    }

    public static void PlaySFX(SoundType sound, float volume = 1)
    {
        if (instance == null) return;

        AudioClip clip = instance.soundList[(int)sound];
        instance.audioSource.PlayOneShot(clip, volume);
    }
}