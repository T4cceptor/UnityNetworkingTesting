#define DRAW_PHYSICS_PROXIES

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class PhysicsSnapshotAdapter : Photon.MonoBehaviour
{
    public Rigidbody RigidBody
    {
        get; private set;
    }

    private struct Snapshot
    {
        public readonly float time;
        public readonly Vector3 position;
        public readonly Vector3 velocity;
        public readonly Quaternion rotation;

        public Snapshot(float time, Vector3 position, Vector3 velocity, Quaternion rotation)
        {
            this.time = time;
            this.position = position;
            this.velocity = velocity;
            this.rotation = rotation;
        }
    }
    private Queue<Snapshot> snapshots;
    private Snapshot currentStart;
    private Snapshot currentEnd;
    private bool hasInitialSnapshots = false;

    private int registeredViewId = 0;

    protected virtual void OnEnable()
    {
        snapshots = new Queue<Snapshot>(2 * PhysicsSnapshotManager.Instance.NumUpdatesToBuffer);
        hasInitialSnapshots = false;
        RigidBody = GetComponent<Rigidbody>();
        if (photonView.viewID > 0)
        {
            PhysicsSnapshotManager.Instance.Register(this);
            registeredViewId = photonView.viewID;
        }
    }

    protected virtual void Awake()
    {        
    }

    protected virtual void Start()
    {
    }
    protected virtual void OnDisable()
    {
        if (PhysicsSnapshotManager.Instance)
        {
            PhysicsSnapshotManager.Instance.Unregister(registeredViewId);
        }
        registeredViewId = 0;
    }

    public void AddSnapshot(float time, Vector3 position, Vector3 velocity, Quaternion rotation)
    {
        snapshots.Enqueue(new Snapshot(time, position, velocity, rotation));
    }

    protected virtual void Update()
    {
        if (photonView.viewID != registeredViewId)
        {
            if (registeredViewId != 0)
            {
                PhysicsSnapshotManager.Instance.Unregister(registeredViewId);
            }
            PhysicsSnapshotManager.Instance.Register(this);
            registeredViewId = photonView.viewID;
        }

        if (photonView.isMine)
        {
            //TODO: properly handle transition
            return;
        }

        RigidBody.isKinematic = true;

        if (!hasInitialSnapshots)
        {
            if (snapshots.Count < 2)
            {
                return;
            }
            currentStart = snapshots.Dequeue();
            currentEnd = snapshots.Dequeue();
            hasInitialSnapshots = true;
        }

        float time = Time.time;

        if (time < currentStart.time)
        {
            // still waiting for start delay to end
            return;
        }
        
        while (time > currentEnd.time && snapshots.Count > 0)
        {
            currentStart = currentEnd;
            currentEnd = snapshots.Dequeue();
        }

        //PhysicsSnapshotManager.Log("obj " + gameObject.name + " = ( " + currentStart.time + " <-> " + currentEnd.time + " ) at " + time + " buffered snapshots = " + snapshots.Count );

        float timeBetweenSnapshots = (currentEnd.time - currentStart.time);
        float interpolateValue = (time - currentStart.time) / (currentEnd.time - currentStart.time);

        if (interpolateValue > 1)
        {
            float deltaT = time - currentEnd.time;
            float reducedDeltaT = Mathf.Pow(deltaT, 1.0f/4.0f);
            Debug.LogWarningFormat("SnapshotPhysics of \"{0}\" missing next snapshot - extrapolating may lead to inconsistent behavior - youngest snapshot is {1}ms old", gameObject.name, deltaT * 1000);
            RigidBody.position = currentEnd.position + reducedDeltaT * currentEnd.velocity;
            RigidBody.rotation = Quaternion.Slerp(currentStart.rotation, currentEnd.rotation, 1 + reducedDeltaT);
        } else
        {
            RigidBody.position = HermiteInterpolation.Interpolate(currentStart.position, currentStart.velocity, currentEnd.position, currentEnd.velocity, interpolateValue);
            PhysicsSnapshotManager.Log("IN: " + currentStart.position + " | " + currentStart.velocity + " <-> " + currentEnd.position + " | " + currentEnd.velocity + " @ " + interpolateValue + " = " + RigidBody.position);
            RigidBody.rotation = Quaternion.Slerp(currentStart.rotation, currentEnd.rotation, interpolateValue);
        }
    }
}
