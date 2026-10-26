using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ControlRemoto : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerMovement principal;      // vacío = se busca por el tag "Player"
    [SerializeField] private CameraFollow camara;           // vacío = se busca en la escena

    [Header("Dispositivos")]
    [SerializeField] private string tagBuscado = "Dispositivo";
    [Tooltip("Entra al modo remoto y pasa al siguiente robot (Shift + tecla = el anterior). Tras el último, vuelve al principal.")]
    [SerializeField] private Key teclaCambiar = Key.Tab;

    private readonly List<PlayerMovement> dispositivos = new List<PlayerMovement>();
    private int indice;
    private bool modoRemoto;       // controlando un dispositivo
    private Transform objetivoOriginal;

    void Start()
    {
        if (principal == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) principal = go.GetComponent<PlayerMovement>();
        }
        if (principal == null)
        {
            Debug.LogError("ControlRemoto: no encuentro el PlayerMovement del robot principal (asígnalo o ponle el tag Player).", this);
            enabled = false;
            return;
        }

        if (camara == null) camara = FindFirstObjectByType<CameraFollow>();
        if (camara != null)
        {
            objetivoOriginal = camara.followTarget;

            if (objetivoOriginal == null)      // diorama fijo: crea un ancla en el sitio original
            {
                var ancla = new GameObject("AnclaCamara");
                ancla.transform.position = camara.transform.position;
                objetivoOriginal = ancla.transform;
            }
        }
        BuscarObjetos();
        ActivarSolo(principal);    // estado inicial: manda el principal
    }

    public void BuscarObjetos()
    {
        dispositivos.Clear();

        GameObject[] encontrados;
        try { encontrados = GameObject.FindGameObjectsWithTag(tagBuscado); }
        catch (UnityException)
        {
            Debug.LogError("ControlRemoto: el tag '" + tagBuscado + "' no existe. Créalo en Tags & Layers.", this);
            return;
        }

        foreach (var go in encontrados)
        {
            var pm = go.GetComponent<PlayerMovement>();
            if (pm != null && pm != principal) dispositivos.Add(pm);
        }
        dispositivos.Sort((a, b) => string.Compare(a.name, b.name));
        indice = Mathf.Clamp(indice, 0, Mathf.Max(0, dispositivos.Count - 1));

        Debug.Log("ControlRemoto: " + dispositivos.Count + " dispositivo(s) encontrados.");
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[teclaCambiar].wasPressedThisFrame)
            Debug.Log("Tab detectada | modoRemoto=" + modoRemoto + " | Remoto permitido=" + HabilidadesNivel.Remoto + " | dispositivos=" + dispositivos.Count);

        // Fuera del modo remoto, Tab entra (si el nivel lo permite).
        if (!modoRemoto)
        {
            if (HabilidadesNivel.Remoto && kb[teclaCambiar].wasPressedThisFrame)
                EntrarModoRemoto();
            return;
        }

        dispositivos.RemoveAll(d => d == null);
        if (dispositivos.Count == 0) { Salir(); return; }

        // TAB recorre: principal -> dispositivo 1 -> ... -> último -> principal (Shift+TAB al revés).
        if (kb[teclaCambiar].wasPressedThisFrame && PuedeCambiar())
        {
            int siguiente = indice + (kb.shiftKey.isPressed ? -1 : 1);
            if (siguiente < 0 || siguiente >= dispositivos.Count) Salir();   // pasó del último (o del primero): vuelve al principal
            else Conectar(siguiente);
        }
    }

    void EntrarModoRemoto()
    {
        if (dispositivos.Count == 0) return;     // nivel sin dispositivos: Tab no hace nada
        if (principal.moving) return;

        modoRemoto = true;
        Conectar(0);                             // siempre empieza por el primer dispositivo
    }
    void Conectar(int i)
    {
        int n = dispositivos.Count;
        if (n == 0) return;
        indice = Mathf.Clamp(i, 0, n - 1);

        ActivarSolo(dispositivos[indice]);
        Enfocar(dispositivos[indice].transform);
        Debug.Log("Controlando: " + dispositivos[indice].name + " (" + (indice + 1) + "/" + n + ")");
    }


    /// Solo "quien" puede moverse; el principal y todos los dispositivos restantes quedan desactivados.
    void ActivarSolo(PlayerMovement quien)
    {
        if (principal != null) principal.enabled = (principal == quien);
        foreach (var d in dispositivos)
            if (d != null) d.enabled = (d == quien);

        Debug.Log("ActivarSolo ? " + (quien != null ? quien.name : "NADIE")
            + " | principal.enabled=" + (principal != null && principal.enabled), this);
    }

    /// No se cambia de robot a mitad de una casilla.
    bool PuedeCambiar()
    {
        PlayerMovement actual = modoRemoto && indice < dispositivos.Count ? dispositivos[indice] : principal;
        return actual == null || !actual.moving;
    }

    void Enfocar(Transform objetivo)
    {
        if (camara != null) camara.followTarget = objetivo;
    }

    public void Salir()
    {
        modoRemoto = false;

        ActivarSolo(principal);
        Enfocar(objetivoOriginal);
        Debug.Log("Control devuelto al robot principal.");
    }
}