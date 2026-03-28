using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [Header ("Sounds")]
    public AudioClip CardClip;
    public AudioClip LoseClip;
  public AudioClip WinClip;
public AudioSource sc;
    public static SoundManager Instance { get; private set; }

    private void Awake()
    {
        Time.timeScale=1f;
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
    public void PlaySoundclip(AudioClip clip, bool IsOk ,AudioSource source)
    {
        source.clip = clip;

        if (IsOk)
        {
            source.Play();
        }
        else
        {
            source.Stop();
        }
    }
    public void PlaySoundclipOneShot(AudioClip clip,AudioSource source)
    {
        source.clip = clip;
        source.PlayOneShot(clip);



    }

      public void PlaySoundcliponPlace(AudioClip clip,AudioSource source)
    {
        source.clip = clip;
        AudioSource.PlayClipAtPoint(clip, transform.position);



    }
}
