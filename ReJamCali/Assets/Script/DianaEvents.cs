using UnityEngine;

public class DianaEvents : MonoBehaviour
{
    public enum Modo { Permanente, Temporizado }

    [Header("Activar objetivos (puertas, etc.)")]
    [Tooltip("Permanente: queda activa para siempre tras el primer impacto. Temporizado: se desactiva sola.")]
    [SerializeField] private Modo modo = Modo.Permanente;
    [Tooltip("Solo Temporizado: segundos que queda activa. Cada impacto reinicia la cuenta.")]
    [SerializeField] private float duracion = 5f;
    [Tooltip("Puertas (u otros Activatable) que se activan con la diana.")]
    [SerializeField] private Activatable[] objetivos;

    [Header("Feedback visual")]
    [Tooltip("Renderer de la diana (el que tiene los 2 materiales). Vacío = el primero que haya en este objeto o en sus hijos.")]
    [SerializeField] private Renderer rendererDiana;
    [Tooltip("Versión brillante de los materiales, en el MISMO orden y cantidad que los originales. " +
             "Se aplican al activarse la diana y, en modo Temporizado, se restauran los originales al desactivarse.")]
    [SerializeField] private Material[] materialesBrillantes;

    [Header("Audio")]
    [Tooltip("Suena en CADA impacto.")]
    [SerializeField] private AudioClip sonidoImpacto;
    [Range(0f, 1f)][SerializeField] private float volumen = 1f;
    [Tooltip("Vacío = se usa el AudioSource de este objeto, o se crea uno 2D.")]
    [SerializeField] private AudioSource fuenteAudio;

    public bool IsActive { get; private set; }
    public float TimeLeft { get; private set; }

    private Material[] materialesOriginales;

    private void Awake()
    {
        if (rendererDiana == null) rendererDiana = GetComponentInChildren<Renderer>();
        if (rendererDiana != null) materialesOriginales = rendererDiana.sharedMaterials;

        if (sonidoImpacto != null && fuenteAudio == null)
        {
            fuenteAudio = GetComponent<AudioSource>();
            if (fuenteAudio == null)
            {
                fuenteAudio = gameObject.AddComponent<AudioSource>();
                fuenteAudio.playOnAwake = false;     // 2D por defecto: se oye igual con la cámara lejos
            }
        }
    }

    public void DebugHit()
    {
        Debug.Log("Me dieron :v");
    }

    /// Enlázalo al evento 'Diana Hit' del componente Diana (se llama en CADA impacto).
    /// Avisa a los objetivos SOLO cuando cambia el estado, que es lo que necesita Door para contar sus fuentes.
    public void ActivarObjetivos()
    {
        if (modo == Modo.Temporizado) TimeLeft = duracion;   // un nuevo impacto reinicia la cuenta
        Set(true);
        Sonar();
    }

    private void Update()
    {
        if (modo != Modo.Temporizado || !IsActive) return;

        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            Set(false);
        }
    }

    private void OnDisable()
    {
        // Si se desactiva con la cuenta en marcha, libera los objetivos para que no se queden abiertos para siempre.
        if (modo == Modo.Temporizado) Set(false);
    }

    private void Set(bool on)
    {
        if (IsActive == on) return;          // solo avisa cuando cambia el estado
        IsActive = on;

        AplicarVisual(on);

        if (objetivos == null) return;
        foreach (Activatable objetivo in objetivos)
            if (objetivo != null) objetivo.SetActivated(on);
    }

    private void AplicarVisual(bool brillante)
    {
        if (rendererDiana == null || materialesOriginales == null) return;

        if (!brillante)
        {
            rendererDiana.sharedMaterials = materialesOriginales;
            return;
        }

        if (materialesBrillantes == null || materialesBrillantes.Length != materialesOriginales.Length)
        {
            Debug.LogWarning("DianaEvents: 'Materiales Brillantes' debe tener los mismos materiales que la diana (" +
                             materialesOriginales.Length + "), en el mismo orden.", this);
            return;
        }

        rendererDiana.sharedMaterials = materialesBrillantes;
    }

    private void Sonar()
    {
        if (sonidoImpacto != null && fuenteAudio != null)
            fuenteAudio.PlayOneShot(sonidoImpacto, volumen);
    }
}