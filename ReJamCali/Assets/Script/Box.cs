using System;
using System.Collections;
using UnityEngine;

public class Box : MonoBehaviour
{
    /// Se dispara cuando la caja queda en un sitio sin salida (para mostrar "reinicia el nivel").
    public static event Action<Box> Stuck;

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

    /// ¿Se puede empujar la caja hacia ese tile?
    public bool CanPushTo(Tile beyond)
    {
        return beyond != null && beyond.IsWalkable && beyond.BoxOn == null;
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

    /// Atascada = en cada eje (X y Z) le falta al menos un lado, así que nunca se puede empujar.
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
        return Tile.At(p + axis * Tile.TileSize) == null || Tile.At(p - axis * Tile.TileSize) == null;
    }
}