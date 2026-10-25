using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Condición de victoria. Poner en el objeto clave (con un Collider en modo Trigger).
/// Cuando el robot termina de llegar a la casilla del objeto:
///  1) El objeto sube una distancia fija.
///  2) El robot da giros, termina mirando a la cámara y se eleva como si flotara (todo por código).
///  3) Se activa el Canvas de victoria.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WinCondition : MonoBehaviour
{
    [Header("Detección")]
    [Tooltip("Respaldo: se acepta también cualquier collider con esta etiqueta.")]
    public string playerTag = "Player";

    [Header("Movimiento del objeto")]
    [Tooltip("Distancia (en unidades del mundo) que sube el objeto.")]
    public float moveDistance = 1f;
    public float moveDuration = 0.8f;
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Celebración del jugador (por código)")]
    [Tooltip("Desactiva PlayerMovement cuando el robot llega a la casilla.")]
    public bool disablePlayerMovement = true;
    [Min(1)] public int spins = 2;
    [Tooltip("Segundos que dura el giro y la elevación.")]
    public float celebrationDuration = 1.6f;
    public AnimationCurve spinCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Altura a la que se eleva el robot sobre el suelo.")]
    public float floatHeight = 0.4f;
    [Tooltip("Oscilación suave mientras flota (0 = quieto).")]
    public float bobAmplitude = 0.05f;
    public float bobSpeed = 2f;
    [Tooltip("Cámara hacia la que mira al final. Si está vacía, usa Camera.main.")]
    public Camera targetCamera;

    [Header("Canvas de victoria")]
    public GameObject winCanvas;
    [Tooltip("Segundos de espera tras la celebración antes de mostrar el canvas.")]
    public float canvasDelay = 0.3f;

    [Header("Eventos")]
    [Tooltip("Se lanza en cuanto el robot llega a la casilla, ANTES de la celebración. Aquí va el audio de victoria.")]
    public UnityEvent onWinStart;
    [Tooltip("Se lanza al final, cuando aparece el canvas.")]
    public UnityEvent onWin;

    bool won;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Start()
    {
        if (winCanvas != null) winCanvas.SetActive(false);
    }

    // Stay (no Enter): se reevalúa en cada paso de física hasta que el robot
    // haya terminado de moverse y esté realmente sobre esta casilla.
    void OnTriggerStay(Collider other)
    {
        if (won) return;

        PlayerMovement movement = other.GetComponentInParent<PlayerMovement>();
        bool isPlayer = movement != null || other.CompareTag(playerTag);
        if (!isPlayer) return;

        if (movement != null && movement.moving) return;   // esperar a que acabe el paso

        Transform playerTf = movement != null ? movement.transform : other.transform;
        if (!SameCell(playerTf.position, transform.position)) return;   // debe estar sobre la casilla

        won = true;
        onWinStart?.Invoke();
        StartCoroutine(WinRoutine(movement, playerTf));
    }

    IEnumerator WinRoutine(PlayerMovement movement, Transform fallbackModel)
    {
        // El robot ya está centrado en la casilla: ahora sí se puede congelar.
        if (disablePlayerMovement && movement != null)
            movement.enabled = false;

        // 1) El objeto sube.
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * moveDistance;
        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float k = moveDuration > 0f ? Mathf.Clamp01(t / moveDuration) : 1f;
            transform.position = Vector3.LerpUnclamped(start, end, moveCurve.Evaluate(k));
            yield return null;
        }
        transform.position = end;

        // 2) Celebración del robot.
        yield return PlayerCelebration(movement, fallbackModel);

        // 3) Canvas de victoria.
        if (canvasDelay > 0f) yield return new WaitForSeconds(canvasDelay);
        if (winCanvas != null) winCanvas.SetActive(true);
        onWin?.Invoke();
    }

    IEnumerator PlayerCelebration(PlayerMovement movement, Transform fallbackModel)
    {
        // Se mueve el modelo (avatar), no la raíz, para no desalinear al robot del grid.
        Transform model = (movement != null && movement.playerAvatar != null)
            ? movement.playerAvatar.transform
            : fallbackModel;
        if (model == null) yield break;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        Quaternion startRot = model.rotation;
        Vector3 startPos = model.position;

        // Giro total = 'spins' vueltas completas + lo necesario para acabar mirando a la cámara.
        float extra = 0f;
        bool hasFinalRot = false;
        Quaternion finalRot = startRot;

        if (cam != null)
        {
            Vector3 toCam = cam.transform.position - model.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
            {
                Vector3 f = model.forward;
                f.y = 0f;
                float startYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                float targetYaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
                extra = Mathf.Repeat(targetYaw - startYaw, 360f);

                finalRot = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                hasFinalRot = true;
            }
        }

        float totalAngle = 360f * spins + extra;

        float t = 0f;
        while (t < celebrationDuration)
        {
            t += Time.deltaTime;
            float k = celebrationDuration > 0f ? Mathf.Clamp01(t / celebrationDuration) : 1f;

            float angle = totalAngle * spinCurve.Evaluate(k);
            model.rotation = Quaternion.AngleAxis(angle, Vector3.up) * startRot;
            model.position = startPos + Vector3.up * (floatHeight * Mathf.SmoothStep(0f, 1f, k));

            yield return null;
        }

        model.rotation = hasFinalRot ? finalRot : Quaternion.AngleAxis(totalAngle, Vector3.up) * startRot;
        Vector3 floatPos = startPos + Vector3.up * floatHeight;
        model.position = floatPos;

        // Flotación suave que continúa mientras se muestra el canvas.
        if (bobAmplitude > 0f)
            StartCoroutine(Bob(model, floatPos));
    }

    IEnumerator Bob(Transform model, Vector3 basePos)
    {
        float t0 = Time.time;
        while (model != null)
        {
            float offset = Mathf.Sin((Time.time - t0) * bobSpeed) * bobAmplitude;
            model.position = basePos + Vector3.up * offset;
            yield return null;
        }
    }

    /// Misma casilla del grid (usa el registro de Tile; si no hay tile, compara distancia plana).
    static bool SameCell(Vector3 a, Vector3 b)
    {
        Tile ta = Tile.At(a);
        Tile tb = Tile.At(b);
        if (ta != null && tb != null) return ta == tb;

        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b) < Tile.TileSize * 0.5f;
    }
}