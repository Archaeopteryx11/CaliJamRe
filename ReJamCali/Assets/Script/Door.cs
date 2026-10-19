using UnityEngine;

/// Va en el MISMO objeto que el Tile. Cerrada = tile bloqueado.
[RequireComponent(typeof(Tile))]
public class Door : Activatable
{
    [Tooltip("Hijo visual que se ve cuando la puerta está cerrada (un muro, por ejemplo).")]
    public GameObject closedVisual;

    Tile tile;
    bool wantsOpen;
    bool isOpen;

    void Awake()
    {
        tile = GetComponent<Tile>();
        Apply(false);                       // empieza cerrada
    }

    public override void SetActivated(bool on) { wantsOpen = on; }

    void Update()
    {
        if (wantsOpen == isOpen) return;
        if (!wantsOpen && tile.IsOccupied) return;   // no se cierra encima del robot: espera a que salga
        Apply(wantsOpen);
    }

    void Apply(bool open)
    {
        isOpen = open;
        tile.blocked = !open;
        if (closedVisual != null) closedVisual.SetActive(!open);
    }
}