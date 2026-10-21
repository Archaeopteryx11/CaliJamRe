using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BuscadorPorTag : MonoBehaviour
{
    [SerializeField] private string tagBuscado;
    [SerializeField] private List<GameObject> objetosEncontrados = new List<GameObject>();
    private int indiceActual = 0;
    [SerializeField] private SelectorModulos seleccionModulo;
    private bool activarInventario = false;
    [SerializeField] private GameObject playerMovement;
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
        if (indiceActual == objetosEncontrados.Count)
        {
            indiceActual = 0;
        }
        else if (indiceActual == -1)
        {
            indiceActual = objetosEncontrados.Count - 1;
        }

        if (scroll > 0)
        {
            indiceActual++;
        }
        else if (scroll < 0)
        {
            indiceActual--;
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
        if (!activarInventario)
        {
            seleccionRueda();
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                objetosEncontrados[indiceActual].GetComponent<PlayerMovement>().enabled = true;
                playerMovement.GetComponent<PlayerMovement>().enabled = false;
            }
            else
            {
                objetosEncontrados[indiceActual].GetComponent<PlayerMovement>().enabled = false;
                playerMovement.GetComponent<PlayerMovement>().enabled = true;
            }
        }
    }
}
