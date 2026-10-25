
using UnityEngine;
using UnityEngine.Events;

public class Diana : MonoBehaviour
{
    [Header("Evento de impacto")]
    [SerializeField] private UnityEvent dianaHit;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ball"))
        {
            // Ejecuta las acciones configuradas en el Inspector.
            dianaHit?.Invoke();
        }
    }
}
