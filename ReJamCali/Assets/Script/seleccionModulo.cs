using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;


public class SelectorModulos : MonoBehaviour
{
    [SerializeField] private UnityEvent[] evento;
    int indiceActual = 0;
    public bool activarInventario { get; private set; } = false;
    public event Action<bool> OnDeteccionCambiada;

    void Update()
    {
        invetario();
        if (activarInventario)
        {
            seleccionarModulo();
            seleccionRueda();
            ejecutarEvento();
        }
    }
    private void Start()
    {
        indiceActual = 0;
    }

    public void seleccionarModulo()
    {
        var teclado = Keyboard.current;
        if (teclado == null) return;

        if (teclado.digit1Key.wasPressedThisFrame || teclado.numpad1Key.wasPressedThisFrame)
        {
            indiceActual = 0;
            Debug.Log("Modulo seleccionado: " + (indiceActual + 1));
        }
        else if (teclado.digit2Key.wasPressedThisFrame || teclado.numpad2Key.wasPressedThisFrame)
        {
            indiceActual = 1;
            Debug.Log("Modulo seleccionado: " + (indiceActual + 1));
        }
        else if (teclado.digit3Key.wasPressedThisFrame || teclado.numpad3Key.wasPressedThisFrame)
        {
            indiceActual = 2;
            Debug.Log("Modulo seleccionado: " + (indiceActual + 1));
        }
    }
    public void seleccionRueda()
    {
        var rueda = Mouse.current;
        if (rueda == null) return;
        float scroll = rueda.scroll.ReadValue().y;
        if (indiceActual == 3)
        {
            indiceActual = 0;
        }
        else if (indiceActual == -1)
        {
            indiceActual = 2;
        }

        if (scroll > 0)
        {
            indiceActual++;
            Debug.Log("Modulo seleccionado: " + (indiceActual + 1));
        }
        else if (scroll < 0)
        {
            indiceActual--;
            Debug.Log("Modulo seleccionado: " + (indiceActual + 1));
        }
        
    }
    public void ejecutarEvento()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (evento[indiceActual] != null)
            {
                evento[indiceActual].Invoke();
                Debug.Log("Evento " + evento[indiceActual].ToString() + " ejecutado");
            }
        }
    }
    public void invetario()
    {
        if(Keyboard.current.tabKey.wasPressedThisFrame)
        {
            activarInventario = !activarInventario;
            Debug.Log("Inventario " + (activarInventario ? "activado" : "desactivado"));
        }
    }
}