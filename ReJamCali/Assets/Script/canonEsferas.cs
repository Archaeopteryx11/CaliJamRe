using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class canonEsferas : MonoBehaviour
{
    [SerializeField] private GameObject esferaPrefab;
    [SerializeField] private Transform puntoDisparo;
    [SerializeField] private float fuerza = 15f;
    [SerializeField] private float tiempoVida = 5f;
    [SerializeField] private SelectorModulos seleccionModulo;
    private bool disparoActivo = true;
    void OnEnable()
    {
        seleccionModulo.OnDeteccionCambiada += disparo;
    }

    void OnDisable()
    {
        seleccionModulo.OnDeteccionCambiada -= disparo;
    }

    public void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame || disparoActivo)
        {
            Disparar();
        }
    }
    public void Disparar()
    {
        Transform origen = puntoDisparo != null ? puntoDisparo : transform;

        GameObject esfera = Instantiate(esferaPrefab, origen.position, origen.rotation);

        Rigidbody rb = esfera.GetComponent<Rigidbody>();
        if (rb == null) rb = esfera.AddComponent<Rigidbody>();

        rb.linearVelocity = origen.forward * fuerza;

        Destroy(esfera, tiempoVida);
    }
    public void disparo(bool activar)
    {
        if(activar)
        {
            disparoActivo = false;
        }
        else
        {
            disparoActivo = true;
        }
    }
}