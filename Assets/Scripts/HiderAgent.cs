using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class HiderAgent : Agent
{
    [SerializeField] private SeekerAgent seekerAgent;
    [SerializeField] private LayerMask hidingWallMask;
    [SerializeField] private RayPerceptionSensorComponent3D raySensorFront;
    [SerializeField] private float movementSpeed;
    [SerializeField] private float rotationSpeed;

    private Rigidbody hiderRb;
    private bool wasFound = true;
    private bool agentIsSeeking;
    private float timeStartedSeeking;
    private bool sawEachOther;
    private bool isBeingSeenBySeeker;
    private float previousDistance = float.MaxValue;
    private bool agentIsHidden;
    private bool gotPunishment;
    private bool hasCollidedWithWall;
    private bool cantCollideWithWall;
    private bool gotAngleReward;
    private Vector3 previousPosition;
    private bool gotNegativeForNotHiding;
    private static Vector3 previousHidingSpot;
    private bool gotRewardForDifferentSpot;
    private bool gotPunishmentForNotLooking;
    private bool gotRewardForApproachingWall;

    public override void Initialize()
    {
        previousHidingSpot = Vector3.zero;
        hiderRb = GetComponentInParent<Rigidbody>();
        seekerAgent.OnSeenAction += SeekerSeeHider;
    }

    private void SeekerSeeHider(bool canSee)
    {
        isBeingSeenBySeeker = canSee;
        if (canSee && RayPerceptionSensorDetectedSeeker())
            sawEachOther = true;
    }

    public override void OnEpisodeBegin()
    {
        if (!wasFound)
            AddReward(2f);

        gotPunishmentForNotLooking = false;
        gotRewardForDifferentSpot = false;
        gotNegativeForNotHiding = false;
        agentIsHidden = false;
        gotAngleReward = false;
        gotPunishment = false;
        sawEachOther = false;
        wasFound = false;
        transform.parent.localPosition = new Vector3(0, 0.25f, 0);
        previousPosition = transform.parent.localPosition;
        hiderRb.velocity = Vector3.zero;
        hiderRb.angularVelocity = Vector3.zero;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        MoveAgent(actions.DiscreteActions);

        CheckIfHidden();

        CheckIfHiderRunningAwayAndReward();

        PunishIfNotHiddenWhenSeekerIsSeeking();

        RewardIfLookingInDirectionOfSeekerWhenHidden();

        RewardForStayingHidden();

        RewardForApproachingHidingSpot();

        RewardIfChangedPlaceOfHiding();

        PunishIfNotLookingAtSeeker();

    }

    private void RewardForStayingHidden()
    {
        if (agentIsHidden && !isBeingSeenBySeeker)
            AddReward(0.001f);
    }

    private void RewardForApproachingHidingSpot()
    {
        if (agentIsSeeking)
            return;

        Collider[] walls = Physics.OverlapSphere(transform.position, 0.5f, hidingWallMask);
        if (walls.Length > 0 && !gotRewardForApproachingWall)
            AddReward(0.001f);
    }

    private void RewardIfChangedPlaceOfHiding()
    {
        if (!agentIsHidden)
            return;

        if (Vector3.Distance(transform.parent.localPosition, previousHidingSpot) > 4f && agentIsSeeking && !gotRewardForDifferentSpot && Time.realtimeSinceStartup - timeStartedSeeking < 0.022f)
        {
            AddReward(1.0f);
            gotRewardForDifferentSpot = true;
            previousHidingSpot = transform.parent.localPosition;
        }
    }

    private void CheckIfHidden()
    {
        bool seekerDetected = RayPerceptionSensorDetectedSeeker();
        bool obstacleBetween = IsObstacleBetweenHiderAndSeeker();

        bool isHiderNotMoving = Vector3.Distance(transform.parent.position, previousPosition) < 0.05f;

        previousPosition = transform.parent.position;

        if (!seekerDetected && obstacleBetween && !agentIsHidden && !agentIsSeeking && isHiderNotMoving)
        {
            agentIsHidden = true;
            AddReward(1f);
        }
        if (!agentIsHidden && agentIsSeeking && !gotNegativeForNotHiding)
        {
            AddReward(-0.5f);
            gotNegativeForNotHiding = true;
        }
    }

    private void PunishIfNotHiddenWhenSeekerIsSeeking()
    {
        if (agentIsSeeking && !agentIsHidden && !gotPunishment)
        {
            AddReward(-0.5f);
            gotPunishment = true;
        }
    }

    private void RewardIfLookingInDirectionOfSeekerWhenHidden()
    {
        if (!agentIsHidden)
            return;

        Vector3 directionToSeeker = seekerAgent.transform.parent.position - transform.parent.position;

        float angleToSeeker = Vector3.Angle(transform.parent.forward, directionToSeeker);
        if (angleToSeeker < 20f && !agentIsSeeking)
        {
            gotAngleReward = true;
            AddReward(0.001f);
        }
    }

    private void PunishIfNotLookingAtSeeker()
    {
        if (agentIsHidden && !gotAngleReward && agentIsSeeking && !gotPunishmentForNotLooking)
        {
            AddReward(-0.5f);
            gotPunishmentForNotLooking = true;
            Debug.LogError("Kaznjen");
        }
    }

    private bool IsObstacleBetweenHiderAndSeeker()
    {
        Vector3 directionToSeeker = seekerAgent.transform.parent.position - transform.parent.position;
        RaycastHit hit;

        if (Physics.Raycast(transform.parent.position, directionToSeeker, out hit, 3f) && !agentIsSeeking)
            if (hit.transform.gameObject.CompareTag("HidingWall"))
                return true;
        return false;
    }

    private void MoveAgent(ActionSegment<int> actions)
    {
        var moveDirection = Vector3.zero;
        var rotateDirection = Vector3.zero;

        var action = actions[0];

        switch (action)
        {
            case 1:
                moveDirection = movementSpeed * Time.deltaTime * Vector3.forward;
                break;
            case 2:
                moveDirection = movementSpeed * Time.deltaTime * Vector3.back;
                break;
        }

        if (actions[1] == 1)
        {
            rotateDirection = rotationSpeed * Time.deltaTime * Vector3.up;
        }
        else if (actions[1] == 2)
        {
            rotateDirection = -rotationSpeed * Time.deltaTime * Vector3.up;
        }

        transform.parent.Rotate(rotateDirection);
        transform.parent.Translate(moveDirection, Space.Self);
    }

    private bool RayPerceptionSensorDetectedSeeker()
    {
        foreach (var raySensor in raySensorFront.RaySensor.RayPerceptionOutput.RayOutputs)
            if (raySensor.HitGameObject != null && raySensor.HitGameObject.CompareTag("Seeker"))
                return true;

        return false;
    }

    private void CheckIfHiderRunningAwayAndReward()
    {
        if (sawEachOther && isBeingSeenBySeeker && agentIsSeeking && agentIsHidden)
        {
            float distance = Vector3.Distance(transform.localPosition, seekerAgent.transform.parent.localPosition);
            float currentDistance = distance;

            if (distance > 1f || currentDistance > previousDistance)
                AddReward(0.0001f);

            previousDistance = currentDistance;
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.W))
            discreteActionsOut[0] = 1;
        else if (Input.GetKey(KeyCode.S))
            discreteActionsOut[0] = 2;
        else
            discreteActionsOut[0] = 0;

        if (Input.GetKey(KeyCode.D))
            discreteActionsOut[1] = 1;
        else if (Input.GetKey(KeyCode.A))
            discreteActionsOut[1] = 2;
        else
            discreteActionsOut[1] = 0;
    }


    public void HandleAgentFound(float currentTime)
    {
        if (currentTime <= 10f)
            AddReward(-0.7f);

        wasFound = true;
        EndEpisode();
    }

    public void AgentIsSeeking()
    {
        agentIsSeeking = true;
        timeStartedSeeking = Time.realtimeSinceStartup;
    }

    public void AgentIsNotSeeking()
    {
        agentIsSeeking = false;
    }

    public void HandleOnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Wall") && !cantCollideWithWall)
            hasCollidedWithWall = true;
    }

    private void FixedUpdate()
    {
        if (!hasCollidedWithWall)
            return;

        hasCollidedWithWall = false;
        cantCollideWithWall = true;
        AddReward(-0.5f);
    }
}
