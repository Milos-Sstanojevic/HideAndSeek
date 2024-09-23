using UnityEngine;

public class HiderController : MonoBehaviour
{
    private HiderAgent hiderAgent;

    private void Start()
    {
        hiderAgent = GetComponentInChildren<HiderAgent>(true);
    }

    private void OnCollisionEnter(Collision other)
    {
        hiderAgent.HandleOnCollisionEnter(other);
    }
}
