using UnityEngine;

public enum ButtonType { Permanent, Hold, Timed }

/// Va en el MISMO objeto que el Tile donde se pisa.
[RequireComponent(typeof(Tile))]
public class PressureButton : MonoBehaviour
{
    public ButtonType type = ButtonType.Permanent;
    [Tooltip("Solo para Timed: segundos que queda activo.")]
    public float duration = 5f;
    public Activatable[] targets;

    [Header("Feedback visual (opcional)")]
    public Renderer visual;
    public Color offColor = new Color(0.6f, 0.2f, 0.2f);
    public Color onColor = new Color(0.2f, 0.9f, 0.3f);

    [Header("Sonido")]
    public AudioClip sonidoPulsar;
    public AudioClip sonidoSoltar;

    public bool IsActive { get; private set; }
    public float TimeLeft { get; private set; }

    Tile tile;

    void Awake()
    {
        tile = GetComponent<Tile>();
        Paint();
    }

    void OnEnable() { tile.OccupancyChanged += OnOccupancy; }
    void OnDisable() { tile.OccupancyChanged -= OnOccupancy; }

    void OnOccupancy(Tile t)
    {
        if (t.IsOccupied)
        {
            if (type == ButtonType.Timed) TimeLeft = duration;   // pisarlo de nuevo reinicia la cuenta
            Set(true);
        }
        else if (type == ButtonType.Hold)
        {
            Set(false);                                          // al soltarlo (quitar robot/caja) se desactiva
        }
    }

    void Update()
    {
        if (type != ButtonType.Timed || !IsActive) return;
        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f) { TimeLeft = 0f; Set(false); }
    }

    void Set(bool on)
    {
        if (IsActive == on) return;
        IsActive = on;
        AudioManager.Sfx(on ? sonidoPulsar : sonidoSoltar);
        foreach (var t in targets) if (t != null) t.SetActivated(on);
        Paint();
    }

    void Paint()
    {
        if (visual != null) visual.material.color = IsActive ? onColor : offColor;
    }
}