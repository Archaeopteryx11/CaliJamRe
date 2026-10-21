using System;
using UnityEngine;

public class RayHit : MonoBehaviour
{
    [SerializeField] private LayerMask superficieMetalica;
    private RaycastHit hit;
    [SerializeField] private float distancia = 5f;
    public bool objetoMetalicoDetectado { get; private set; } = false;
    public event Action<bool> OnDeteccionCambiada;
    void Start()
    {
        
    }
    void Update()
    {
        DetectarObjetoMetalico();
    }
    public void DetectarObjetoMetalico()
    {
        if (Physics.Raycast(transform.position, transform.forward, out hit, distancia, superficieMetalica))
        {
            objetoMetalicoDetectado = true;
            Debug.Log("Objeto metálico detectado: " + hit.collider.name);
        }
        else
        {
            objetoMetalicoDetectado = false;
        }
    }
}
