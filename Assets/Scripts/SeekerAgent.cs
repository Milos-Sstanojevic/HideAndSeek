using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class SeekerAgent : Agent
{
    public const string WallTag = "Wall";
    public const string HiderTag = "Hider";
    public static Vector3 StartingPosition = new Vector3(-1, 0.25f, 2f);
    public const int ForwardAction = 1;
    public const int BackwardAction = 2;
    public const int RightRotationAction = 1;
    public const int LeftRotationAction = 2;
    public const int NoAction = 0;
    public const float RewardForGettingClosesEverToHider = 0.2f;
    public const float FactorOfRewardForMovingCloser = 0.02f;
    public const float SmallRewardForAligningToHider = 0.01f;
    public const float SuperSmallRewardForNotLosingSightOfHider = 0.0003f;
    public const float RewardForSeeingAgentFirstTime = 0.5f;
    public const float RewardForCatchingHider = 2f;
    public const float PunishmentForCollidingWithWall = -0.5f;
    public const float AngleForReward = 0.7f;
    public const float LittleStep = 0.5f;
    public const float RestartedDistanceFromHider = 0f;
    public const float RestartedAlignmentToHider = 0f;

    public Action<int> OnFoundAction;
    public Action<bool> OnSeenAction;

    [SerializeField] private float rotationSpeed;
    [SerializeField] private float movementSpeed;
    [SerializeField] private RayPerceptionSensorComponent3D rayPerceptionSensor;

    private int episodeCounter = 0;
    private Rigidbody seekerRb;
    private Vector3 positionOfHider;
    private bool gotRewardForSeeingHiderFirstTime;
    private float closestDistanceEverToHider;
    private bool hasGotHider;
    private float bestAlignmentToHider;
    private float previousDistanceToHider;
    private bool hasCollidedWithHider;
    private bool cantCollideWithHider;
    private bool cantCollideWithWall;
    private bool hasCollidedWithWall;
    private bool agentStarted;

    public override void Initialize()
    {
        seekerRb = GetComponentInParent<Rigidbody>();
        bestAlignmentToHider = RestartedAlignmentToHider;
        positionOfHider = Vector3.zero;
        closestDistanceEverToHider = float.MaxValue;
    }

    public override void OnEpisodeBegin()
    {
        ResetConditionsForOneTimeReward();
        ResetStateOfSeekerForNewEpisode();

        if (!hasGotHider)
            return;

        ResetConditionsIfHiderIsGotten();
    }

    private void ResetConditionsForOneTimeReward()
    {
        cantCollideWithHider = false;
        cantCollideWithWall = false;
        hasCollidedWithHider = false;
        hasCollidedWithWall = false;
    }

    private void ResetStateOfSeekerForNewEpisode()
    {
        seekerRb.velocity = Vector3.zero;
        seekerRb.angularVelocity = Vector3.zero;
        transform.parent.localPosition = StartingPosition;
        previousDistanceToHider = RestartedDistanceFromHider;
    }

    private void ResetConditionsIfHiderIsGotten()
    {
        closestDistanceEverToHider = float.MaxValue;
        bestAlignmentToHider = RestartedAlignmentToHider;
        positionOfHider = Vector3.zero;
        gotRewardForSeeingHiderFirstTime = false;
        hasGotHider = false;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!agentStarted)
            return;

        MoveAgent(actions.DiscreteActions);

        if (!gotRewardForSeeingHiderFirstTime)
            return;

        float currentDistanceToHider = Vector3.Distance(transform.parent.localPosition, positionOfHider);

        RewardIfSeekerGotClosestEverToHider(currentDistanceToHider);

        RewardIfSeekerGotCloserToHiderNow(currentDistanceToHider);

        RewardIfSeekerIsLookingAtHider();
    }

    private void MoveAgent(ActionSegment<int> actions)
    {
        RotationMovement(actions);
        ForwardBackwardMovement(actions[0]);
    }

    private void RotationMovement(ActionSegment<int> actions)
    {
        var rotateDirection = Vector3.zero;

        if (actions[1] == RightRotationAction)
            rotateDirection = rotationSpeed * Time.deltaTime * Vector3.up;
        else if (actions[1] == LeftRotationAction)
            rotateDirection = -rotationSpeed * Time.deltaTime * Vector3.up;

        transform.parent.Rotate(rotateDirection);
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

    private void RewardIfSeekerGotClosestEverToHider(float currentDistanceToHider)
    {
        if (currentDistanceToHider + LittleStep >= closestDistanceEverToHider)
            return;

        closestDistanceEverToHider = currentDistanceToHider;
        AddReward(RewardForGettingClosesEverToHider);
    }

    private void RewardIfSeekerGotCloserToHiderNow(float currentDistanceToHider)
    {
        if (previousDistanceToHider == 0)
        {
            previousDistanceToHider = currentDistanceToHider;
        }
        else if (SeekerGettingCloserToHider(currentDistanceToHider))
        {
            AddReward(RewardForMovingCloserToHider(currentDistanceToHider));
            previousDistanceToHider = currentDistanceToHider;
        }
    }

    private bool SeekerGettingCloserToHider(float currentDistanceToHider) => previousDistanceToHider - currentDistanceToHider >= LittleStep;

    private float RewardForMovingCloserToHider(float currentDistanceToHider) => (previousDistanceToHider - currentDistanceToHider) * FactorOfRewardForMovingCloser;

    private void RewardIfSeekerIsLookingAtHider()
    {
        Vector3 directionToHider = (positionOfHider - transform.parent.localPosition).normalized;
        float currentAlignment = Vector3.Dot(transform.parent.forward, directionToHider);
        currentAlignment = Mathf.Clamp01(currentAlignment);

        if (currentAlignment > bestAlignmentToHider)
        {
            bestAlignmentToHider = currentAlignment;
            AddReward(SmallRewardForAligningToHider);
        }

        if (currentAlignment > AngleForReward)
            AddReward(currentAlignment * SmallRewardForAligningToHider);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActionsOut = actionsOut.DiscreteActions;

        HandleMovementActions(discreteActionsOut);

        HandleRotationActions(discreteActionsOut);
    }

    private void HandleMovementActions(ActionSegment<int> discreteActionsOut)
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
            discreteActionsOut[1] = RightRotationAction;
        else if (Input.GetKey(KeyCode.A))
            discreteActionsOut[1] = LeftRotationAction;
        else
            discreteActionsOut[1] = NoAction;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (!agentStarted)
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Quaternion.identity);
            sensor.AddObservation(Vector3.zero);
            return;
        }

        SetObservationsForLocalPositionAndOrientation(sensor);

        RewardIfSeekerSawOrIsLookingAtHider();

        if (gotRewardForSeeingHiderFirstTime)
            sensor.AddObservation(positionOfHider.normalized);
        else
            sensor.AddObservation(Vector3.zero);
    }

    private void SetObservationsForLocalPositionAndOrientation(VectorSensor sensor)
    {
        sensor.AddObservation(transform.parent.localPosition.normalized);
        sensor.AddObservation(transform.parent.localRotation.normalized);
    }

    private void RewardIfSeekerSawOrIsLookingAtHider()
    {
        bool canSeeHider = false;

        foreach (var raySensor in rayPerceptionSensor.RaySensor.RayPerceptionOutput.RayOutputs)
        {
            if (raySensor.HitGameObject == null || !raySensor.HitGameObject.CompareTag(HiderTag))
                continue;

            RewardSeekerForSeeingAgentForFirstTime();

            //Reward Seeker for not losing Sight of Hider
            canSeeHider = true;
            positionOfHider = raySensor.HitGameObject.transform.localPosition;
            AddReward(SuperSmallRewardForNotLosingSightOfHider);
            break;
        }

        OnSeenAction.Invoke(canSeeHider);
    }

    private void RewardSeekerForSeeingAgentForFirstTime()
    {
        if (gotRewardForSeeingHiderFirstTime)
            return;

        AddReward(RewardForSeeingAgentFirstTime);
        gotRewardForSeeingHiderFirstTime = true;
    }

    public void HandleOnCollisionEnter(Collision other)
    {
        if (!agentStarted)
            return;

        if (other.gameObject.CompareTag(WallTag) && !cantCollideWithWall)
            hasCollidedWithWall = true;

        if (other.gameObject.CompareTag(HiderTag) && !cantCollideWithHider)
            hasCollidedWithHider = true;
    }

    private void FixedUpdate()
    {
        if (!agentStarted)
            return;

        HandleCollidingWithHider();

        HandleCollidingWithWall();
    }

    private void HandleCollidingWithHider()
    {
        if (!hasCollidedWithHider)
            return;

        hasCollidedWithHider = false;
        cantCollideWithHider = true;

        AddReward(RewardForCatchingHider);
        OnFoundAction.Invoke(episodeCounter);
        hasGotHider = true;

        EndEpisode();
    }

    private void HandleCollidingWithWall()
    {
        if (!hasCollidedWithWall)
            return;

        hasCollidedWithWall = false;
        cantCollideWithWall = true;
        AddReward(PunishmentForCollidingWithWall);
        EndEpisode();
    }

    public void StartAgent()
    {
        agentStarted = true;
    }

    public void StopAgent()
    {
        agentStarted = false;
        EndEpisode();
    }
}
