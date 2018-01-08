#define DRAW_PHYSICS_PROXIES

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ExitGames.Client.Photon;

public class PhysicsSnapshotManager : Photon.MonoBehaviour
{
    [SerializeField]
    private int numPhysicsFramesToSkip = 0;
    [SerializeField]
    private int numUpdatesToBuffer = 3;

    private readonly byte physicsEventCode = 142;
    private RaiseEventOptions eventOptions;

    private static TextWriter log;
    public static void Log(string message)
    {
        if (log == null)
        {
            DateTime time = DateTime.Now;
            string filename = "snapshotlog.txt";
            log = new StreamWriter(filename);
        }

        log.WriteLine(Time.time.ToString("F3") + ": " + message);
    }


    public int NumUpdatesToBuffer
    {
        get
        {
            return numUpdatesToBuffer;
        }
    }

    private struct UpdateInfo
    {
        public struct ObjectInfo
        {
            public int id;
            public Vector3 position;
            public Vector3 velocity;
            public Quaternion rotation;
        }

        public int time;
        public short playerId;
        public int frameIndex;
        public short numObjects;
        public ObjectInfo[] objects;

        #region Serialization

        private static byte[] serializeBuffer = new Byte[sizeof(int) + 3 * sizeof(float) + 3 * sizeof(float) + 4 * sizeof(float)];

        public static object Deserialize(StreamBuffer inStream, short length)
        {
            UpdateInfo info = new UpdateInfo();
            lock (serializeBuffer)
            {
                int index = 0;
                inStream.Read(serializeBuffer, 0, 2 * sizeof(int) + 2 * sizeof(short));
                Protocol.Deserialize(out info.time, serializeBuffer, ref index);
                Protocol.Deserialize(out info.playerId, serializeBuffer, ref index);
                Protocol.Deserialize(out info.frameIndex, serializeBuffer, ref index);
                Protocol.Deserialize(out info.numObjects, serializeBuffer, ref index);
                info.objects = new ObjectInfo[info.numObjects];
                ObjectInfo objectInfo = new ObjectInfo();
                for (int i = 0; i < info.numObjects; ++i)
                {
                    index = 0;

                    inStream.Read(serializeBuffer, 0, sizeof(int) + 10 * sizeof(float));
                    Protocol.Deserialize(out objectInfo.id, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.position.x, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.position.y, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.position.z, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.velocity.x, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.velocity.y, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.velocity.z, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.rotation.x, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.rotation.y, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.rotation.z, serializeBuffer, ref index);
                    Protocol.Deserialize(out objectInfo.rotation.w, serializeBuffer, ref index);
                    //objectInfo.rotation.w = Mathf.Sqrt(1 - objectInfo.rotation.x * objectInfo.rotation.x
                    //                                    - objectInfo.rotation.y * objectInfo.rotation.y
                    //                                    - objectInfo.rotation.z * objectInfo.rotation.z);

                    info.objects[i] = objectInfo;
                }
            }
            string msg = "deser w/ time " + (uint)info.time + " of " + info.objects.Length + " objects";
            Log(msg);
            //Debug.Log("Deserialized physics update, size " + length + "bytes");
            return info;
        }

        public static short Serialize(StreamBuffer outStream, object customObject)
        {
            UpdateInfo info = (UpdateInfo) customObject;
            short accumulatedLength = 0;
            lock (serializeBuffer)
            {
                int index = 0;
                Protocol.Serialize(info.time, serializeBuffer, ref index);
                Protocol.Serialize(info.playerId, serializeBuffer, ref index);
                Protocol.Serialize(info.frameIndex, serializeBuffer, ref index);
                Protocol.Serialize(info.numObjects, serializeBuffer, ref index);
                outStream.Write(serializeBuffer, 0, index);
                accumulatedLength += (short) index;

                for (int i = 0; i < info.numObjects; ++i)
                {
                    ObjectInfo objectInfo = info.objects[i];
                    index = 0;

                    Protocol.Serialize(objectInfo.id, serializeBuffer, ref index);
                    Protocol.Serialize(objectInfo.position.x, serializeBuffer, ref index);
                    Protocol.Serialize(objectInfo.position.y, serializeBuffer, ref index);
                    Protocol.Serialize(objectInfo.position.z, serializeBuffer, ref index);
                    Protocol.Serialize(objectInfo.velocity.x, serializeBuffer, ref index);
                    Protocol.Serialize(objectInfo.velocity.y, serializeBuffer, ref index);
                    Protocol.Serialize(objectInfo.velocity.z, serializeBuffer, ref index);
                    Quaternion rotation = objectInfo.rotation;
                    // ensure that w component is positive
                    //if (rotation.w < 0)
                    //{
                    //    for (int j = 0; j < 4; ++j)
                    //    {
                    //        rotation[j] = -rotation[j];
                    //    }
                    //}
                    Protocol.Serialize(rotation.x, serializeBuffer, ref index);
                    Protocol.Serialize(rotation.y, serializeBuffer, ref index);
                    Protocol.Serialize(rotation.z, serializeBuffer, ref index);
                    Protocol.Serialize(rotation.w, serializeBuffer, ref index);
                    outStream.Write(serializeBuffer, 0, index);
                    accumulatedLength += (short) index;
                }
            }
            //Debug.Log("Serialized physics update, size " + accumulatedLength + "bytes");
            string msg = "ser w/ time " + (uint) info.time + " of " + info.numObjects + "obj -> " + accumulatedLength + "bytes";
            Log(msg);
            return accumulatedLength;
        }
        #endregion
    }
    private UpdateInfo writeBuffer = new UpdateInfo();

    private Dictionary<int, PhysicsSnapshotAdapter> adapters = new Dictionary<int, PhysicsSnapshotAdapter>();
    private Dictionary<int, int> playerFrameIndexOffsets = new Dictionary<int, int>();

    private int frameIndex = 0;
    private int skippedFrames = 0;

    public static PhysicsSnapshotManager Instance
    {
        get; private set;
    }

    protected virtual void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning("two physics snapshot managers detected, destroying this one");
            GameObject.Destroy(this);
        } else
        {
            Instance = this;
            PhotonPeer.RegisterType(typeof(UpdateInfo), 42, UpdateInfo.Serialize, UpdateInfo.Deserialize);

            eventOptions = new RaiseEventOptions();
            eventOptions.CachingOption = EventCaching.DoNotCache;
            eventOptions.Receivers = ReceiverGroup.Others;
            // use a custom sequence channel - this way, physics event order is independent of RPCs/other photon messages
            // so that we potentially reduce some latency
            // TODO: this requires adding a channel
            //eventOptions.SequenceChannel = 42;

            PhotonNetwork.OnEventCall += OnPhysicsEvent;
        }
    }

    protected virtual void OnDestroy()
    {
        PhotonNetwork.OnEventCall -= OnPhysicsEvent;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Register(PhysicsSnapshotAdapter adapter)
    {
        adapters.Add((short) adapter.photonView.viewID, adapter);
    }

    public void Unregister(int viewId)
    {
        adapters.Remove((short) viewId);
    }

    public void Unregister(PhysicsSnapshotAdapter adapter)
    {
        adapters.Remove((short) adapter.photonView.viewID);
    }

    public void FixedUpdate()
    {
        if (skippedFrames < numPhysicsFramesToSkip)
        {
            ++frameIndex;
            ++skippedFrames;
            return;
        }

        skippedFrames = 0;

        // compute server time this timestamp should be displayed
        float delay = (1 + NumUpdatesToBuffer) * Time.fixedDeltaTime * (1 + numPhysicsFramesToSkip);
        writeBuffer.time = PhotonNetwork.ServerTimestamp + (int)(delay * 1000);

        writeBuffer.frameIndex = frameIndex;
        if (writeBuffer.objects == null || adapters.Count > writeBuffer.objects.Length)
        {
            writeBuffer.objects = new UpdateInfo.ObjectInfo[adapters.Count + 5];
        }

        int objectIndex = 0;
        UpdateInfo.ObjectInfo objectInfo = new UpdateInfo.ObjectInfo();
        foreach (KeyValuePair<int, PhysicsSnapshotAdapter> adapter in adapters)
        {
            if (adapter.Value.photonView.isMine == false)
            {
                continue;
            }

            objectInfo.id = adapter.Key;
            objectInfo.position = adapter.Value.RigidBody.position;
            objectInfo.velocity = adapter.Value.RigidBody.velocity * Time.fixedDeltaTime;
            objectInfo.rotation = adapter.Value.RigidBody.rotation;

            writeBuffer.objects[objectIndex] = objectInfo;
            ++objectIndex;
        }
        writeBuffer.numObjects = (short) objectIndex;

        if (objectIndex > 0)
        {
            Log("sending snapshots for " + objectIndex + " objects");
            //photonView.RPC("PhysicsObjectsStateUpdate", PhotonTargets.Others, writeBuffer);

            PhotonNetwork.RaiseEvent(physicsEventCode, writeBuffer, false, eventOptions);

            // if we want to send updates faster than the current photon update rate, we trigger a send ourselves
            float deltaT = Time.fixedDeltaTime * (1 + numPhysicsFramesToSkip);
            if (deltaT < 1.0f / PhotonNetwork.sendRate)
            {
                PhotonNetwork.SendOutgoingCommands();
            }
        }

        ++frameIndex;
    }

    [PunRPC]
    private void PhysicsObjectsStateUpdate(UpdateInfo info)
    {
        HandlePhysicsUpdate(info);
    }

    private void OnPhysicsEvent(byte eventCode, object content, int senderId)
    {
        if (eventCode != physicsEventCode)
        {
            return;
        }

        HandlePhysicsUpdate((UpdateInfo) content);
    }

    private void HandlePhysicsUpdate(UpdateInfo info)
    {
        //int frameIndexOffset;
        //if (!playerFrameIndexOffsets.TryGetValue(info.playerId, out frameIndexOffset))
        //{
        //    frameIndexOffset = info.frameIndex - frameIndex;
        //    playerFrameIndexOffsets[info.playerId] = frameIndexOffset;
        //    Debug.Log("init frame offset to " + frameIndexOffset);
        //} else if (info.frameIndex - frameIndex < frameIndexOffset)
        //{
        //    frameIndexOffset = info.frameIndex - frameIndex;
        //    playerFrameIndexOffsets[info.playerId] = frameIndexOffset;
        //    Debug.Log("reduce frame offset to " + frameIndexOffset);
        //}

        uint timeDelay = (uint) info.time - (uint) PhotonNetwork.ServerTimestamp;
        //uint deltaT = (uint) info.time - (uint) PhotonNetwork.ServerTimestamp;
        float time = Time.time + (float) (timeDelay / 1000.0);
        Log("time = " + Time.time);
        Log("ts = " + (uint)PhotonNetwork.ServerTimestamp / 1000.0);
        Log("tt = " + (uint) info.time / 1000.0);
        Log("time = " + Time.time + " + " + timeDelay / 1000.0);

        Log("received snapshots for " + info.objects.Length + " objects for time " + time);

        foreach (UpdateInfo.ObjectInfo objectInfo in info.objects)
        {
            try
            {
                PhysicsSnapshotAdapter adapter = adapters[objectInfo.id];
                adapter.AddSnapshot(time, objectInfo.position, objectInfo.velocity, objectInfo.rotation);
            } catch (KeyNotFoundException)
            {
                Debug.LogWarning("PhysicsSnapshotManager - cannot find object with photon id " + objectInfo.id);
            }
        }
    }
}
