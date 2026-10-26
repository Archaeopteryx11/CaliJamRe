using System;
using System.Collections;
using UnityEngine;

public class Box : MonoBehaviour
{
    /// Se dispara cuando la caja queda en un sitio sin salida.
    public static event Action<Box> Stuck;

    [Header("Paredes")]
    [Tooltip("Un tile cuenta como pared si está, al menos, esta fracción de TileSize POR ENCIMA del tile donde está la caja.")]
    [Range(0.1f, 1f)] public float wallMinHeight = 0.5f;

    [Header("Caída al vacío / Nivel Inferior")]
    public float fallSpeed = 12f;
    [Tooltip("Distancia máxima a caer si no encuentra ningún tile debajo.")]
    public float maxFallDistance = 10f;

    [Header("Sonido")]
    public AudioClip sonidoArrastre;
    AudioSource fuente;

    public Tile CurrentTile { get; private set; }

    bool moving;
    bool isFalling;
    Vector3 target;
    float speed;
    
    float targetFallY;
    Tile landingTile;

    void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.clip = sonidoArrastre;
    }
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

    /// ¿Se puede empujar la caja hacia ese tile? (El vacío es válido porque la caja puede caer).
    public bool CanPushTo(Tile beyond)
    {
        if (beyond == null) return true; 
        return beyond.IsWalkable && beyond.BoxOn == null && !IsWall(beyond);
    }

    bool IsWall(Tile t)
    {
        if (t == null || !t.isActiveAndEnabled) return false;

        float level = CurrentTile != null ? CurrentTile.GetTilePosition().y : transform.position.y;
        return t.GetTilePosition().y > level + Tile.TileSize * wallMinHeight;
    }

    /// Mueve la caja horizontalmente.
    public void PushTo(Tile tile, Vector3 pushDirection, float moveSpeed)
    {
        if (CurrentTile != null) CurrentTile.Exit(gameObject);

        Vector3 startPos = CurrentTile != null ? CurrentTile.GetTilePosition() : transform.position;

        if (tile != null)
        {
            var tp = tile.GetTilePosition();
            target = new Vector3(tp.x, transform.position.y, tp.z);
        }
        else
        {
            // Sin piso: la caja avanza 1 casilla en el aire antes de caer
            target = startPos + pushDirection.normalized * Tile.TileSize;
            target.y = transform.position.y;
        }

        CurrentTile = tile;
        if (CurrentTile != null) CurrentTile.Enter(gameObject);

        speed = moveSpeed;

        if (sonidoArrastre != null && !fuente.isPlaying)
        {
            fuente.volume = AudioManager.VolumenSfx;
            fuente.Play();
        }
        moving = true;
    }

    // Agrega esta versión abreviada dentro de Box.cs:
    public void PushTo(Tile tile, float moveSpeed)
    {
        // Usa la dirección hacia la que apunta la caja o el frente por defecto
        PushTo(tile, transform.forward, moveSpeed);
    }

    void Update()
    {
        // 1. Lógica de caída vertical
        if (isFalling)
        {
            Vector3 targetPosition = new Vector3(transform.position.x, targetFallY, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, fallSpeed * Time.deltaTime);

            if ((transform.position - targetPosition).sqrMagnitude < 0.0001f)
            {
                transform.position = targetPosition;
                isFalling = false;

                // Si aterriza en un tile inferior, se vincula a él
                if (landingTile != null)
                {
                    CurrentTile = landingTile;
                    CurrentTile.Enter(gameObject);
                    CheckStuck();
                }
            }
            return;
        }

        if (!moving) return;

        // 2. Movimiento horizontal
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude > 0.000001f) return;

        transform.position = target;

        moving = false;
        fuente.Stop();
        // 3. Comprobar si quedó sobre el vacío para iniciar caída
        if (CurrentTile == null)
        {
            StartFalling();
        }
        else
        {
            CheckStuck();
        }
    }

    void StartFalling()
    {
        isFalling = true;
        landingTile = FindTileBelow();

        if (landingTile != null)
        {
            targetFallY = landingTile.GetTilePosition().y;
        }
        else
        {
            // Si es un abismo sin fondo, cae la distancia limite y se detiene
            targetFallY = transform.position.y - maxFallDistance;
        }
    }

    /// Escanea hacia abajo en la misma columna (X, Z) para encontrar un Tile en un nivel inferior.
    Tile FindTileBelow()
    {
        float step = Tile.TileSize > 0 ? Tile.TileSize : 1f;
        float startY = transform.position.y;

        for (float y = startY - step; y >= startY - maxFallDistance; y -= step)
        {
            Tile t = Tile.At(new Vector3(transform.position.x, y, transform.position.z));
            if (t != null && t.IsWalkable)
            {
                return t;
            }
        }
        return null;
    }

    void CheckStuck()
    {
        if (CurrentTile == null || CurrentTile.GetInCell<PressureButton>() != null) return;

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

    bool IsDeadSide(Tile t)
    {
        if (t == null) return false; // El vacío no atasca la caja
        return IsWall(t) && t.GetComponent<Door>() == null;
    }
}