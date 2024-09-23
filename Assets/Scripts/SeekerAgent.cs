using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class SeekerAgent : Agent
{
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
        bestAlignmentToHider = 0;
        positionOfHider = Vector3.zero;
        closestDistanceEverToHider = float.MaxValue;
    }

    public override void OnEpisodeBegin()
    {
        episodeCounter++;
        cantCollideWithHider = false;
        cantCollideWithWall = false;
        hasCollidedWithHider = false;
        hasCollidedWithWall = false;
        seekerRb.velocity = Vector3.zero;
        seekerRb.angularVelocity = Vector3.zero;
        transform.parent.localPosition = new Vector3(-1, 0.25f, 2f);
        previousDistanceToHider = 0f;

        if (hasGotHider)
        {
            closestDistanceEverToHider = float.MaxValue;
            bestAlignmentToHider = 0;
            positionOfHider = Vector3.zero;
            gotRewardForSeeingHiderFirstTime = false;
            hasGotHider = false;
        }

    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!agentStarted)
            return;

        MoveAgent(actions.DiscreteActions);
        if (!gotRewardForSeeingHiderFirstTime)
            return;

        float currentDistanceToHider = Vector3.Distance(transform.parent.localPosition, positionOfHider);

        if (currentDistanceToHider + 0.5f < closestDistanceEverToHider)
        {
            closestDistanceEverToHider = currentDistanceToHider;
            AddReward(0.2f);
        }

        if (previousDistanceToHider == 0)
        {
            previousDistanceToHider = currentDistanceToHider;
        }
        else if (previousDistanceToHider - currentDistanceToHider >= 0.5f)
        {
            AddReward((previousDistanceToHider - currentDistanceToHider) * 0.02f);
            previousDistanceToHider = currentDistanceToHider;
        }

        Vector3 directionToHider = (positionOfHider - transform.parent.localPosition).normalized;
        float currentAlignment = Vector3.Dot(transform.parent.forward, directionToHider);
        currentAlignment = Mathf.Clamp01(currentAlignment);

        if (currentAlignment > bestAlignmentToHider)
        {
            bestAlignmentToHider = currentAlignment;
            AddReward(0.01f);

            if (bestAlignmentToHider > 0.7f)
                AddReward(bestAlignmentToHider * 0.01f);
        }
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

    public override void CollectObservations(VectorSensor sensor)
    {
        if (agentStarted)
        {
            sensor.AddObservation(transform.parent.localPosition.normalized);
            sensor.AddObservation(transform.parent.localRotation.normalized);

            // if (!hiderFound)
            // {
            //     foreach (var raySensor in rayPerceptionSensor.RaySensor.RayPerceptionOutput.RayOutputs)
            //     {
            //         if (raySensor.HitGameObject != null && raySensor.HitGameObject.CompareTag("Hider"))
            //         {
            //             AddReward(0.5f);
            //             positionOfHider = raySensor.HitGameObject.transform.localPosition;
            //             hiderFound = true;
            //             OnSeenAction.Invoke(true);
            //             break;
            //         }
            //     }
            // }
            bool canSeeHider = false;
            foreach (var raySensor in rayPerceptionSensor.RaySensor.RayPerceptionOutput.RayOutputs)
            {
                if (raySensor.HitGameObject != null && raySensor.HitGameObject.CompareTag("Hider"))
                {
                    if (!gotRewardForSeeingHiderFirstTime)
                    {
                        AddReward(0.5f);
                        gotRewardForSeeingHiderFirstTime = true;
                    }
                    canSeeHider = true;
                    positionOfHider = raySensor.HitGameObject.transform.localPosition;
                    AddReward(0.0003f);
                    break;
                }
            }

            OnSeenAction.Invoke(canSeeHider);

            if (gotRewardForSeeingHiderFirstTime)
                sensor.AddObservation(positionOfHider.normalized);
            else
                sensor.AddObservation(Vector3.zero);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Quaternion.identity);
            sensor.AddObservation(Vector3.zero);
        }
    }

    public void HandleOnCollisionEnter(Collision other)
    {
        if (!agentStarted)
            return;

        if (other.gameObject.CompareTag("Wall") && !cantCollideWithWall)
            hasCollidedWithWall = true;

        if (other.gameObject.CompareTag("Hider") && !cantCollideWithHider)
            hasCollidedWithHider = true;
    }

    // public void HandleOnCollisionStay(Collision other)
    // {
    //     if (other.gameObject.CompareTag("Wall") || other.gameObject.CompareTag("HidingWall"))
    //         AddReward(-0.03f * Time.fixedDeltaTime);
    // }

    private void FixedUpdate()
    {
        if (!agentStarted)
            return;

        if (hasCollidedWithHider)
        {
            hasCollidedWithHider = false;
            cantCollideWithHider = true;

            AddReward(2f);
            OnFoundAction.Invoke(episodeCounter);
            hasGotHider = true;

            EndEpisode();
        }

        if (hasCollidedWithWall)
        {
            hasCollidedWithWall = false;
            cantCollideWithWall = true;
            AddReward(-0.5f);
            EndEpisode();
        }
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
