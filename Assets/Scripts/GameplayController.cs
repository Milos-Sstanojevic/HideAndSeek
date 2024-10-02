using UnityEngine;

public class GameplayController : MonoBehaviour
{
    public const float RewardForFindingHider = 1f;
    public const float HidingTime = 10f;
    public const float SeekingTime = 20f;
    public const float StartingTime = 0f;

    [SerializeField] private HiderAgent hiderAgent;
    [SerializeField] private SeekerAgent seekerAgent;
    [SerializeField] private MeshRenderer groundMesh;
    [SerializeField] private Material winMaterial;
    [SerializeField] private Material defaultMaterial;

    private float currentTime = 0f;
    private bool isHiding = false;
    private bool isSeeking = false;

    private void OnEnable()
    {
        seekerAgent.OnFoundAction += FoundHider;
    }

    private void Start()
    {
        StartHidingPhase();
    }

    private void Update()
    {
        if (isHiding)
        {
            currentTime += Time.deltaTime;
            if (currentTime >= HidingTime)
            {
                StartSeekingPhase();
            }
        }
        else if (isSeeking)
        {
            currentTime += Time.deltaTime;
            if (currentTime >= SeekingTime)
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
        hiderAgent.AgentIsNotSeeking();
        currentTime = StartingTime;
        isHiding = true;
        isSeeking = false;
    }

    private void StartSeekingPhase()
    {
        seekerAgent.StartAgent();
        hiderAgent.AgentIsSeeking();
        currentTime = StartingTime;
        isHiding = false;
        isSeeking = true;
    }

    private void FoundHider(int episodeCounter)
    {
        seekerAgent.AddReward(RewardForFindingHider);
        hiderAgent.HandleAgentFound(currentTime);

        ChangeColorOfGround(episodeCounter);

        StartHidingPhase();
    }

    private void ChangeColorOfGround(int episodeCounter)
    {
        if (episodeCounter % 2 == 0)
            groundMesh.material = winMaterial;
        else
            groundMesh.material = defaultMaterial;
    }

    private void OnDisable()
    {
        seekerAgent.OnFoundAction -= FoundHider;
    }
}
