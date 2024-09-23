using UnityEngine;

public class GameplayController : MonoBehaviour
{
    [SerializeField] private HiderAgent hiderAgent;
    [SerializeField] private SeekerAgent seekerAgent;
    [SerializeField] private MeshRenderer groundMesh;
    [SerializeField] private Material winMaterial;
    [SerializeField] private Material defaultMaterial;
    private Collider seekerCollider;

    private float hidingTime = 10f;
    private float seekingTime = 20f;
    private float currentTime = 0f;
    private bool isHiding = false;
    private bool isSeeking = false;

    private void OnEnable()
    {
        seekerAgent.OnFoundAction += FoundHider;
    }

    private void Start()
    {
        seekerCollider = seekerAgent.GetComponentInParent<Collider>();
        StartHidingPhase();
    }

    private void Update()
    {
        if (isHiding)
        {
            currentTime += Time.deltaTime;
            if (currentTime >= hidingTime)
            {
                StartSeekingPhase();
            }
        }
        else if (isSeeking)
        {
            currentTime += Time.deltaTime;
            if (currentTime >= seekingTime)
            {
                seekerAgent.StopAgent();
                seekerAgent.EndEpisode();
                hiderAgent.EndEpisode();
                StartHidingPhase();
            }
        }
    }

    private void StartHidingPhase()
    {
        seekerAgent.StopAgent();
        // hiderAgent.StartAgent();
        hiderAgent.AgentIsNotSeeking();
        // seekerCollider.enabled = false;
        currentTime = 0f;
        isHiding = true;
        isSeeking = false;
    }

    private void StartSeekingPhase()
    {
        seekerAgent.StartAgent();
        // hiderAgent.StopAgent();
        hiderAgent.AgentIsSeeking();
        // seekerCollider.enabled = true;
        currentTime = 0f;
        isHiding = false;
        isSeeking = true;
    }

    private void FoundHider(int episodeCounter)
    {
        seekerAgent.AddReward(1f);
        hiderAgent.HandleAgentFound(currentTime);

        if (episodeCounter % 2 == 0)
            groundMesh.material = winMaterial;
        else
            groundMesh.material = defaultMaterial;

        StartHidingPhase();
    }

    private void OnDisable()
    {
        seekerAgent.OnFoundAction -= FoundHider;
    }
}
