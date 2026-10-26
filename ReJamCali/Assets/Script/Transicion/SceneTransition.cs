using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Lanza un trigger en un Animator, espera a que termine la animación resultante
/// y cambia de escena.
///
/// Se puede activar de varias maneras:
///  - Desde un UnityEvent (botón, Timeline Signal, etc.): StartTransition() o StartTransition(string).
///  - Desde código: GetComponent&lt;SceneTransition&gt;().StartTransition();
///  - Automáticamente al iniciar la escena (triggerOnStart).
///  - Cuando el jugador entra en un trigger (triggerOnPlayerEnter).
/// </summary>
public class SceneTransition : MonoBehaviour
{
    [Header("Animación")]
    [Tooltip("Animator que tiene el parámetro Trigger. Si está vacío, se busca en este objeto.")]
    public Animator animator;
    [Tooltip("Nombre del parámetro Trigger del Animator.")]
    public string triggerName = "CambiarEscena";
    [Tooltip("Capa del Animator que se vigila.")]
    public int layerIndex = 0;
    [Tooltip("Segundos máximos de espera por seguridad (por si la animación hace loop o el trigger no tiene transición).")]
    public float maxWaitSeconds = 10f;

    [Header("Música")]
    [Tooltip("Baja el volumen de TODO el juego (AudioListener.volume): música, efectos, ambiente... sin asignar AudioSources.")]
    public bool fundidoGlobal = true;
    [Tooltip("Volumen general al que se restaura al empezar cada escena (AudioListener.volume se queda en 0 entre escenas). " +
             "Si más adelante haces un menú de opciones, pon aquí el valor guardado.")]
    [Range(0f, 1f)] public float volumenMaestro = 1f;
    [Tooltip("Solo si 'Fundido Global' está desactivado: AudioSources concretos que bajan a 0. Vacío = sin fundido.")]
    public AudioSource[] musica;
    [Tooltip("Segundos que dura el fundido. Empieza en cuanto se llama a la transición.")]
    public float fadeOutDuration = 1f;
    [Tooltip("Si el fundido dura más que la animación, espera a que termine antes de cambiar de escena.")]
    public bool esperarFundido = true;

    [Header("Escena")]
    [Tooltip("Nombre de la escena a cargar (debe estar en Build Settings). Si está vacío, carga la siguiente del Build.")]
    public string sceneName;

    [Header("Activación automática")]
    public bool triggerOnStart = false;
    public float startDelay = 0f;
    public bool triggerOnPlayerEnter = false;
    public string playerTag = "Player";

    [Header("Eventos")]
    public UnityEvent onTransitionStarted;
    public UnityEvent onAnimationFinished;

    bool running;
    bool fading;

    void Reset()
    {
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();

        // AudioListener.volume es estático y sobrevive al cambio de escena: se restaura al entrar.
        if (fundidoGlobal) AudioListener.volume = volumenMaestro;
    }

    void Start()
    {
        if (triggerOnStart)
            StartCoroutine(StartAfterDelay());
    }

    IEnumerator StartAfterDelay()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        StartTransition();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!triggerOnPlayerEnter) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;
        StartTransition();
    }

    // ---------------- API pública ----------------

    /// Usa la escena configurada en el inspector.
    [ContextMenu("Probar transición")]
    public void StartTransition()
    {
        StartTransition(sceneName);
    }

    /// Permite elegir la escena desde un UnityEvent (campo de texto) o desde código.
    public void StartTransition(string targetScene)
    {
        if (running) return;   // evita activarlo dos veces
        StartCoroutine(TransitionRoutine(targetScene));
    }

    // ---------------- Lógica ----------------

    IEnumerator TransitionRoutine(string targetScene)
    {
        running = true;
        onTransitionStarted?.Invoke();
        StartCoroutine(FadeOutMusic());   // corre en paralelo a la animación

        if (animator != null && HasTrigger(animator, triggerName))
        {
            int startState = animator.GetCurrentAnimatorStateInfo(layerIndex).shortNameHash;
            animator.SetTrigger(triggerName);

            float elapsed = 0f;

            // 1) Esperar a que el Animator empiece la transición hacia el nuevo estado.
            yield return null;
            while (elapsed < maxWaitSeconds &&
                   !animator.IsInTransition(layerIndex) &&
                   animator.GetCurrentAnimatorStateInfo(layerIndex).shortNameHash == startState)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 2) Esperar a que termine el cruce entre estados.
            while (elapsed < maxWaitSeconds && animator.IsInTransition(layerIndex))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // 3) Esperar a que el estado nuevo complete una reproducción.
            while (elapsed < maxWaitSeconds &&
                   animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime < 1f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (elapsed >= maxWaitSeconds)
                Debug.LogWarning($"[{name}] Se agotó el tiempo de espera de la animación. " +
                                 "Revisa que el trigger tenga una transición y que el clip no esté en loop.", this);
        }
        else
        {
            Debug.LogWarning($"[{name}] No hay Animator o no existe el trigger '{triggerName}'. " +
                             "Se cambia de escena directamente.", this);
        }

        if (esperarFundido)
            while (fading) yield return null;

        onAnimationFinished?.Invoke();
        LoadScene(targetScene);
    }

    /// Baja el volumen de cada fuente desde el que tenga ahora hasta 0 (tiempo sin escalar: funciona con timeScale = 0).
    IEnumerator FadeOutMusic()
    {
        if (fundidoGlobal)
        {
            fading = true;
            float desde = AudioListener.volume;
            float tg = 0f;
            while (tg < fadeOutDuration)
            {
                tg += Time.unscaledDeltaTime;
                float k = fadeOutDuration > 0f ? Mathf.Clamp01(tg / fadeOutDuration) : 1f;
                AudioListener.volume = Mathf.Lerp(desde, 0f, k);
                yield return null;
            }
            AudioListener.volume = 0f;
            fading = false;
            yield break;
        }

        if (musica == null || musica.Length == 0) yield break;

        fading = true;
        float[] inicial = new float[musica.Length];
        for (int i = 0; i < musica.Length; i++)
            inicial[i] = musica[i] != null ? musica[i].volume : 0f;

        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = fadeOutDuration > 0f ? Mathf.Clamp01(t / fadeOutDuration) : 1f;
            for (int i = 0; i < musica.Length; i++)
                if (musica[i] != null) musica[i].volume = Mathf.Lerp(inicial[i], 0f, k);
            yield return null;
        }

        for (int i = 0; i < musica.Length; i++)
            if (musica[i] != null) musica[i].volume = 0f;

        fading = false;
    }

    void LoadScene(string targetScene)
    {
        if (string.IsNullOrEmpty(targetScene))
        {
            int next = SceneManager.GetActiveScene().buildIndex + 1;
            if (next < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(next);
            }
            else
            {
                Debug.LogError($"[{name}] No hay nombre de escena y no existe una siguiente en Build Settings.", this);
                running = false;
            }
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError($"[{name}] La escena '{targetScene}' no está en Build Settings o el nombre es incorrecto.", this);
            running = false;
            return;
        }

        SceneManager.LoadScene(targetScene);
    }

    static bool HasTrigger(Animator anim, string param)
    {
        foreach (var p in anim.parameters)
            if (p.name == param && p.type == AnimatorControllerParameterType.Trigger)
                return true;
        return false;
    }
}