using System.Collections.Generic;
using UnityEngine;

/// Va en el MISMO objeto que el Tile. Cerrada = tile bloqueado.
/// El propio tile (con su collider y sus hijos) se hunde en el suelo para abrir y vuelve a subir para cerrar.
/// Solo se mueve en Y del mundo, así que el registro de casillas de Tile (que usa X y Z) no se ve afectado.
/// Debajo de la puerta debe haber un tile de suelo.
///
/// Varias fuentes (botones, dianas...) pueden controlar la misma puerta. Cada una avisa con
/// SetActivated(true) al activarse y SetActivated(false) al desactivarse, SOLO cuando su estado cambia.
/// La puerta cuenta cuántas fuentes están activas y se abre mientras haya 'requiredActive' o más.
[RequireComponent(typeof(Tile))]
public class Door : Activatable
{
    [Header("Fuentes (botones, dianas...)")]
    [Tooltip("Cuántas fuentes deben estar activas a la vez para abrir. 1 = basta con cualquiera.")]
    [Min(1)] public int requiredActive = 1;

    [Header("Animación (el tile se hunde para abrir y sube para cerrar)")]
    [Tooltip("Hunde el tile tanto como mide de alto (según sus Renderers). Si está desactivado, se usa Sink Distance.")]
    public bool autoDistance = true;
    [Tooltip("Distancia en unidades del mundo que baja el tile (si Auto Distance está desactivado o no se puede medir).")]
    public float sinkDistance = 1.5f;
    [Tooltip("Extra para que el tile quede del todo bajo el suelo (solo con Auto Distance).")]
    public float sinkMargin = 0.05f;
    public float openDuration = 0.6f;
    public float closeDuration = 0.5f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Sonido")]
    public AudioClip sonidoAbrir;
    public AudioClip sonidoCerrar;
    bool querAbrirAntes;

    [Header("Paso")]
    [Tooltip("Al abrir, el tile se desbloquea cuando se ha hundido este porcentaje. Al cerrar, se bloquea al instante.")]
    [Range(0f, 1f)] public float passableAt = 0.8f;
    [Tooltip("Mientras se puede pasar, desactiva el componente Tile para que los detectores de pared de PlayerMovement " +
             "(isActiveAndEnabled) lo ignoren aunque su collider todavía esté cerca. Desactívalo si no los usas con puertas.")]
    public bool disableTileWhenPassable = true;
    [Tooltip("Oculta los Renderers cuando está del todo hundido (ahorra dibujado y sombras).")]
    public bool hideWhenOpen = true;

    Tile tile;
    int activeSources;
    float progress;                       // 0 = cerrada (arriba), 1 = abierta (hundida)
    float closedY, openY;
    bool visible = true;
    readonly List<Renderer> renderers = new List<Renderer>();

    void Awake()
    {
        tile = GetComponent<Tile>();

        // Solo se gestionan los Renderers que empiezan activos.
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (r.enabled) renderers.Add(r);
        }

        float depth = sinkDistance;
        if (autoDistance && TryGetHeight(out float h)) depth = h + sinkMargin;

        closedY = transform.position.y;
        openY = closedY - depth;          // "abajo" es siempre hacia abajo en el mundo

        progress = 0f;
        tile.blocked = true;              // empieza cerrada
        ApplyPose();
    }

    public override void SetActivated(bool on)
    {
        activeSources += on ? 1 : -1;
        if (activeSources < 0)
        {
            Debug.LogWarning($"[{name}] Una fuente avisó de que se desactiva sin haberse activado: " +
                             "cada fuente debe llamar a SetActivated solo cuando su estado cambia.", this);
            activeSources = 0;
        }
    }

    void Update()
    {
        bool wantsOpen = activeSources >= requiredActive;

        // Nunca cerrar encima del robot o de una caja: espera a que salgan.
        if (!wantsOpen && progress > 0f && tile.IsOccupied) wantsOpen = true;

        if (wantsOpen != querAbrirAntes)
        {
            querAbrirAntes = wantsOpen;
            AudioManager.Sfx(wantsOpen ? sonidoAbrir : sonidoCerrar);
        }

        float target = wantsOpen ? 1f : 0f;
        if (progress != target)
        {
            float duration = wantsOpen ? openDuration : closeDuration;
            float step = duration > 0f ? Time.deltaTime / duration : 1f;
            progress = Mathf.MoveTowards(progress, target, step);
            ApplyPose();
        }

        // Al abrir, el paso se libera cuando ya está lo bastante hundida; al cerrar, se bloquea ya.
        bool passable = wantsOpen && progress >= passableAt;
        tile.blocked = !passable;
        if (disableTileWhenPassable) tile.enabled = !passable;
    }

    void ApplyPose()
    {
        // Solo cambia Y: la casilla (X, Z) con la que Tile se registró no se toca.
        Vector3 p = transform.position;
        p.y = Mathf.LerpUnclamped(closedY, openY, ease.Evaluate(progress));
        transform.position = p;

        bool show = !(hideWhenOpen && progress >= 1f);
        if (show != visible)
        {
            visible = show;
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = show;
        }
    }

    /// Alto en el mundo del conjunto de Renderers de la puerta.
    bool TryGetHeight(out float height)
    {
        height = 0f;
        if (renderers.Count == 0) return false;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Count; i++) b.Encapsulate(renderers[i].bounds);

        height = b.size.y;
        return height > 0.01f;
    }
}