using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class HiderAgent : Agent
{
    public const string HidingWallTag = "HidingWall";
    public const string WallTag = "Wall";
    public const string SeekerTag = "Seeker";
    public static Vector3 StartingPosition = new Vector3(0, 0.25f, 0);
    public const float MomentOfStartingToSeek = 0.022f;
    public const float SmallContinuousReward = 0.001f;
    public const float ExtraSmallContinuousReward = 0.0001f;
    public const float PunishmentForBeingFound = -0.7f;
    public const float RewardForNotBeingFound = 2f;
    public const float RewardForChangingPlaceOfHiding = 1f;
    public const float RewardForHiding = 1f;
    public const float PunishmentForNotHiding = -0.5f;
    public const float PunishmentForNotLookingAtSeeker = -0.5f;
    public const float PunishmentForCollidingWithWall = -0.5f;
    public const float MaxDistanceOfRay = 3f;
    public const float MaxAngleToSeeker = 20f;
    public const int ForwardAction = 1;
    public const int BackwardAction = 2;
    public const int LeftRotation = 2;
    public const int RightRotation = 1;
    public const int NoAction = 0;
    public const int MinimumTimeOfAgentToBeHidden = 10;
    public const float RadiusOfSphere = 0.5f;
    public const float SmallDistance = 0.05f;
    public const float RadiusOfNewHidingSpot = 4f;


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

    private bool RayPerceptionSensorDetectedSeeker()
    {
        foreach (var raySensor in raySensorFront.RaySensor.RayPerceptionOutput.RayOutputs)
            if (raySensor.HitGameObject != null && raySensor.HitGameObject.CompareTag(SeekerTag))
                return true;

        return false;
    }

    public override void OnEpisodeBegin()
    {
        if (!wasFound)
            AddReward(RewardForNotBeingFound);

        ResetConditionsForNewEpisode();
        ResetStateOfHiderForNewEpisode();
    }

    private void ResetConditionsForNewEpisode()
    {
        gotPunishmentForNotLooking = false;
        gotRewardForDifferentSpot = false;
        gotNegativeForNotHiding = false;
        agentIsHidden = false;
        gotAngleReward = false;
        gotPunishment = false;
        sawEachOther = false;
        wasFound = false;
    }

    private void ResetStateOfHiderForNewEpisode()
    {
        transform.parent.localPosition = StartingPosition;
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

    private void MoveAgent(ActionSegment<int> actions)
    {
        RotationMovement(actions);
        ForwardBackwardMovement(actions[0]);
    }

    private void ForwardBackwardMovement(int action)
    {

        var moveDirection = Vector3.zero;

        switch (action)
        {
            case ForwardAction:
                moveDirection = movementSpeed * Time.deltaTime * Vector3.forward;
                break;
            case BackwardAction:
                moveDirection = movementSpeed * Time.deltaTime * Vector3.back;
                break;
        }

        transform.parent.Translate(moveDirection, Space.Self);
    }

    private void RotationMovement(ActionSegment<int> actions)
    {
        var rotateDirection = Vector3.zero;

        if (actions[1] == RightRotation)
            rotateDirection = rotationSpeed * Time.deltaTime * Vector3.up;
        else if (actions[1] == LeftRotation)
            rotateDirection = -rotationSpeed * Time.deltaTime * Vector3.up;

        transform.parent.Rotate(rotateDirection);
    }

    private void CheckIfHidden()
    {
        bool seekerDetected = RayPerceptionSensorDetectedSeeker();
        bool obstacleBetween = IsObstacleBetweenHiderAndSeeker();

        bool isHiderNotMoving = IsHiderNotMoving();

        previousPosition = transform.parent.position;

        if (!seekerDetected && obstacleBetween && !agentIsHidden && !agentIsSeeking && isHiderNotMoving)
        {
            agentIsHidden = true;
            AddReward(RewardForHiding);
        }
        if (!agentIsHidden && agentIsSeeking && !gotNegativeForNotHiding)
        {
            AddReward(PunishmentForNotHiding);
            gotNegativeForNotHiding = true;
        }
    }

    private bool IsHiderNotMoving() => Vector3.Distance(transform.parent.position, previousPosition) < SmallDistance;

    private bool IsObstacleBetweenHiderAndSeeker()
    {
        Vector3 directionToSeeker = seekerAgent.transform.parent.position - transform.parent.position;
        RaycastHit hit;

        if (Physics.Raycast(transform.parent.position, directionToSeeker, out hit, MaxDistanceOfRay) && !agentIsSeeking)
            if (hit.transform.gameObject.CompareTag(HidingWallTag))
                return true;

        return false;
    }

    private void CheckIfHiderRunningAwayAndReward()
    {
        if (!sawEachOther || !isBeingSeenBySeeker || !agentIsSeeking || !agentIsHidden)
            return;

        float distance = Vector3.Distance(transform.localPosition, seekerAgent.transform.parent.localPosition);
        float currentDistance = distance;

        if (distance > 1f || currentDistance > previousDistance)
            AddReward(ExtraSmallContinuousReward);

        previousDistance = currentDistance;
    }

    private void PunishIfNotHiddenWhenSeekerIsSeeking()
    {
        if (!agentIsSeeking || agentIsHidden || gotPunishment)
            return;

        AddReward(PunishmentForNotHiding);
        gotPunishment = true;
    }

    private void RewardIfLookingInDirectionOfSeekerWhenHidden()
    {
        if (!agentIsHidden)
            return;

        Vector3 directionToSeeker = seekerAgent.transform.parent.position - transform.parent.position;

        float angleToSeeker = Vector3.Angle(transform.parent.forward, directionToSeeker);

        if (angleToSeeker < MaxAngleToSeeker && !agentIsSeeking)
        {
            gotAngleReward = true;
            AddReward(SmallContinuousReward);
        }
    }

    private void RewardForStayingHidden()
    {
        if (agentIsHidden && !isBeingSeenBySeeker)
            AddReward(SmallContinuousReward);
    }

    private void RewardForApproachingHidingSpot()
    {
        if (agentIsSeeking)
            return;

        Collider[] walls = Physics.OverlapSphere(transform.position, RadiusOfSphere, hidingWallMask);
        if (walls.Length > 0 && !gotRewardForApproachingWall)
            AddReward(SmallContinuousReward);
    }

    private void RewardIfChangedPlaceOfHiding()
    {
        if (!agentIsHidden)
            return;

        if (IsInRadiusOfPrevoiusHidingSpot() || !agentIsSeeking || gotRewardForDifferentSpot || Time.realtimeSinceStartup - timeStartedSeeking > MomentOfStartingToSeek)
            return;

        AddReward(RewardForChangingPlaceOfHiding);
        gotRewardForDifferentSpot = true;
        previousHidingSpot = transform.parent.localPosition;
    }

    private bool IsInRadiusOfPrevoiusHidingSpot() => Vector3.Distance(transform.parent.localPosition, previousHidingSpot) <= RadiusOfNewHidingSpot;

    private void PunishIfNotLookingAtSeeker()
    {
        if (!agentIsHidden || gotAngleReward || !agentIsSeeking || gotPunishmentForNotLooking)
            return;

        AddReward(PunishmentForNotLookingAtSeeker);
        gotPunishmentForNotLooking = true;
    }


    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActionsOut = actionsOut.DiscreteActions;

        HandleMoveActions(discreteActionsOut);

        HandleRotationActions(discreteActionsOut);
    }

    private void HandleMoveActions(ActionSegment<int> discreteActionsOut)
    {
        if (Input.GetKey(KeyCode.W))
            discreteActionsOut[0] = ForwardAction;
        else if (Input.GetKey(KeyCode.S))
            discreteActionsOut[0] = BackwardAction;
        else
            discreteActionsOut[0] = NoAction;
    }

    private void HandleRotationActions(ActionSegment<int> discreteActionsOut)
    {
        if (Input.GetKey(KeyCode.D))
            discreteActionsOut[1] = RightRotation;
        else if (Input.GetKey(KeyCode.A))
            discreteActionsOut[1] = LeftRotation;
        else
            discreteActionsOut[1] = NoAction;
    }

    public void HandleAgentFound(float currentTime)
    {
        if (currentTime <= MinimumTimeOfAgentToBeHidden)
            AddReward(PunishmentForBeingFound);

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
        if (other.gameObject.CompareTag(WallTag) && !cantCollideWithWall)
            hasCollidedWithWall = true;
    }

    private void FixedUpdate()
    {
        if (!hasCollidedWithWall)
            return;

        hasCollidedWithWall = false;
        cantCollideWithWall = true;
        AddReward(PunishmentForCollidingWithWall);
    }
}
