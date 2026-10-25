using UnityEngine;
using UnityEngine.InputSystem;

public class canonEsferas : MonoBehaviour
{
    [SerializeField] private GameObject esferaPrefab;
    [SerializeField] private Transform puntoDisparo;
    [SerializeField] private float fuerza = 15f;
    [SerializeField] private float tiempoVida = 5f;
    [SerializeField] private Animator animator;

    private static readonly int DisparoHash = Animator.StringToHash("Shot");
    private bool disparoActivo = false;
    private bool disparando = false;        // true desde que empieza la animaci�n hasta que termina

    public void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame || disparoActivo)
            IniciarDisparo();
    }

    void IniciarDisparo()
    {
        if (disparando) return;             // ya hay un disparo en curso
        disparando = true;

        if (animator != null)
        {
            animator.SetTrigger(DisparoHash);   // la esfera saldr� cuando la animaci�n llame a EmitirEsfera()
        }
        else
        {
            EmitirEsfera();                     // sin animator: dispara al instante
            FinDisparo();
        }
    }

    public void EmitirEsfera()
    {
        Transform origen = puntoDisparo != null ? puntoDisparo : transform;

        GameObject esfera = Instantiate(esferaPrefab, origen.position, origen.rotation);

        Rigidbody rb = esfera.GetComponent<Rigidbody>();
        if (rb == null) rb = esfera.AddComponent<Rigidbody>();

        rb.useGravity = false;
        
        rb.linearVelocity = origen.forward * fuerza;

        Destroy(esfera, tiempoVida);
    }

    public void FinDisparo()
    {
        disparando = false;
    }

    public void disparo(bool activar)
    {
        disparoActivo = activar;
    }
}