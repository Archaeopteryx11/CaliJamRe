using System;
using System.Collections;
using UnityEngine;

public class Box : MonoBehaviour
{
    /// Se dispara cuando la caja queda en un sitio sin salida (para mostrar "reinicia el nivel").
    public static event Action<Box> Stuck;

    [Header("Paredes")]
    [Tooltip("Un tile cuenta como pared si está, al menos, esta fracción de TileSize POR ENCIMA del tile donde está la caja.")]
    [Range(0.1f, 1f)] public float wallMinHeight = 0.5f;

    public Tile CurrentTile { get; private set; }

    bool moving;
    Vector3 target;
    float speed;

    IEnumerator Start()
    {
        yield return null;      // espera 1 frame para que todos los tiles ya existan y estén colocados

        CurrentTile = Tile.At(transform.position);
        if (CurrentTile == null)
        {
            Debug.LogWarning("Box fuera de la cuadrícula: " + name, this);
            yield break;
        }
        CurrentTile.Enter(gameObject);

        var tp = CurrentTile.GetTilePosition();
        transform.position = new Vector3(tp.x, transform.position.y, tp.z);
    }

    void OnDestroy()
    {
        if (CurrentTile != null) CurrentTile.Exit(gameObject);
    }

    /// ¿Se puede empujar la caja hacia ese tile? (Tile.At devuelve el tile más alto de la casilla:
    /// si hay una pared, es ella.)
    public bool CanPushTo(Tile beyond)
    {
        return beyond != null && beyond.IsWalkable && beyond.BoxOn == null && !IsWall(beyond);
    }

    /// Pared = tile activo apilado por encima del nivel del suelo donde está la caja.
    /// Un Tile desactivado (por ejemplo, una puerta abierta) no cuenta.
    bool IsWall(Tile t)
    {
        if (t == null || !t.isActiveAndEnabled) return false;

        float level = CurrentTile != null ? CurrentTile.GetTilePosition().y : transform.position.y;
        return t.GetTilePosition().y > level + Tile.TileSize * wallMinHeight;
    }

    public void PushTo(Tile tile, float moveSpeed)
    {
        CurrentTile.Exit(gameObject);
        CurrentTile = tile;
        CurrentTile.Enter(gameObject);

        var tp = tile.GetTilePosition();
        target = new Vector3(tp.x, transform.position.y, tp.z);
        speed = moveSpeed;
        moving = true;
    }

    void Update()
    {
        if (!moving) return;

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude > 0.000001f) return;

        transform.position = target;
        moving = false;
        CheckStuck();
    }

    /// Atascada = en cada eje (X y Z) le falta al menos un lado (vacío o pared), así que nunca se puede empujar.
    /// Si está sobre un botón no cuenta, porque ahí puede ser justo la solución.
    void CheckStuck()
    {
        if (CurrentTile.GetInCell<PressureButton>() != null) return;

        if (DeadAxis(Vector3.right) && DeadAxis(Vector3.forward))
        {
            Debug.LogWarning("Caja atascada: reinicia el nivel (R).", this);
            if (Stuck != null) Stuck(this);
        }
    }

    bool DeadAxis(Vector3 axis)
    {
        var p = CurrentTile.GetTilePosition();
        return IsDeadSide(Tile.At(p + axis * Tile.TileSize)) || IsDeadSide(Tile.At(p - axis * Tile.TileSize));
    }

    /// Lado sin salida: vacío o pared. Las puertas no cuentan, porque pueden abrirse.
    bool IsDeadSide(Tile t)
    {
        if (t == null) return true;
        return IsWall(t) && t.GetComponent<Door>() == null;
    }
}