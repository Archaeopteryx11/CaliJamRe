using UnityEngine;

public class AudioManager : MonoBehaviour
{
    static AudioManager instancia;
    AudioSource musica, sfx;

    public static float VolumenMusica = 0.4f;
    public static float VolumenSfx = 1f;
    bool musicaPausada;

    static AudioManager Instancia
    {
        get
        {
            if (instancia == null)
                new GameObject("AudioManager").AddComponent<AudioManager>();
            return instancia;
        }
    }

    void Awake()
    {
        if (instancia != null && instancia != this) { Destroy(gameObject); return; }
        instancia = this;
        DontDestroyOnLoad(gameObject);

        musica = gameObject.AddComponent<AudioSource>();
        musica.loop = true;
        musica.playOnAwake = false;
        musica.volume = VolumenMusica;

        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
    }

    /// No reinicia la canción si ya está sonando (así R no corta la música del nivel).
    public static void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        var a = Instancia;
        if (a.musica.clip == clip && (a.musica.isPlaying || a.musicaPausada)) return;
        a.musica.clip = clip;
        a.musica.volume = VolumenMusica;
        a.musicaPausada = false;
        a.musica.Play();
    }

    public static void Sfx(AudioClip clip, float volumen = 1f)
    {
        if (clip == null) return;
        Instancia.sfx.PlayOneShot(clip, volumen * VolumenSfx);
    }

    public static void SetVolumenMusica(float v)
    {
        VolumenMusica = Mathf.Clamp01(v);
        Instancia.musica.volume = VolumenMusica;
    }

    public static void PausarMusica()
    {
        var a = Instancia;
        if (!a.musica.isPlaying) return;
        a.musica.Pause();
        a.musicaPausada = true;
    }

    public static void ReanudarMusica()
    {
        var a = Instancia;
        if (!a.musicaPausada) return;
        a.musica.UnPause();
        a.musicaPausada = false;
    }

    public static void DetenerMusica()
    {
        var a = Instancia;
        a.musica.Stop();
        a.musica.clip = null;
        a.musicaPausada = false;
    }
}