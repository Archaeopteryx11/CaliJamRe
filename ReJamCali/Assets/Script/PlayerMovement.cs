using UnityEngine;
using UnityEngine.InputSystem;

public enum ControlScheme
{
    /// WASD: toque rápido = solo gira, mantener = camina. (Opción 1 del GDD)
    TapToTurnHoldToMove,
    /// WASD camina siempre; las flechas solo giran en su eje. (Opción 2 del GDD)
    ArrowsTurnWasdMove,
}

public class PlayerMovement : MonoBehaviour
{
    public GameObject playerAvatar;
    public CameraFollow cameraRig;

    public Tile currentTile;
    public Tile leftTile;
    public Tile rightTile;
    public Tile forwardTile;
    public Tile backwardTile;

    [Header("Control")]
    public ControlScheme scheme = ControlScheme.TapToTurnHoldToMove;
    [Tooltip("Segundos que hay que mantener la tecla para empezar a caminar.")]
    public float holdThreshold = 0.18f;
    [Range(0, 3)] public int keyRotation = 0;

    [Header("Movimiento")]
    public float speed = 4f;
    [Tooltip("Grados por segundo")]
    public float rotationSpeed = 720f;

    [Header("Estado (solo lectura)")]
    public bool moving;
    public bool rotating;
    public Vector3 movingDirection;
    public Vector3 targetPosition;
    public float distanceToTarget;

    static readonly Vector3[] Ring = { Vector3.right, Vector3.back, Vector3.left, Vector3.forward };

    InputAction[] moveKeys;   // 0=W, 1=D, 2=S, 3=A
    InputAction[] turnKeys;   // 0=↑, 1=→, 2=↓, 3=←
    readonly float[] pressTime = new float[4];
    Vector3 facingDir;

    Tile occupiedTile;        // tile del que "parte" el robot
    Tile destinationTile;     // tile al que va

    [Header("Magnetismo")]
    public bool magneticMode;
    public Key magneticToggleKey = Key.M;
    public float magneticRange = 1.2f;
    public float magneticRotationSpeed = 360f;
    public LayerMask magneticLayers = ~0;

    private Vector3 surfaceUp = Vector3.up;
    private Tile magneticTile;
    private InputAction magneticToggleAction;

    [Header("Movimiento magnético")]
    public float magneticAlignmentSpeed = 540f;
    [Range(0.5f, 1f)]
    public float neighbourDirectionThreshold = 0.85f;

    private Vector3 magneticForward;
    private bool magneticMoving;

    public float magneticSurfaceOffset = 0.5f;
    public float magneticVerticalOffset = 0.5f;

    void Awake()
    {
        if (cameraRig == null) cameraRig = Object.FindFirstObjectByType<CameraFollow>();

        moveKeys = new[]
        {
            Make("W", "<Keyboard>/w"), Make("D", "<Keyboard>/d"),
            Make("S", "<Keyboard>/s"), Make("A", "<Keyboard>/a"),
        };
        turnKeys = new[]
        {
            Make("TurnUp", "<Keyboard>/upArrow"),    Make("TurnRight", "<Keyboard>/rightArrow"),
            Make("TurnDown", "<Keyboard>/downArrow"), Make("TurnLeft", "<Keyboard>/leftArrow"),
        };

        magneticToggleAction = new InputAction(
            "ToggleMagnetism",
            InputActionType.Button,
            "<Keyboard>/m"
        );

        Vector3 f = playerAvatar != null ? playerAvatar.transform.forward : transform.forward;
        f.y = 0f;
        facingDir = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    static InputAction Make(string name, string binding)
    {
        return new InputAction(name, InputActionType.Button, binding);
    }

    void OnEnable() { foreach (var a in moveKeys) a.Enable(); foreach (var a in turnKeys) a.Enable(); magneticToggleAction.Enable();}
    void OnDisable() { foreach (var a in moveKeys) a.Disable(); foreach (var a in turnKeys) a.Disable(); magneticToggleAction.Disable();}
    void OnDestroy() { foreach (var a in moveKeys) a.Dispose(); foreach (var a in turnKeys) a.Dispose(); magneticToggleAction.Dispose();}

    void Update()
    {

        if (magneticToggleAction.WasPressedThisFrame()){
        Debug.Log("Tecla M detectada");
        ToggleMagnetism();
        }

        SyncInitialTile();
        if (!moving) ReadInput();
        if (moving) PerformMovement();
        RotateTowardsFacing();
    }

    /// El detector de piso rellena currentTile unos frames después de empezar;
    /// la primera vez que existe, el robot "entra" a ese tile (por si empieza sobre un botón).
    void SyncInitialTile()
    {
        if (occupiedTile != null || currentTile == null) return;
        occupiedTile = currentTile;
        occupiedTile.Enter(gameObject);
    }

    // ---------------- Input ----------------
    void ReadInput()
    {
        if (magneticMode)
        {
            HandleMagneticInput();
            return;
        }

        if (scheme == ControlScheme.TapToTurnHoldToMove)
            HandleTapHold();
        else
            HandleArrowsTurn();
    }
    void HandleTapHold()
    {
        int active = -1;
        float latest = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            if (moveKeys[i].WasPressedThisFrame())
            {
                pressTime[i] = Time.time;
                facingDir = KeyToWorldDir(i);
            }
            if (moveKeys[i].IsPressed() && pressTime[i] >= latest)
            {
                latest = pressTime[i];
                active = i;
            }
        }
        if (active < 0) return;

        facingDir = KeyToWorldDir(active);
        bool heldLongEnough = Time.time - pressTime[active] >= holdThreshold;
        if (heldLongEnough && FacingReached())
            TryMoveTo(GetNeighbour(facingDir));
    }

    void HandleArrowsTurn()
    {
        for (int i = 0; i < 4; i++)
            if (turnKeys[i].WasPressedThisFrame()) facingDir = KeyToWorldDir(i);

        for (int i = 0; i < 4; i++)
        {
            if (!moveKeys[i].IsPressed()) continue;
            facingDir = KeyToWorldDir(i);
            if (FacingReached()) TryMoveTo(GetNeighbour(facingDir));
            break;
        }
    }

    Vector3 KeyToWorldDir(int keyIndex)
    {
        int view = cameraRig != null ? cameraRig.View : 0;
        return Ring[(keyIndex + view + keyRotation) & 3];
    }

    Tile GetNeighbour(Vector3 worldDir)
    {
        Vector3 local = transform.InverseTransformDirection(worldDir);
        if (Mathf.Abs(local.z) > Mathf.Abs(local.x))
            return local.z > 0f ? forwardTile : backwardTile;
        return local.x > 0f ? rightTile : leftTile;
    }

    bool FacingReached()
    {
        return Vector3.Angle(playerAvatar.transform.forward, facingDir) < 10f;
    }

    //Movimiento y giro
    void TryMoveTo(Tile tile)
    {
        if (tile == null || !tile.IsWalkable) return;    // puerta cerrada, etc.

        // Dirección del paso (plana) y, si hay caja en el destino, si se puede empujar.
        Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 dir = (tile.GetTilePosition() - flatPos).normalized;

        Box box = tile.BoxOn;
        Tile beyond = null;
        if (box != null)
        {
            beyond = Tile.At(tile.GetTilePosition() + dir * Tile.TileSize);
            if (!box.CanPushTo(beyond)) return;          // la caja no tiene a dónde ir: el robot no avanza
        }

        destinationTile = tile;
        destinationTile.Enter(gameObject);
        MoveToPosition(tile.GetTilePosition());
        if (!moving)
        {
            destinationTile.Exit(gameObject);
            destinationTile = null;
            return;
        }

        if (box != null) box.PushTo(beyond, speed);      // la caja se mueve a la misma velocidad que el robot
    }

    void PerformMovement()
    {
        transform.position += movingDirection * speed * Time.deltaTime;
        float currentDistance = (targetPosition - transform.position).magnitude;
        if (distanceToTarget < currentDistance)
        {
            moving = false;
            transform.position = targetPosition;
            OnArrived();
        }
        else
        {
            distanceToTarget = currentDistance;
        }
    }

    void OnArrived()
    {
        if (magneticMoving)
        {
            magneticTile = destinationTile;
            destinationTile = null;
            magneticMoving = false;
            return;
        }

        if (occupiedTile != null && occupiedTile != destinationTile)
            occupiedTile.Exit(gameObject);

        occupiedTile = destinationTile;
        destinationTile = null;
    }

    void RotateTowardsFacing()
    {
        if (facingDir.sqrMagnitude < 0.0001f)
        {
            rotating = false;
            return;
        }

        Vector3 up = magneticMode ? surfaceUp : Vector3.up;

        Vector3 forward = Vector3.ProjectOnPlane(
            facingDir,
            up
        ).normalized;

        if (forward.sqrMagnitude < 0.0001f)
        {
            rotating = false;
            return;
        }

        Quaternion target = Quaternion.LookRotation(forward, up);

        playerAvatar.transform.rotation = Quaternion.RotateTowards(
            playerAvatar.transform.rotation,
            target,
            (magneticMode ? magneticAlignmentSpeed : rotationSpeed)
                * Time.deltaTime
        );

        rotating = Quaternion.Angle(
            playerAvatar.transform.rotation,
            target
        ) > 0.5f;
    }

    public void MoveToPosition(Vector3 position)
    {
        targetPosition = position;
        movingDirection = position - transform.position;
        distanceToTarget = movingDirection.magnitude;
        if (distanceToTarget < 0.001f) return;
        movingDirection = movingDirection.normalized;

        Vector3 flat = movingDirection; flat.y = 0f;
        if (flat.sqrMagnitude > 0.0001f) facingDir = flat.normalized;

        moving = true;
    }

    void HandleMagneticInput()
    {
        if (magneticTile == null || moving)
            return;

        int active = -1;
        float latest = float.MinValue;

        for (int i = 0; i < 4; i++)
        {
            if (moveKeys[i].WasPressedThisFrame())
                pressTime[i] = Time.time;

            if (moveKeys[i].IsPressed() &&
                pressTime[i] >= latest)
            {
                latest = pressTime[i];
                active = i;
            }
        }

        if (active < 0)
            return;

        Vector3 wallNormal = surfaceUp.normalized;

        // Eje vertical del mundo: subir y bajar por la pared.
        Vector3 vertical = Vector3.up;

        // Eje horizontal de la pared, si después lo necesitas.
        Vector3 horizontal = Vector3.Cross(wallNormal, vertical).normalized;

        Vector3 direction;

        switch (active)
        {
            case 0: direction = vertical; break;       // Arriba
            case 1: direction = horizontal; break;     // Derecha
            case 2: direction = -vertical; break;      // Abajo
            default: direction = -horizontal; break;   // Izquierda
        }

        facingDir = direction;

        // Mantener el sistema de pulsación prolongada.
        if (Time.time - pressTime[active] < holdThreshold)
            return;

        // Diagnóstico temporal.
        Debug.Log($"Movimiento magnético solicitado: {direction}");

        TryMagneticMove(direction);
    }

    void TryMagneticMove(Vector3 direction)
    {
        if (magneticMoving || magneticTile == null)
        {
            Debug.LogWarning("Bloqueado: ya se mueve o no hay tile magnético.");
            return;
        }

        // Solo permitimos movimiento vertical por la pared.
        direction = direction.y >= 0f ? Vector3.up : Vector3.down;

        // Comprobar que la dirección vertical esté en el plano de la pared.
        Vector3 wallNormal = surfaceUp.normalized;

        

        if (Mathf.Abs(Vector3.Dot(direction, wallNormal)) > 0.1f)
        {
            Debug.LogWarning(
                "Movimiento cancelado: la pared no permite movimiento vertical."
            );
            return;
        }

        Tile neighbour = FindMagneticNeighbour(direction);

        if (neighbour == null)
        {
            Debug.LogWarning("Movimiento cancelado: no se encontró un tile vecino.");
            return;
        }

        if (neighbour.BoxOn != null)
        {
            Debug.LogWarning("Movimiento cancelado: hay una caja en el vecino.");
            return;
        }

        destinationTile = neighbour;
        magneticMoving = true;

        targetPosition = neighbour.transform.position;
        movingDirection = direction;

        distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        moving = distanceToTarget > 0.001f;

        if (!moving)
        {
            magneticTile = destinationTile;
            destinationTile = null;
            magneticMoving = false;
        }
    }
    
    Tile FindMagneticNeighbour(Vector3 direction)
    {
        Tile best = null;
        float bestDistance = float.MaxValue;

        foreach (Tile candidate in Tile.AllTiles)
        {
            if (candidate == null || candidate == magneticTile)
                continue;

            if (!candidate.CanMagnetize)
            {
                Debug.Log($"{candidate.name}: no puede magnetizarse.");
                continue;
            }

            Vector3 delta =
                candidate.transform.position -
                magneticTile.transform.position;

            float distance = delta.magnitude;

            if (distance < 0.001f)
                continue;

            if (Mathf.Abs(distance - Tile.TileSize) > 0.2f)
            {
                Debug.Log(
                    $"{candidate.name}: distancia incorrecta ({distance:F2})."
                );
                continue;
            }

            Vector3 candidateDirection = delta / distance;

            if (Vector3.Dot(candidateDirection, direction)
                < neighbourDirectionThreshold)
            {
                Debug.Log(
                    $"{candidate.name}: dirección incorrecta. " +
                    $"Dot={Vector3.Dot(candidateDirection, direction):F2}"
                );
                continue;
            }

            if (Vector3.Dot(
                candidate.surfaceNormal.normalized,
                surfaceUp
            ) < 0.95f)
            {
                Debug.Log(
                    $"{candidate.name}: normal de superficie diferente."
                );
                continue;
            }

            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        Debug.Log(
            best != null
                ? $"Vecino seleccionado: {best.name}"
                : "No existe ningún vecino válido."
        );

        return best;
    }


    void ToggleMagnetism()
    {
        if (magneticMode)
        {
            magneticMode = false;
            magneticTile = null;
            surfaceUp = Vector3.up;
            magneticMoving = false;

            Debug.Log("Magnetismo desactivado");
            return;
        }

        if (TryFindMagneticTile(out Tile tile))
        {
            magneticTile = tile;
            magneticMode = true;
            surfaceUp = tile.surfaceNormal.normalized;

            // Proyectar la dirección del personaje sobre la pared.
            Vector3 projectedForward = Vector3.ProjectOnPlane(
                playerAvatar.transform.forward,
                surfaceUp
            ).normalized;

            // Si mira directamente hacia la pared, usar el eje vertical
            // del mundo como dirección inicial para poder escalar.
            if (projectedForward.sqrMagnitude < 0.001f)
            {
                projectedForward = Vector3.ProjectOnPlane(
                    Vector3.up,
                    surfaceUp
                ).normalized;
            }

            // Último recurso si la superficie es horizontal.
            if (projectedForward.sqrMagnitude < 0.001f)
            {
                projectedForward = Vector3.ProjectOnPlane(
                    Vector3.forward,
                    surfaceUp
                ).normalized;
            }

            facingDir = projectedForward;
            magneticForward = projectedForward;
            

            Debug.Log("Magnetismo activado en: " + tile.name);
        }
        else
        {
            Debug.LogWarning(
                "No se encontró ningún Tile metálico en el alcance del raycast."
            );
        }
    }

    bool TryFindMagneticTile(out Tile tile)
{
    tile = null;

    Vector3 origin = transform.position + Vector3.up * 1f;

    // Buscar en las seis direcciones principales del mundo.
    Vector3[] directions =
    {
        Vector3.up,
        Vector3.down,
        Vector3.left,
        Vector3.right,
        Vector3.forward,
        Vector3.back
    };

    foreach (Vector3 direction in directions)
    {
        Debug.DrawRay(
            origin,
            direction * magneticRange,
            Color.yellow,
            2f
        );

        if (!Physics.Raycast(
            origin,
            direction,
            out RaycastHit hit,
            magneticRange,
            magneticLayers,
            QueryTriggerInteraction.Ignore))
        {
            continue;
        }

        Tile detected = hit.collider.GetComponentInChildren<Tile>();

        if (detected == null || !detected.CanMagnetize)
            continue;

        tile = detected;
        return true;
    }

    return false;
}

    public void SetLeftTile(Tile tile) { leftTile = tile; }
    public void SetRightTile(Tile tile) { rightTile = tile; }
    public void SetForwardTile(Tile tile) { forwardTile = tile; }
    public void SetBackwardTile(Tile tile) { backwardTile = tile; }
    public void SetCurrentTile(Tile tile) { currentTile = tile; }
}