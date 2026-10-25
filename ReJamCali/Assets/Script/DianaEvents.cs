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

    public bool IsActive { get; private set; }
    public float TimeLeft { get; private set; }

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

        if (objetivos == null) return;
        foreach (Activatable objetivo in objetivos)
            if (objetivo != null) objetivo.SetActivated(on);
    }
}