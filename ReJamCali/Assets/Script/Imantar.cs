using UnityEngine;

public class OtroScript : MonoBehaviour
{
    [SerializeField] private RayHit rayHit;

    void OnEnable()
    {
        rayHit.OnDeteccionCambiada += AlCambiarDeteccion;
    }

    void OnDisable()
    {
        rayHit.OnDeteccionCambiada -= AlCambiarDeteccion;
    }

    private void AlCambiarDeteccion(bool detectado)
    {
        if (detectado)
            Debug.Log("Hay un objeto metálico en frente");
        else
            Debug.Log("Ya no hay objeto metálico");
    }
}