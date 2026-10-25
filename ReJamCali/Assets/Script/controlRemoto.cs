using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ControlRemoto : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerMovement principal;      // vacío = se busca por el tag "Player"
    [SerializeField] private CameraFollow camara;           // vacío = se busca en la escena
    [SerializeField] private SelectorModulos seleccionModulo;

    [Header("Dispositivos")]
    [SerializeField] private string tagBuscado = "Dispositivo";
    [SerializeField] private Key teclaConectar = Key.Enter;
    [SerializeField] private Key teclaSalir = Key.Escape;

    private readonly List<PlayerMovement> dispositivos = new List<PlayerMovement>();
    private int indice;
    private bool modoRemoto;       // eligiendo o conectado
    private bool conectado;        // controlando un dispositivo
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

    /// Conéctalo al UnityEvent del módulo de conexión inalámbrica en el SelectorModulos.
    public void activo()
    {
        if (modoRemoto) Salir();
        else EntrarModoRemoto();
    }

    void Update()
    {
        if (!modoRemoto) return;

        dispositivos.RemoveAll(d => d == null);
        if (dispositivos.Count == 0) { Salir(); return; }
        indice = Mathf.Clamp(indice, 0, dispositivos.Count - 1);

        // Con el inventario abierto la rueda es del selector de módulos.
        if (seleccionModulo != null && seleccionModulo.activarInventario) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb[teclaSalir].wasPressedThisFrame)
        {
            if (PuedeCambiar()) Salir();
            return;
        }

        if (conectado) return;

        ElegirConRueda();
        if (kb[teclaConectar].wasPressedThisFrame) Conectar(indice);
    }

    // ---------------- Estados ----------------
    void EntrarModoRemoto()
    {
        if (dispositivos.Count == 0)
        {
            Debug.LogWarning("ControlRemoto: no hay dispositivos con el tag '" + tagBuscado + "'.", this);
            return;
        }
        if (principal.moving) return;          // espera a que termine el paso

        modoRemoto = true;
        conectado = false;
        ActivarSolo(null);                     // nadie se mueve mientras se elige
        if (seleccionModulo != null) seleccionModulo.CerrarInventario();
        Enfocar(dispositivos[indice].transform);
        Debug.Log("Modo remoto: rueda para elegir, " + teclaConectar + " para conectar, " + teclaSalir + " para salir.");
    }

    void Conectar(int i)
    {
        indice = i;
        conectado = true;
        ActivarSolo(dispositivos[i]);          // desactiva al principal y a todos los demás
        Enfocar(dispositivos[i].transform);
        Debug.Log("Conectado a: " + dispositivos[i].name);
    }

    void Salir()
    {
        modoRemoto = false;
        conectado = false;
        ActivarSolo(principal);
        Enfocar(objetivoOriginal);             // lo que seguía la cámara antes (null = diorama fijo)
        Debug.Log("Control devuelto al robot principal.");
    }

    // ---------------- Utilidades ----------------
    void ElegirConRueda()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        int n = dispositivos.Count;
        indice += scroll > 0f ? 1 : -1;
        indice = ((indice % n) + n) % n;

        Enfocar(dispositivos[indice].transform);
        Debug.Log("Dispositivo seleccionado: " + dispositivos[indice].name);
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
        PlayerMovement actual = conectado ? dispositivos[indice] : principal;
        return actual == null || !actual.moving;
    }

    void Enfocar(Transform objetivo)
    {
        if (camara != null) camara.followTarget = objetivo;
    }
}