using UnityEngine;

/// Todos los sonidos del robot en UN solo componente. Observa a PlayerMovement (sin modificarlo)
/// y reproduce cada sonido cuando cambia su estado: deslizamiento, giro, magnetizar, desmagnetizar y aterrizaje.
/// Ponlo en cada robot (el principal y los dispositivos): cada uno puede tener sus propios clips.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerAudio : MonoBehaviour
{
    [Header("Salida")]
    [Tooltip("Para los sonidos puntuales (giro, magnetismo, aterrizaje...). Vacío = el AudioSource de este objeto, o se crea uno 2D.")]
    [SerializeField] private AudioSource fuente;
    [Range(0f, 1f)][SerializeField] private float volumen = 1f;

    [Header("Deslizamiento")]
    [Tooltip("Suena en bucle MIENTRAS el robot se mueve y se corta al detenerse. Mejor un clip que enlace bien al repetirse.")]
    [SerializeField] private AudioClip deslizar;
    [Tooltip("Sonido al deslizarse por la pared. Vacío = se usa el normal.")]
    [SerializeField] private AudioClip deslizarMagnetico;
    [Tooltip("Segundos que debe estar parado antes de cortarlo. Evita cortes entre casillas seguidas (con 0, corta al instante).")]
    [SerializeField] private float retardoParada = 0.06f;
    [SerializeField] private float fundidoEntrada = 0.04f;
    [Tooltip("Segundos que tarda en apagarse una vez cortado (con 0, corte seco).")]
    [SerializeField] private float fundidoSalida = 0.06f;
    [Tooltip("Vacío = se crea una fuente propia, 2D y en bucle.")]
    [SerializeField] private AudioSource fuenteDeslizar;

    [Header("Giro")]
    [Tooltip("Giro en el sitio.")]
    [SerializeField] private AudioClip giro;

    [Header("Magnetismo")]
    [SerializeField] private AudioClip magnetizar;
    [SerializeField] private AudioClip desmagnetizar;

    [Header("Caída")]
    [SerializeField] private AudioClip aterrizaje;
    [Tooltip("Velocidad hacia abajo (unidades/seg) a partir de la cual se considera que cae.")]
    [SerializeField] private float velocidadCaidaMinima = 2f;

    [Header("Otros")]
    [Tooltip("Se reproduce al llamar a Victoria() (por ejemplo, desde el evento On Win de WinCondition).")]
    [SerializeField] private AudioClip victoria;

    private const float VentanaSinGiro = 0.5f;   // el avatar se endereza al (des)magnetizarse: eso no es un giro del jugador

    private PlayerMovement movimiento;
    private bool ultimoGirando, ultimoMagnetico, ultimoCayendo;
    private float ultimaY;
    private float sinGiroHasta;
    private float tiempoParado;       // segundos seguidos sin moverse
    private float nivelDeslizar;      // 0..1: fundido del sonido de deslizamiento

    private void Awake()
    {
        movimiento = GetComponent<PlayerMovement>();

        if (fuente == null)
        {
            fuente = GetComponent<AudioSource>();
            if (fuente == null)
            {
                fuente = gameObject.AddComponent<AudioSource>();
                fuente.playOnAwake = false;      // 2D por defecto: se oye igual con la cámara lejos
            }
        }

        // Si es la misma fuente que la de los sonidos puntuales, el Stop() y el volumen del deslizamiento
        // cancelarían (o silenciarían) el resto de sonidos: se separa siempre.
        if (fuenteDeslizar == fuente) fuenteDeslizar = null;

        if (fuenteDeslizar == null)
        {
            // Fuente propia en un hijo, para no mezclarla con la de los sonidos puntuales.
            GameObject hijo = new GameObject("AudioDeslizar");
            hijo.transform.SetParent(transform, false);
            fuenteDeslizar = hijo.AddComponent<AudioSource>();
            fuenteDeslizar.playOnAwake = false;
            fuenteDeslizar.spatialBlend = 0f;
        }
        fuenteDeslizar.loop = true;
        fuenteDeslizar.volume = 0f;

        // Un AudioSource con el componente desmarcado no puede reproducir nada ("Can not play a disabled audio source").
        fuente.enabled = true;
        fuenteDeslizar.enabled = true;
    }

    private void Start()
    {
        // Estado inicial: evita falsos sonidos en el primer frame.
        ultimoGirando = movimiento.rotating;
        ultimoMagnetico = movimiento.magneticMode;
        ultimaY = transform.position.y;
        tiempoParado = retardoParada;
    }

    // LateUpdate: PlayerMovement ya ha actualizado su estado en este frame.
    private void LateUpdate()
    {
        bool moviendo = movimiento.moving;
        bool girando = movimiento.rotating;
        bool magnetico = movimiento.magneticMode;

        float dt = Time.deltaTime;
        float vy = dt > 0f ? (transform.position.y - ultimaY) / dt : 0f;
        ultimaY = transform.position.y;
        bool cayendo = !magnetico && vy < -velocidadCaidaMinima;    // bajar por la pared magnetizado no es caer

        // Si PlayerMovement está desactivado (otro robot tiene el control), no se desliza.
        ActualizarDeslizar(movimiento.enabled && moviendo, magnetico, dt);

        if (magnetico != ultimoMagnetico)
        {
            Reproducir(magnetico ? magnetizar : desmagnetizar);
            sinGiroHasta = Time.time + VentanaSinGiro;
        }

        if (girando && !ultimoGirando && !moviendo && Time.time >= sinGiroHasta) Reproducir(giro);

        if (ultimoCayendo && !cayendo) Reproducir(aterrizaje);       // dejó de caer: toca el suelo

        ultimoGirando = girando;
        ultimoMagnetico = magnetico;
        ultimoCayendo = cayendo;
    }

    // ---------------- Deslizamiento ----------------

    /// Bucle que suena solo mientras el robot se mueve. Entre dos casillas seguidas hay un frame con
    /// moving = false: 'retardoParada' lo absorbe para que no se corte a mitad de un trayecto continuo.
    private void ActualizarDeslizar(bool moviendo, bool magnetico, float dt)
    {
        if (fuenteDeslizar == null) return;

        tiempoParado = moviendo ? 0f : tiempoParado + dt;
        bool sonar = moviendo || tiempoParado < retardoParada;

        AudioClip clip = (magnetico && deslizarMagnetico != null) ? deslizarMagnetico : deslizar;
        float objetivo = (sonar && clip != null) ? 1f : 0f;

        float duracion = objetivo > nivelDeslizar ? fundidoEntrada : fundidoSalida;
        float paso = duracion > 0f ? dt / duracion : 1f;
        nivelDeslizar = Mathf.MoveTowards(nivelDeslizar, objetivo, paso);

        if (objetivo > 0f)
        {
            if (fuenteDeslizar.clip != clip)
            {
                fuenteDeslizar.clip = clip;
                fuenteDeslizar.Play();
            }
            else if (!fuenteDeslizar.isPlaying)
            {
                fuenteDeslizar.Play();
            }
        }

        fuenteDeslizar.volume = volumen * nivelDeslizar;

        // Cuando se ha apagado del todo, se detiene de verdad (la próxima vez empieza desde el principio).
        if (nivelDeslizar <= 0f && fuenteDeslizar.isPlaying) fuenteDeslizar.Stop();
    }

    // ---------------- API pública (para eventos del inspector) ----------------

    /// Enlázalo, por ejemplo, al evento On Win de WinCondition.
    public void Victoria()
    {
        if (victoria == null)
        {
            Debug.LogWarning("PlayerAudio.Victoria(): el campo 'Victoria' no tiene clip asignado en " + name, this);
            return;
        }
        if (fuente == null) fuente = GetComponent<AudioSource>();
        if (fuente == null)
        {
            // Red de seguridad: suena aunque la fuente no exista (por ejemplo, si se llama antes de Awake).
            AudioSource.PlayClipAtPoint(victoria, transform.position, volumen);
            return;
        }

        Sonar(victoria);
    }

    /// Reproduce cualquier clip por el AudioSource del robot (se puede enlazar desde un UnityEvent).
    public void Reproducir(AudioClip clip)
    {
        if (clip == null || fuente == null) return;
        Sonar(clip);
    }

    /// PlayOneShot falla si el objeto de la fuente está inactivo en la jerarquía; en ese caso suena igualmente.
    private void Sonar(AudioClip clip)
    {
        if (fuente != null && fuente.gameObject.activeInHierarchy)
        {
            if (!fuente.enabled) fuente.enabled = true;
            fuente.PlayOneShot(clip, volumen);
        }
        else
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, volumen);
        }
    }
}