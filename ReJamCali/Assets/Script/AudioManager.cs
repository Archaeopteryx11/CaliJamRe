using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class AudioManager : MonoBehaviour
{
    public const string TagCinematica = "Cinematica";

    static AudioManager instancia;
    AudioSource musica, sfx;
    AudioClip musicaActual;      // la que debería sonar (se recupera al terminar una cinemática)
    bool enCinematica;

    public static float VolumenMusica = 0.4f;
    public static float VolumenSfx = 1f;

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

        SceneManager.sceneLoaded += AlCargarEscena;
        EnlazarCinematicas();                         // la escena actual
    }

    void OnDestroy() { SceneManager.sceneLoaded -= AlCargarEscena; }

    void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        enCinematica = false;
        EnlazarCinematicas();                         // cada escena nueva
    }

    // ---------------- Música ----------------
    /// No reinicia la canción si ya está sonando (así R no corta la música del nivel).
    public static void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        var a = Instancia;
        a.musicaActual = clip;
        if (a.enCinematica) return;                   // durante la cinemática solo se recuerda cuál toca
        if (a.musica.clip == clip && a.musica.isPlaying) return;

        a.musica.clip = clip;
        a.musica.volume = VolumenMusica;
        a.musica.Play();
    }

    public static void DetenerMusica()
    {
        var a = Instancia;
        a.musica.Stop();
        a.musica.clip = null;
        a.musicaActual = null;
    }

    public static void SetVolumenMusica(float v)
    {
        VolumenMusica = Mathf.Clamp01(v);
        Instancia.musica.volume = VolumenMusica;
    }

    // ---------------- Efectos ----------------
    public static void Sfx(AudioClip clip, float volumen = 1f)
    {
        if (clip == null) return;
        Instancia.sfx.PlayOneShot(clip, volumen * VolumenSfx);
    }

    // ---------------- Cinemáticas ----------------
    /// Por si quieres llamarlas a mano (UnityEvent, señal de Timeline, tu propio código).
    public static void IniciarCinematica() { Instancia.InicioCine(); }
    public static void TerminarCinematica() { Instancia.FinCine(); }

    void InicioCine()
    {
        if (enCinematica) return;
        enCinematica = true;
        musica.Stop();
        musica.clip = null;                           // musicaActual se conserva para volver a ponerla
    }

    void FinCine()
    {
        if (!enCinematica) return;
        enCinematica = false;
        if (musicaActual != null) PlayMusic(musicaActual);   // arranca de nuevo desde el principio
    }

    /// Detecta solo los VideoPlayer y PlayableDirector (Timeline) cuyo objeto tenga el tag "Cinematica".
    void EnlazarCinematicas()
    {
        foreach (var v in FindObjectsByType<VideoPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!EsCinematica(v.gameObject)) continue;
            v.started += (x) => InicioCine();
            v.loopPointReached += (x) => FinCine();
        }

        foreach (var d in FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!EsCinematica(d.gameObject)) continue;
            d.played += (x) => InicioCine();
            d.stopped += (x) => FinCine();
        }
    }

    static bool EsCinematica(GameObject go)
    {
        try { return go.CompareTag(TagCinematica); }
        catch (UnityException) { return false; }      // el tag no existe todavía
    }
}