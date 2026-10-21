using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BuscadorPorTag : MonoBehaviour
{
    [SerializeField] private string tagBuscado;
    private List<GameObject> objetosEncontrados = new List<GameObject>();
    private int indiceActual = 0;
    private SelectorModulos seleccionModulo;
    private bool activarInventario = false;
    private GameObject playerMovement;
    private bool activar = false;
    void OnEnable()
    {
        seleccionModulo.OnDeteccionCambiada += controlar;
    }

    void OnDisable()
    {
        seleccionModulo.OnDeteccionCambiada -= controlar;
    }
    void Start()
    {
        BuscarObjetos();
        playerMovement = GameObject.FindGameObjectWithTag("Player");
    }

    public void BuscarObjetos()
    {
        objetosEncontrados = new List<GameObject>(GameObject.FindGameObjectsWithTag(tagBuscado));
        Debug.Log("Encontrados: " + objetosEncontrados.Count);
    }
    public void seleccionRueda()
    {
        var rueda = Mouse.current;
        if (rueda == null) return;
        float scroll = rueda.scroll.ReadValue().y;
        if (scroll > 0)
        {
            indiceActual++;
            Debug.Log("Modulo seleccionado: " + (indiceActual));
        }
        else if (scroll < 0)
        {
            indiceActual--;
            Debug.Log("Modulo seleccionado: " + (indiceActual));
        }
        if (indiceActual >= objetosEncontrados.Count)
        {
            indiceActual = 0;
        }
        else if (indiceActual < 0)
        {
            indiceActual = objetosEncontrados.Count - 1;
        }

    }
    public void controlar(bool activar)
    {
        if (activar)
        {
            activarInventario = true;
        }
        else
        {
            activarInventario = false;
        }
    }
    public void Update()
    {
        if (!activarInventario && activar)
        {
            seleccionRueda();
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                objetosEncontrados[indiceActual].GetComponent<PlayerMovement>().enabled = true;
                playerMovement.GetComponent<PlayerMovement>().enabled = false;
                Debug.Log("Control remoto " + indiceActual);
            }
            else if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                objetosEncontrados[indiceActual].GetComponent<PlayerMovement>().enabled = false;
                playerMovement.GetComponent<PlayerMovement>().enabled = true;
                Debug.Log("Control remoto " + indiceActual);
            }
        }
        if (!activar)
        {
            objetosEncontrados[indiceActual].GetComponent<PlayerMovement>().enabled = false;
            playerMovement.GetComponent<PlayerMovement>().enabled = true;
            Debug.Log("Control remoto " + indiceActual);
        }
    }
    public void activo()
    {
        activar = !activar;
        Debug.Log("Control remoto: " + activar);
    }
   
}
