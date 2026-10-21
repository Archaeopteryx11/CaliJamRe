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

        Vector3 f = playerAvatar != null ? playerAvatar.transform.forward : transform.forward;
        f.y = 0f;
        facingDir = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
    }

    static InputAction Make(string name, string binding)
    {
        return new InputAction(name, InputActionType.Button, binding);
    }

    void OnEnable() { foreach (var a in moveKeys) a.Enable(); foreach (var a in turnKeys) a.Enable(); }
    void OnDisable() { foreach (var a in moveKeys) a.Disable(); foreach (var a in turnKeys) a.Disable(); }
    void OnDestroy() { foreach (var a in moveKeys) a.Dispose(); foreach (var a in turnKeys) a.Dispose(); }

    void Update()
    {
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
        if (scheme == ControlScheme.TapToTurnHoldToMove) HandleTapHold();
        else HandleArrowsTurn();
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
        if (occupiedTile != null && occupiedTile != destinationTile)
            occupiedTile.Exit(gameObject);               // recién ahora se "suelta" el tile anterior
        occupiedTile = destinationTile;
        destinationTile = null;
    }

    void RotateTowardsFacing()
    {
        if (facingDir.sqrMagnitude < 0.0001f) { rotating = false; return; }

        Quaternion target = Quaternion.LookRotation(facingDir);
        playerAvatar.transform.rotation = Quaternion.RotateTowards(
            playerAvatar.transform.rotation, target, rotationSpeed * Time.deltaTime);
        rotating = Quaternion.Angle(playerAvatar.transform.rotation, target) > 0.5f;
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

    public void SetLeftTile(Tile tile) { leftTile = tile; }
    public void SetRightTile(Tile tile) { rightTile = tile; }
    public void SetForwardTile(Tile tile) { forwardTile = tile; }
    public void SetBackwardTile(Tile tile) { backwardTile = tile; }
    public void SetCurrentTile(Tile tile) { currentTile = tile; }
}