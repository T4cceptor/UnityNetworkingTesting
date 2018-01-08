
using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// networking for physics object that works without mirrored physics - when an update arrives,
/// the local rigid body position/rotation is slowly corrected towards the values received from the master.
/// If the object is being controlled locally, ownership has to be acquired, and before changing the transform directly, 
/// or before using non-sync forces/joints (e.g. for grabbing), either make the rigid body kinematic, or set the
/// IsBeingControlled flag of this script
/// </summary>
public class CorrectivePhysicsNetworking : Photon.MonoBehaviour
{
    [SerializeField]
    [Tooltip("maximum duration during until a sleeping rigid body re-sends its info")]
    private float maxNoSendDuration = 1.0f;

    [SerializeField]
    [Tooltip("maximum distance for which interpolation will be used - if above, target position will be set directly")]
    private float maxPositionInterpolationThreshold = 1.0f;
    [SerializeField]
    [Tooltip("maximum angle for which interpolation will be used - if above, target rotation will be set directly")]
    private float maxRotationInterpolationThreshold = 30.0f;

    [SerializeField]
    [Tooltip("minimum distance for which interpolation will be used - if below, target position will be set directly")]
    private float minPositionInterpolationThreshold = 0.02f;
    [SerializeField]
    [Tooltip("minimum angle for which interpolation will be used - if below, target rotation will be set directly")]
    private float minRotationInterpolationThreshold = 1.0f;
    

    private struct Interpolation
    {
        public float absolute;
        public float errorRel;
        public float speedRel;

        public Interpolation(float absolute, float errorRel, float speedRel)
        {
            this.absolute = absolute;
            this.errorRel = errorRel;
            this.speedRel = speedRel;
        }

        public float GetCorrection(float currentError, float initialError, float currentSpeed)
        {
            float res = absolute * Time.fixedDeltaTime;
            res += errorRel * currentError;
            res += speedRel * currentSpeed;
            return res;
        }
    }

    [SerializeField]
    private Interpolation positionInterpolation = new Interpolation(1.0f, 2.0f, 0.1f);
    [SerializeField]
    private Interpolation rotationInterpolation = new Interpolation(5.0f, 2.0f, 0.1f);


    private Rigidbody rigidBody;

    private Vector3 lastReceivedPosition;
    private Quaternion lastReceivedRotation;
    private float lastReceiveTime = 0;

    private Vector3 positionError;
    private float initialPositionError;
    private bool requiresPositionCheck;
    private Vector3 rotationErrorAxis;
    private float initialRotationErrorAngle;
    private float rotationErrorAngle;
    private bool requiresRotationCheck;

    private Vector3 lastSentScale = Vector3.zero;
    private float nextFullSendTime = 0;
    private bool lastSendWasSleeping = false;

    private bool usesKinematicInterpolation = false;
    private Vector3 kinematicInterpolationStartPosition;
    private Quaternion kinematicInterpolationStartRotation;
    private Vector3 kinematicInterpolationTargetPosition;
    private Quaternion kinematicInterpolationTargetRotation;
    private Vector3 kinematicInterpolationStartVelocity;
    private Vector3 kinematicInterpolationTargetVelocity;
    private float kinematicInterpolationTime;


    /// <summary>
    /// Indicates if the object is currently being controlled, i.e. its transform is changed by the client
    /// directly, not just by physics. Can be set if the kinematic state is not set for the object (e.g. if 
    /// it is controlled by a grab joint).
    /// </summary>
    public virtual bool IsBeingControlled
    {
        get; set;
    }

    protected virtual void Awake()
    {
        rigidBody = GetComponent<Rigidbody>();

        if (photonView.ObservedComponents == null)
        {
            photonView.ObservedComponents = new List<Component>();
        }
        photonView.ObservedComponents.Add(this);
        photonView.synchronization = ViewSynchronization.Unreliable;
    }

    protected virtual void OnEnable()
    {
        // create some randomness for the next full send time, to avoid that all objects send at the same time
        nextFullSendTime = Time.time + UnityEngine.Random.Range(0.5f * maxNoSendDuration, maxNoSendDuration);
    }

    protected virtual void FixedUpdate()
    {
        if (usesKinematicInterpolation)
        {
            // only interpolate if we have a second kinematic sample
            if (kinematicInterpolationTime > 0)
            {
                float deltaT = Time.time - lastReceiveTime;
                float fraction = deltaT / kinematicInterpolationTime;
                if (fraction > 1)
                {
                    rigidBody.position = kinematicInterpolationTargetPosition;
                    rigidBody.rotation = kinematicInterpolationTargetRotation;
                } else
                {
                    rigidBody.position = HermiteInterpolation.Interpolate(kinematicInterpolationStartPosition, kinematicInterpolationStartVelocity, kinematicInterpolationTargetPosition, kinematicInterpolationTargetVelocity, fraction);
                    rigidBody.rotation = Quaternion.Slerp(kinematicInterpolationStartRotation, kinematicInterpolationTargetRotation, fraction);
                }
            }
        } else
        {
            if (requiresPositionCheck)
            {
                float errorDistance = positionError.magnitude;
                if (errorDistance >= maxPositionInterpolationThreshold || errorDistance <= minPositionInterpolationThreshold)
                {
                    rigidBody.position += positionError;
                    positionError = Vector3.zero;
                    requiresPositionCheck = false;
                } else
                {
                    float correctionDistance = positionInterpolation.GetCorrection(errorDistance, initialPositionError, rigidBody.velocity.magnitude);
                    if (correctionDistance > errorDistance)
                    {
                        rigidBody.position += positionError;
                        requiresPositionCheck = false;
                        //Debug.Log("reached goal " + rigidBody.position.ToString("F3"));
                    } else
                    {
                        Vector3 correction = positionError / errorDistance * correctionDistance;
                        //Debug.Log("phys upd to " + rigidBody.position.ToString("F3") + " by " + correction.ToString("F3") + ", remaining error " + (positionError.magnitude - correction));
                        rigidBody.position += correction;
                        positionError -= correction;
                    }
                }
            }

            if (requiresRotationCheck)
            {
                if (rotationErrorAngle >= maxRotationInterpolationThreshold || rotationErrorAngle <= minRotationInterpolationThreshold)
                {
                    rigidBody.rotation = Quaternion.AngleAxis(rotationErrorAngle, rotationErrorAxis) * rigidBody.rotation;
                    rotationErrorAngle = 0;
                    requiresRotationCheck = false;
                } else
                {
                    float angularSpeed = Mathf.Rad2Deg * rigidBody.angularVelocity.magnitude;
                    float correctionAngle = rotationInterpolation.GetCorrection(rotationErrorAngle, rotationErrorAngle, angularSpeed);
                    if (correctionAngle > rotationErrorAngle)
                    {
                        rigidBody.rotation = Quaternion.AngleAxis(rotationErrorAngle, rotationErrorAxis) * rigidBody.rotation;
                        requiresRotationCheck = false;
                        //Debug.Log("reached goal " + rigidBody.rotation.eulerAngles.ToString("F3"));
                    } else
                    {
                        //Debug.Log("phys upd to " + rigidBody.rotation.eulerAngles.ToString("F3") + " by " + correctionAngle + ", remaining error " + (rotationErrorAngle - correctionAngle));
                        rigidBody.rotation = Quaternion.AngleAxis(correctionAngle, rotationErrorAxis) * rigidBody.rotation;
                        correctionAngle -= correctionAngle;
                    }
                }
            }
        }
    }

    [Flags]
    enum PhysicsState : byte
    {
        NONE = 0,
        KINEMATIC = 1,
        SLEEPING = 2,
        USE_GRAVITY = 4,
        SCALE_CHANGED = 8,
    }
    private static bool StateHasFlag(PhysicsState state, PhysicsState flag)
    {
        return ((state & flag) == flag);
    }


    public virtual void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.isWriting)
        {
            bool isFullSendEvent = false;
            if (Time.time > nextFullSendTime)
            {
                isFullSendEvent = true;
                nextFullSendTime = Time.time + maxNoSendDuration;
            }

            if (!isFullSendEvent && lastSendWasSleeping && rigidBody.IsSleeping())
            {
                // we're asleep - only send if full update
                return;
            }

            PhysicsState state = PhysicsState.NONE;
            if (rigidBody.isKinematic || IsBeingControlled)
            {
                state |= PhysicsState.KINEMATIC;
            }
            if (rigidBody.IsSleeping())
            {
                state |= PhysicsState.SLEEPING;
            }
            if (rigidBody.useGravity)
            {
                state |= PhysicsState.USE_GRAVITY;
            }

            bool scaleChanged = (lastSentScale != transform.localScale);
            if (scaleChanged || isFullSendEvent)
            {
                state |= PhysicsState.SCALE_CHANGED;
            }

            stream.SendNext(state);
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            if (scaleChanged || isFullSendEvent)
            {
                stream.SendNext(transform.localScale);
                lastSentScale = transform.localScale;
            }

            if (rigidBody.isKinematic)
            {
                stream.SendNext(rigidBody.velocity);
            } else if (!rigidBody.IsSleeping())
            {
                stream.SendNext(rigidBody.velocity);
                stream.SendNext(rigidBody.angularVelocity);
            }

            lastSendWasSleeping = rigidBody.IsSleeping();
        } else
        {
            PhysicsState state = (PhysicsState) stream.ReceiveNext();


            lastReceivedPosition = (Vector3) stream.ReceiveNext();
            lastReceivedRotation = (Quaternion) stream.ReceiveNext();
            if (StateHasFlag(state, PhysicsState.SCALE_CHANGED))
            {
                transform.localScale = (Vector3) stream.ReceiveNext();
            }

            rigidBody.isKinematic = StateHasFlag(state, PhysicsState.KINEMATIC);
            rigidBody.useGravity = StateHasFlag(state, PhysicsState.USE_GRAVITY);
            bool sleeping = StateHasFlag(state, PhysicsState.SLEEPING);

            if (rigidBody.isKinematic)
            {
                Vector3 velocity = (Vector3) stream.ReceiveNext();
                                
                if (usesKinematicInterpolation)
                {
                    kinematicInterpolationTime = Time.time - lastReceiveTime;

                    kinematicInterpolationStartVelocity = kinematicInterpolationTargetVelocity;
                    kinematicInterpolationStartPosition = kinematicInterpolationTargetPosition;
                    kinematicInterpolationStartRotation = kinematicInterpolationTargetRotation;

                    kinematicInterpolationTargetVelocity = velocity;
                    kinematicInterpolationTargetPosition = lastReceivedPosition;
                    kinematicInterpolationTargetRotation = lastReceivedRotation;
                } else
                {
                    // first frame - set hard
                    usesKinematicInterpolation = true;
                    kinematicInterpolationTime = 0;
                    rigidBody.position = lastReceivedPosition;
                    rigidBody.rotation = lastReceivedRotation;

                    kinematicInterpolationTargetVelocity = velocity;
                    kinematicInterpolationTargetPosition = lastReceivedPosition;
                    kinematicInterpolationTargetRotation = lastReceivedRotation;
                }
                
            } else if (sleeping)
            {                
                rigidBody.position = lastReceivedPosition;
                rigidBody.rotation = lastReceivedRotation;
                rigidBody.velocity = Vector3.zero;
                rigidBody.angularVelocity = Vector3.zero;
                rigidBody.Sleep();
                requiresPositionCheck = false;
                requiresRotationCheck = false;
                usesKinematicInterpolation = false;
            } else
            {
                if (rigidBody.IsSleeping())
                {
                    rigidBody.WakeUp();
                }
                rigidBody.velocity = (Vector3) stream.ReceiveNext();
                rigidBody.angularVelocity = (Vector3) stream.ReceiveNext();

                positionError = lastReceivedPosition - rigidBody.position;
                initialPositionError = positionError.magnitude;
                Quaternion rotationError = lastReceivedRotation * Quaternion.Inverse(rigidBody.rotation);
                rotationError.ToAngleAxis(out rotationErrorAngle, out rotationErrorAxis);
                initialRotationErrorAngle = rotationErrorAngle;

                requiresPositionCheck = true;
                requiresRotationCheck = true;
                usesKinematicInterpolation = false;
            }
        }

        lastReceiveTime = Time.time;
    }
}
