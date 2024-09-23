using UnityEngine;

public class SeekerController : MonoBehaviour
{
    private SeekerAgent seekerAgent;

    private void Start()
    {
        seekerAgent = GetComponentInChildren<SeekerAgent>(true);
    }

    private void OnCollisionEnter(Collision other)
    {
        seekerAgent.HandleOnCollisionEnter(other);
    }

    // private void OnCollisionStay(Collision other)
    // {
    //     seekerAgent.HandleOnCollisionStay(other);
    // }
}
