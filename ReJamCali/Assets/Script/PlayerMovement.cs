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

    [Header("Paredes (tiles a la altura del cuerpo)")]
    public Tile wallLeftTile;
    public Tile wallRightTile;
    public Tile wallForwardTile;
    public Tile wallBackwardTile;

    public void SetWallLeftTile(Tile t) { wallLeftTile = t; }
    public void SetWallRightTile(Tile t) { wallRightTile = t; }
    public void SetWallForwardTile(Tile t) { wallForwardTile = t; }
    public void SetWallBackwardTile(Tile t) { wallBackwardTile = t; }

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

    private Vector3 magneticForward;
    private bool magneticMoving;

    [Header("Enganche magnético")]
    public float magneticAttachSpeed = 5f;
    private bool magneticAttaching = false;
    private Tile landingTile;

    [Header("Geometría del jugador")]
    [Tooltip("Distancia pivote→pies. Negativo = calibrar automáticamente en Start.")]
    public float pivotToFeet = -1f;
    public bool autoCalibrateFeet = true;
    public float groundProbeUp = 0.3f;   // el rayo de suelo parte esta altura sobre los pies
    [Tooltip("Si es false, magneticSurfaceOffset es la distancia pivote→superficie.")]
    public bool autoSurfaceOffset = true;
    public float magneticSurfaceOffset = 0.02f;
    public float magneticProbeHeight = 1f;

    [Header("Separación de la pared")]
    [Tooltip("Si es true, usa el volumen de los Renderers del modelo; si no, solo el Collider.")]
    public bool useModelBounds = true;
    public float skin = 0.03f;
    [Tooltip("Velocidad (m/s) a la que el cuerpo se aleja de la pared al caer.")]
    public float peelOffSpeed = 3f;

    Collider bodyCollider;
    Collider detachWallCollider;
    Vector3 detachNormal;
    Vector3 detachFacePoint;
    float settleTimer;

    [Header("Caída (gravedad manual)")]
    public float gravity = 20f;
    public float maxFallSpeed = 25f;
    public float landingCenterSpeed = 6f;
    public LayerMask groundLayers = ~0;
    [Range(0f, 1f)] public float groundMinNormalY = 0.7f;
    public float fallAbortY = -100f;


    enum DetachPhase { None, Falling, Settling }
    DetachPhase detachPhase = DetachPhase.None;
    float fallVelocity;
    Vector3 landingPoint;

    static readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    static readonly Vector3[] ProbeDirs =
    {
        Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
    };
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

    void Start()
    {
        if (autoCalibrateFeet) pivotToFeet = CalibratePivotToFeet();
    }

    void Update()
    {

        if (HabilidadesNivel.Imantar && magneticToggleAction.WasPressedThisFrame()) ToggleMagnetism();

        UpdateMagneticAttach();
        UpdateMagneticDetach();

        if (magneticAttaching || detachPhase != DetachPhase.None)
        {
            RotateTowardsFacing();   // el avatar se orienta mientras engancha/cae
            return;
        }
        if (!magneticMode)
        {
            // Si el detector coló un tile de pared, vuelve al tile realmente ocupado.
            if (!moving && occupiedTile != null && IsMagneticWallTile(currentTile))
            {
                currentTile = occupiedTile;
                RefreshGroundNeighbours(occupiedTile);
            }
            SyncInitialTile();
        }
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

        if (WallInDirection(dir)) return;

        Box box = tile.BoxOn;
        Tile beyond = null;
        if (box != null)
        {
            beyond = Tile.At(tile.GetTilePosition() + dir * Tile.TileSize);
            if (!box.CanPushTo(beyond)) return;          // la caja no tiene a dónde ir: el robot no avanza
        }

        destinationTile = tile;
        destinationTile.Enter(gameObject);
        Vector3 stepTarget = tile.GetTilePosition();
        stepTarget.y = transform.position.y;     // conservar la altura de aterrizaje
        MoveToPosition(stepTarget);
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

    /// Ejes de la pared vistos por quien mira hacia ella: W=arriba, D=derecha.
    void GetWallAxes(out Vector3 wallUp, out Vector3 wallRight)
    {
        Vector3 n = surfaceUp;
        wallUp = Vector3.ProjectOnPlane(Vector3.up, n);
        if (wallUp.sqrMagnitude < 0.01f)                    // techo/suelo
            wallUp = Vector3.ProjectOnPlane(magneticForward, n);
        wallUp.Normalize();
        wallRight = Vector3.Cross(n, wallUp).normalized;    // corregido (antes Cross(up, n))
    }

    void HandleMagneticInput()
    {
        if (magneticTile == null || moving) return;

        int active = -1;
        float latest = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            if (moveKeys[i].WasPressedThisFrame()) pressTime[i] = Time.time;
            if (moveKeys[i].IsPressed() && pressTime[i] >= latest)
            {
                latest = pressTime[i];
                active = i;
            }
        }
        if (active < 0) return;
        if (Time.time - pressTime[active] < holdThreshold) return;

        GetWallAxes(out Vector3 wallUp, out Vector3 wallRight);

        Vector3 direction;
        switch (active)
        {
            case 0:  direction = wallUp; break;       // W
            case 1:  direction = wallRight; break;    // D
            case 2:  direction = -wallUp; break;      // S
            default: direction = -wallRight; break;   // A
        }

        facingDir = direction;
        TryMagneticMove(direction);
    }

    void TryMagneticMove(Vector3 direction)
    {
        if (magneticMoving || magneticTile == null) return;

        Tile neighbour = FindMagneticNeighbour(direction);
        if (neighbour == null || neighbour.BoxOn != null) return;

        destinationTile = neighbour;
        magneticMoving = true;
        targetPosition = ComputeMagneticPivot(neighbour, surfaceUp);

        Vector3 delta = targetPosition - transform.position;
        distanceToTarget = delta.magnitude;

        if (distanceToTarget < 0.001f)
        {
            magneticTile = neighbour;
            destinationTile = null;
            magneticMoving = false;
            return;
        }

        movingDirection = delta / distanceToTarget;
        moving = true;                 // PerformMovement/OnArrived existentes se reutilizan
    }

    Tile FindMagneticNeighbour(Vector3 direction)
    {
        Vector3 n = surfaceUp;
        Vector3 origin = TileCenter(magneticTile);
        Vector3 expected = direction * Tile.TileSize;

        Tile best = null;
        float bestError = Tile.TileSize * 0.25f;   // tolerancia

        foreach (Tile cand in Tile.AllTiles)
        {
            if (cand == null || cand == magneticTile || !cand.CanMagnetize) continue;
            if (Vector3.Dot(cand.surfaceNormal.normalized, n) < 0.95f) continue;

            Vector3 delta = TileCenter(cand) - origin;
            if (Mathf.Abs(Vector3.Dot(delta, n)) > Tile.TileSize * 0.1f) continue; // coplanar

            float error = (delta - expected).magnitude;
            if (error < bestError) { best = cand; bestError = error; }
        }
        return best;
    }
    


    public void ToggleMagnetism()
    {
        if (!isActiveAndEnabled) return;

        if (magneticAttaching || magneticMoving || moving || detachPhase != DetachPhase.None)
            return;

        if (magneticMode) { BeginMagneticDetach(); return; }

        if (!TryFindMagneticTile(out Tile tile, out Vector3 normal))
        {
            Debug.LogWarning("No se encontró ningún Tile magnético al alcance.");
            return;
        }

        magneticTile = tile;
        magneticMode = true;
        surfaceUp = normal;
        targetPosition = ComputeMagneticPivot(tile, surfaceUp);

        Vector3 fwd = Vector3.ProjectOnPlane(playerAvatar.transform.forward, surfaceUp);
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.ProjectOnPlane(Vector3.up, surfaceUp);
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.ProjectOnPlane(Vector3.forward, surfaceUp);
        magneticForward = fwd.normalized;
        facingDir = magneticForward;

        // Ocupación: en la pared el robot ya no ocupa el tile del suelo.
        if (occupiedTile != null) { occupiedTile.Exit(gameObject); occupiedTile = null; }

        magneticAttaching = true;
        currentTile = null;
        leftTile = rightTile = forwardTile = backwardTile = null;
    }

    bool RaycastNearest(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
    {
        best = default;
        int n = Physics.RaycastNonAlloc(origin, dir, hitBuffer, dist, magneticLayers,
                                        QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider.transform.IsChildOf(transform)) continue;   // ignorar al propio jugador
            if (h.distance < bestDist) { best = h; bestDist = h.distance; found = true; }
        }
        return found;
    }

    static Tile ResolveTile(Collider c)
    {
        if (c == null) return null;

        Tile t = c.GetComponentInParent<Tile>();
        if (t == null) t = c.GetComponentInChildren<Tile>();
        if (t == null && c.attachedRigidbody != null)
            t = c.attachedRigidbody.GetComponentInChildren<Tile>();
        return t;
    }

    bool TryFindMagneticTile(out Tile tile, out Vector3 normal)
    {
        tile = null;
        normal = Vector3.up;
        Vector3 origin = transform.position + Vector3.up * magneticProbeHeight;
        float bestDist = float.MaxValue;

        foreach (Vector3 dir in ProbeDirs)
        {
            Debug.DrawRay(origin, dir * magneticRange, Color.yellow, 2f);
            if (!RaycastNearest(origin, dir, magneticRange, out RaycastHit hit)) continue;
            if (hit.distance >= bestDist) continue;

            Tile t = hit.collider.GetComponentInChildren<Tile>();
            if (t == null || !t.CanMagnetize) continue;

            Vector3 n = t.surfaceNormal.normalized;
            if (Vector3.Dot(hit.normal, n) < 0.9f) continue;   // solo la cara expuesta

            tile = t; normal = n; bestDist = hit.distance;
        }
        return tile != null;
    }

    void UpdateMagneticAttach()
    {
        if (!magneticAttaching)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            magneticAttachSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            magneticAttaching = false;
        }
    }

    void BeginMagneticDetach()
    {

        // Garantiza que la caída no arranque dentro de la pared (no es un teletransporte: solo
        // corrige penetración, normalmente de unos milímetros).
       if (magneticTile != null)
        {
            detachWallCollider = TileCollider(magneticTile);
            detachNormal = surfaceUp;
            detachFacePoint = GetSurfacePoint(magneticTile, surfaceUp);
        }
        settleTimer = 0f;

        facingDir = ComputeDetachFacing(surfaceUp);   // el avatar se endereza solo vía RotateTowardsFacing

        magneticMode = false;
        magneticTile = null;
        surfaceUp = Vector3.up;
        magneticMoving = false;
        magneticAttaching = false;
        moving = false;
        destinationTile = null;
        landingTile = null;

        fallVelocity = 0f;
        detachPhase = DetachPhase.Falling;            // la caída parte de la posición ACTUAL
    }

    Vector3 ComputeDetachFacing(Vector3 wallNormal)
    {
        Vector3 f = Vector3.ProjectOnPlane(playerAvatar.transform.forward, Vector3.up);
        if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(-wallNormal, Vector3.up);
        if (f.sqrMagnitude < 0.01f) f = Vector3.ProjectOnPlane(magneticForward, Vector3.up);
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        return f.normalized;
    }

    void UpdateMagneticDetach()
    {
        switch (detachPhase)
        {
            case DetachPhase.Falling:  StepFall();   break;
            case DetachPhase.Settling: StepSettle(); break;
        }
    }

    void StepFall()
    {
        PeelFromWall(peelOffSpeed * Time.deltaTime);

        fallVelocity = Mathf.Min(fallVelocity + gravity * Time.deltaTime, maxFallSpeed);
        float step = fallVelocity * Time.deltaTime;
        Vector3 pos = transform.position;

        float feetY = pos.y - pivotToFeet;
        Vector3 probeOrigin = new Vector3(pos.x, feetY + groundProbeUp, pos.z);

        if (TryFindGround(probeOrigin, groundProbeUp + step, out RaycastHit hit, out Tile ground))
        {
            landingTile = ground;
            Vector3 c = ground.GetTilePosition();
            landingPoint = new Vector3(c.x, hit.point.y + pivotToFeet, c.z);   // 0.5 + (-0.492) ≈ 0.008
            fallVelocity = 0f;
            settleTimer = 0f;
            detachPhase = DetachPhase.Settling;
            return;
        }

        pos.y -= step;
        transform.position = pos;

        if (pos.y < fallAbortY)
        {
            Debug.LogError("Caída sin suelo detectado; se aborta la caída.");
            detachPhase = DetachPhase.None;
            detachWallCollider = null;
        }
    }

    void StepSettle()
    {
        settleTimer += Time.deltaTime;

        // Centrado hacia el tile de aterrizaje...
        transform.position = Vector3.MoveTowards(
            transform.position, landingPoint, landingCenterSpeed * Time.deltaTime);

        // ...pero la pared siempre manda: el cuerpo nunca queda dentro.
        float remaining = PeelFromWall(float.MaxValue);

        bool atPoint = (transform.position - landingPoint).sqrMagnitude < 1e-6f;
        bool settled = (atPoint && remaining <= 0.001f && !rotating) || settleTimer > 1.5f;
        if (!settled) return;

        if (!atPoint)
            Debug.LogWarning($"[Land] El centro de {landingTile.name} queda dentro de la pared: " +
                            "el cuerpo es más ancho que el hueco. Se respeta la pared.");

            Collider gc = TileCollider(landingTile);
            Renderer[] gr = landingTile.GetComponentsInParent<Renderer>();
            float tileVisualTop = float.NaN;
            foreach (var r in gr) tileVisualTop = float.IsNaN(tileVisualTop) ? r.bounds.max.y : Mathf.Max(tileVisualTop, r.bounds.max.y);

            float modelBottom = float.NaN;
            foreach (var r in GetComponentsInChildren<Renderer>())
                modelBottom = float.IsNaN(modelBottom) ? r.bounds.min.y : Mathf.Min(modelBottom, r.bounds.min.y);

        detachPhase = DetachPhase.None;
        detachWallCollider = null;

        occupiedTile = landingTile;
        occupiedTile.Enter(gameObject);
        currentTile = landingTile;
        RefreshGroundNeighbours(landingTile);
    }

    /// Primer collider de suelo VÁLIDO bajo 'origin'. Una pared magnética nunca cuenta como suelo.
    bool TryFindGround(Vector3 origin, float distance, out RaycastHit best, out Tile tile)
    {
        best = default;
        tile = null;
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuffer, distance,
                                        groundLayers, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.distance >= bestDist) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;

            Tile t = ResolveTile(h.collider);
            if (!IsValidGround(t, h)) continue;

            best = h; tile = t; bestDist = h.distance;
        }
        return tile != null;
    }

    bool IsValidGround(Tile t, RaycastHit h)
    {
        if (t == null || !t.IsWalkable) return false;
        if (h.normal.y < groundMinNormalY) return false;           // cara lateral/inclinada
        bool isMagneticWall = t.magneticSurface &&
            Mathf.Abs(Vector3.Dot(t.surfaceNormal, Vector3.up)) < 0.5f;
        return !isMagneticWall;
    }
    /// Tile magnético cuya cara expuesta es vertical: nunca puede ser "suelo" del jugador.
    static bool IsMagneticWallTile(Tile t)
    {
        return t != null && t.magneticSurface &&
            Mathf.Abs(Vector3.Dot(t.surfaceNormal, Vector3.up)) < 0.5f;
    }

    bool GroundStateLocked =>
        magneticMode || magneticAttaching || detachPhase != DetachPhase.None;

    /// Respaldo: rellena los 4 vecinos de suelo. Si tu detector externo ya los rellena, los sobrescribirá.
    void RefreshGroundNeighbours(Tile t)
    {
        Vector3 p = t.GetTilePosition();
        float s = Tile.TileSize;
        forwardTile  = Tile.At(p + Vector3.forward * s);
        backwardTile = Tile.At(p + Vector3.back * s);
        rightTile    = Tile.At(p + Vector3.right * s);
        leftTile     = Tile.At(p + Vector3.left * s);
    }

    bool WallInDirection(Vector3 worldDir)
    {
        Vector3 local = transform.InverseTransformDirection(worldDir);

        Tile wall = Mathf.Abs(local.z) > Mathf.Abs(local.x)
            ? (local.z > 0f ? wallForwardTile : wallBackwardTile)
            : (local.x > 0f ? wallRightTile : wallLeftTile);

        // isActiveAndEnabled: si la pared se desactiva (puerta que se abre), deja de bloquear
        return wall != null && wall.isActiveAndEnabled;
    }

    float CalibratePivotToFeet()
    {
        float minY = float.MaxValue;
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            minY = Mathf.Min(minY, r.bounds.min.y);
            any = true;
        }
        if (any) return transform.position.y - minY;

        Collider c = GetComponentInChildren<Collider>();
        if (c != null) return transform.position.y - c.bounds.min.y;

        Debug.LogWarning("No se pudo calibrar pivotToFeet; usando 0.");
        return 0f;
    }

    bool TryGetBodyBounds(out Bounds b)
    {
        if (useModelBounds)
        {
            Renderer[] rs = GetComponentsInChildren<Renderer>();
            bool any = false;
            b = default;
            foreach (Renderer r in rs)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            if (any) return true;
        }

        if (bodyCollider == null) bodyCollider = GetComponentInChildren<Collider>();
        if (bodyCollider != null) { b = bodyCollider.bounds; return true; }

        b = default;
        return false;
    }

    /// Distancia pivote → cara del cuerpo que mira a la pared, medida en la dirección n.
    float StandOffsetAlong(Vector3 n)
    {
        if (!autoSurfaceOffset) return magneticSurfaceOffset;

        if (TryGetBodyBounds(out Bounds b))
        {
            float extent = Mathf.Abs(n.x) * b.extents.x
                        + Mathf.Abs(n.y) * b.extents.y
                        + Mathf.Abs(n.z) * b.extents.z;
            float d = extent + Vector3.Dot(transform.position - b.center, n);
            return Mathf.Max(d, 0.01f) + skin;
        }
        return Mathf.Max(pivotToFeet, 0.01f);
    }

    /// Distancia con signo pivote → cara de la pared con los pies tocándola.
    /// Con el pivote bajo los pies (pivotToFeet < 0) es negativa: el pivote queda dentro de la pared.
    float SurfaceStandOffset => autoSurfaceOffset ? pivotToFeet + skin : magneticSurfaceOffset;

    float PeelFromWall(float maxMove)
    {
        if (detachWallCollider == null) return 0f;

        float d = Vector3.Dot(transform.position - detachFacePoint, detachNormal);
        float deficit = StandOffsetAlong(detachNormal) - d;
        if (deficit <= 0.001f) return 0f;

        // Ya por debajo/encima de la pared: nada que separar.
        if (TryGetBodyBounds(out Bounds b) && !b.Intersects(detachWallCollider.bounds))
            return 0f;

        float move = Mathf.Min(deficit, maxMove);
        transform.position += detachNormal * move;
        return deficit - move;
    }

    static Collider TileCollider(Tile t)
    {
        Collider fallback = null;
        float minSize = Tile.TileSize * 0.9f;

        // Orden: el propio objeto primero, luego sus ancestros (el más cercano antes).
        foreach (Collider c in t.GetComponentsInParent<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;

            Vector3 s = c.bounds.size;
            if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) >= minSize)
                return c;                       // primer collider sólido del tamaño de un tile

            if (fallback == null) fallback = c; // marcador pequeño: solo como último recurso
        }
        return fallback != null ? fallback : t.GetComponentInChildren<Collider>();
    }
    static Vector3 TileCenter(Tile t)
    {
        Collider c = TileCollider(t);
        return (c != null && c.enabled) ? c.bounds.center : t.transform.position;
    }

    /// Punto real de la cara expuesta (centro de la cara), medido con el propio collider.
    static Vector3 GetSurfacePoint(Tile t, Vector3 normal)
    {
        Collider c = TileCollider(t);
        if (c == null || !c.enabled) return t.transform.position;

        Vector3 center = c.bounds.center;
        float reach = c.bounds.size.magnitude + 1f;
        Ray ray = new Ray(center + normal * reach, -normal);
        return c.Raycast(ray, out RaycastHit hit, reach * 2f) ? hit.point : center;
    }

    Vector3 ComputeMagneticPivot(Tile tile, Vector3 normal)
    {
        return GetSurfacePoint(tile, normal) + normal * SurfaceStandOffset;
    }

    public void SetLeftTile(Tile tile)     { if (AcceptGroundTile(tile)) leftTile = tile; }
    public void SetRightTile(Tile tile)    { if (AcceptGroundTile(tile)) rightTile = tile; }
    public void SetForwardTile(Tile tile)  { if (AcceptGroundTile(tile)) forwardTile = tile; }
    public void SetBackwardTile(Tile tile) { if (AcceptGroundTile(tile)) backwardTile = tile; }
    public void SetCurrentTile(Tile tile)  { if (AcceptGroundTile(tile)) currentTile = tile; }

    /// Rechaza escrituras del detector mientras escalas/caes, y tiles de pared siempre.
    bool AcceptGroundTile(Tile tile)
    {
        if (tile == null) return !GroundStateLocked;   // limpiar solo si no estamos bloqueados
        if (GroundStateLocked) return false;
        if (IsMagneticWallTile(tile)) return false;
        return true;
    }
}