using UnityEngine;
using UnityEngine.InputSystem;

/// Cámara isométrica ortográfica con 4 vistas (90°). Q / E rotan.
/// Este objeto es el PIVOTE (gira en Y); la cámara queda como hija.
public class CameraFollow : MonoBehaviour
{
    [Header("Ángulos")]
    [Tooltip("35.264 = isométrico verdadero. 30 = look clásico de videojuego.")]
    public float pitch = 35.264f;
    public float baseYaw = 45f;

    [Header("Encuadre")]
    public float distance = 40f;
    public float orthoSize = 6f;
    public float rotateSmoothTime = 0.12f;

    [Header("Seguimiento (opcional)")]
    [Tooltip("Vacío = el pivote se queda donde lo pongas (diorama fijo).")]
    public Transform followTarget;
    public float followSmoothTime = 0.15f;

    /// Vista objetivo normalizada 0..3. La usa el PlayerController.
    public int View { get { return ((yawIndex % 4) + 4) % 4; } }

    Camera cam;
    InputAction rotateLeft, rotateRight;
    int yawIndex;                 // sin normalizar, para que nunca gire "por el lado largo"
    float currentYaw, yawVel;
    Vector3 posVel;

    float TargetYaw { get { return baseYaw + 90f * yawIndex; } }

    void Awake()
    {
        cam = GetComponentInChildren<Camera>();
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.transform.SetParent(transform, false);
        }

        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = distance * 3f;

        Quaternion tilt = Quaternion.Euler(pitch, 0f, 0f);
        cam.transform.localRotation = tilt;
        cam.transform.localPosition = tilt * new Vector3(0f, 0f, -distance);

        currentYaw = TargetYaw;
        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        rotateLeft = new InputAction("RotateLeft", InputActionType.Button, "<Keyboard>/q");
        rotateRight = new InputAction("RotateRight", InputActionType.Button, "<Keyboard>/e");
    }

    void OnEnable() { rotateLeft.Enable(); rotateRight.Enable(); }
    void OnDisable() { rotateLeft.Disable(); rotateRight.Disable(); }
    void OnDestroy() { rotateLeft.Dispose(); rotateRight.Dispose(); }

    void Update()
    {
        if (rotateLeft.WasPressedThisFrame()) yawIndex--;
        if (rotateRight.WasPressedThisFrame()) yawIndex++;

        currentYaw = Mathf.SmoothDampAngle(currentYaw, TargetYaw, ref yawVel, rotateSmoothTime);
        transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        if (followTarget != null)
            transform.position = Vector3.SmoothDamp(transform.position, followTarget.position, ref posVel, followSmoothTime);
    }
}