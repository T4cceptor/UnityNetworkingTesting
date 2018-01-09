//#define DRAW_PHYSICS_PROXIES

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(PhotonView), typeof(Rigidbody))]
public class MirroredPhysicsObjectNetworking : MonoBehaviour
{
    [SerializeField]
    private float maxNoSendDuration = 1.0f;
    [SerializeField]
    private float positionCorrectionPerSecond = 0.5f;
    [SerializeField]
    private float rotationCorrectionPerSecond = 0.5f;

    [SerializeField]
    private bool drawPhysicsProxy = true;

    private static Material proxyMaterial = null;
    private static Material ProxyMaterial
    {
        get
        {
            if (proxyMaterial == null)
            {
                proxyMaterial = new Material(Shader.Find("Standard"));
                proxyMaterial.color = new Color(0.0f, 0.5f, 0.0f, 0.3f);
                proxyMaterial.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.One);
                proxyMaterial.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                proxyMaterial.SetInt("_ZWrite", 0);
                proxyMaterial.DisableKeyword("_ALPHATEST_ON");
                proxyMaterial.DisableKeyword("_ALPHABLEND_ON");
                proxyMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                proxyMaterial.renderQueue = 3000;
            }
            return proxyMaterial;
        }
    }


    protected PhotonView photonView;

    private bool usesPhysicsProxy = false;
    private Rigidbody rigidBody;
    private GameObject physicsProxy;
    private Rigidbody physicsProxyRigidbody;

    private Vector3 lastReceivedPosition;
    private Quaternion lastReceivedRotation;

    private Vector3 lastSentRBVelocity;
    private Vector3 lastSentRBAngularVelocity;

    private Vector3 lastSentPosition;
    private Quaternion lastSentRotation;
    private Vector3 lastSentScale;

    [SerializeField]
    private Vector3 rigidbodyVelocity;

    [SerializeField]
    private Quaternion rigidbodyRotation;

    [SerializeField]
    private bool logPhotonViewChanges;



    private float lastSendTime = 0;

    protected virtual void Awake()
    {
        photonView = GetComponent<PhotonView>();
        rigidBody = GetComponent<Rigidbody>();

        lastSendTime = Time.time;

        CreatePhysicsProxy();

        if (photonView.ObservedComponents == null)
        {
            photonView.ObservedComponents = new List<Component>();
        }
        photonView.ObservedComponents.Add(this);
        photonView.synchronization = ViewSynchronization.Unreliable;
    }

    protected virtual void OnEnable()
    {
        EnsureProxyState(false);
    }
    protected virtual void OnDisable()
    {
        EnsureProxyState(false);
    }
    protected virtual void OnDestroy()
    {
        GameObject.Destroy(physicsProxy);
    }

    protected virtual void Update()
    {
        if (!photonView.isMine && usesPhysicsProxy)
        {
            UpdateOriginalFromProxy();
        }
        UpdateRigidbodyDisplay();
    }

    private void UpdateRigidbodyDisplay()
    {
        rigidbodyVelocity = physicsProxyRigidbody.velocity;
        rigidbodyRotation = physicsProxyRigidbody.rotation;
    }

    enum PhysicsState : byte
    {
        NONE = 0,
        KINEMATIC = 1,
        SLEEPING = 2,
        USE_GRAVITY = 4,
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.isWriting)
        {
            EnsureProxyState(false);

            bool sendHeartbeat = Time.time < lastSendTime + maxNoSendDuration;

            if (rigidBody.IsSleeping())
            {
                // don't send data for deactivated rigid bodies, unless timeout is exceeded
                // TODO: spread this across different rigid bodies, so that not all send at once
                if (sendHeartbeat)
                    return;
            }

            // check if change is significant enough to warrant an update message
            // ignore changes that are very small and / or potentially unnoticeable
            // this piece of code is basically the standard DIA dead reckoning algorithm
            if(rigidbodyNeedsUpdate() == false && transformNeedsUpdate() == false && sendHeartbeat == false)
            {
                return;
            }

            PhysicsState state = PhysicsState.NONE;
            if (rigidBody.isKinematic)
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

            lastSentPosition = transform.position;
            lastSentRotation = transform.rotation;
            lastSentScale = transform.localScale;

            stream.SendNext(state);
            stream.SendNext(lastSentPosition);
            stream.SendNext(lastSentRotation);
            stream.SendNext(lastSentScale);
            // todo: only send occasionally, or when changed
            bool updateRB = !rigidBody.isKinematic && !rigidBody.IsSleeping();
            stream.SendNext(updateRB);
            if (updateRB)
            {
                stream.SendNext(rigidBody.velocity);
                stream.SendNext(rigidBody.angularVelocity);
                log("Send new rigidbody values: " + rigidBody.velocity.ToString());
                
            }
            lastSendTime = Time.time;
        }
        else
        {
            // EnsureProxyState(false);

            PhysicsState state = (PhysicsState) stream.ReceiveNext();
            EnsureProxyState(true);
            physicsProxyRigidbody.isKinematic = false;
            physicsProxy.transform.position = (Vector3) stream.ReceiveNext();
            physicsProxy.transform.rotation = (Quaternion) stream.ReceiveNext();
            physicsProxy.transform.localScale = (Vector3) stream.ReceiveNext();
            bool updatedRbValues = false;
            physicsProxyRigidbody.useGravity = (state &= PhysicsState.USE_GRAVITY) != PhysicsState.NONE;

            //if ((state &= PhysicsState.KINEMATIC) != PhysicsState.NONE )
            //{
            //    if ((state &= PhysicsState.KINEMATIC) != PhysicsState.NONE)
            //    {
            //        physicsProxyRigidbody.Sleep();
            //    }
            //    else
            //    {
                    
            //    }
            //}

            bool updateRB = (bool)stream.ReceiveNext();
            if (updateRB)
            {
                physicsProxyRigidbody.velocity = (Vector3)stream.ReceiveNext();
                physicsProxyRigidbody.angularVelocity = (Vector3)stream.ReceiveNext();
                log("New RB values: " + physicsProxyRigidbody.velocity.ToString());
                
                updatedRbValues = true;
            }
            

            if (!updatedRbValues) {
                log("Updated transform without rigidbody!");
            }
        }
    }

    private void log(string newLog)
    {
        if (logPhotonViewChanges)
        {
            Debug.Log(newLog);
        }
    }

    private bool transformNeedsUpdate()
    {
        bool result = false;

        Vector3 positionChange = lastSentPosition - transform.position;
        bool positionChanged = positionChange.magnitude > 0.01; // if scale is maintained, 0.01 should be equal to 1 cm

        float rotationChange = Quaternion.Angle(lastSentRotation, transform.rotation);
        bool rotationChanged = rotationChange > 2; // TODO: check if this is correct and adjust appropriately

        Vector3 scaleChange = lastSentScale - transform.localScale;
        bool scaleChanged = scaleChange.magnitude > 0.1; // TODO: check if this is correct and adjust appropriately

        result = positionChanged || rotationChanged || scaleChanged;

        return result;
    }

    private bool rigidbodyNeedsUpdate()
    {
        bool result = false;
        Rigidbody currentRB = rigidBody;

        Vector3 velocityChange = lastSentRBVelocity - currentRB.velocity;
        bool velocityChanged = velocityChange.magnitude > 0.01;

        Vector3 angularVelocityChange = lastSentRBAngularVelocity - currentRB.angularVelocity;
        bool angularVelocityChanged = angularVelocityChange.magnitude > 0.01;

        result = velocityChanged || angularVelocityChanged;

        return result;
    }

    void EnsureProxyState(bool useProxy)
    {
        if (useProxy == usesPhysicsProxy)
            return;

        physicsProxy.SetActive(useProxy);
        rigidBody.detectCollisions = !useProxy;
        rigidBody.isKinematic = useProxy;
        if (!useProxy)
        {
            rigidBody.velocity = physicsProxyRigidbody.velocity;
            rigidBody.angularVelocity = physicsProxyRigidbody.angularVelocity;
        }

        usesPhysicsProxy = useProxy;
    }

    void CheckOriginalRigidbodyState()
    {
        rigidBody.detectCollisions = false;
        physicsProxyRigidbody.isKinematic = rigidBody.isKinematic;

    }

    void UpdateOriginalFromProxy()
    {
        // re-enable override, in case it was changed externally
        rigidBody.detectCollisions = false;
        rigidBody.isKinematic = true;

        float positionFactor = Mathf.Pow(positionCorrectionPerSecond, Time.fixedDeltaTime);
        float rotationFactor = Mathf.Pow(rotationCorrectionPerSecond, Time.fixedDeltaTime);

        log("positionFactor: " + positionFactor + " ,rotationFactor: " + rotationFactor);

        log("current position: " + transform.position);
        Vector3 positionError = physicsProxy.transform.position - transform.position;
        transform.position += positionFactor * positionError;
        log("new (corrected) position: " + transform.position);

        Quaternion rotationError = Quaternion.Inverse(transform.rotation) * physicsProxy.transform.rotation;
        transform.rotation *= Quaternion.Slerp(Quaternion.identity, rotationError, rotationFactor);

        //Debug.LogFormat("{0}: assigned pos {1} ori {2} by {3}, {4}", gameObject.name, transform.position, transform.rotation, positionFactor, rotationFactor);

        //rigidBody.detectCollisions = true;
        //rigidBody.isKinematic = false;
    }

    void CreatePhysicsProxy()
    {
        // TODO: forward callbacks

        physicsProxy = new GameObject();

        physicsProxy.name = gameObject.name + "_physics_proxy";
        physicsProxy.tag = gameObject.tag;
        physicsProxy.layer = gameObject.layer;
        physicsProxy.transform.localPosition = Vector3.zero;
        physicsProxy.transform.localRotation = Quaternion.identity;
        physicsProxy.transform.localScale = Vector3.one;

        Collider physicsProxyCollider = physicsProxy.GetComponent<Collider>();
        if (physicsProxyCollider != null) {
            Destroy(physicsProxyCollider);
        }
        

        physicsProxyRigidbody = (Rigidbody) CloneComponent(gameObject.GetComponent<Rigidbody>(), physicsProxy);

        // MA: do not cover child collider ect
        Dictionary<GameObject, GameObject> proxyChildren = new Dictionary<GameObject, GameObject>();
        proxyChildren[gameObject] = physicsProxy;

        Collider[] colliders = gameObject.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            GameObject childProxy = GetProxyObject(proxyChildren, collider.gameObject);
            Type colliderType = collider.GetType();
            if (colliderType == typeof(SphereCollider))
            {
                CloneComponent((SphereCollider)collider, childProxy);
            }
            else if (colliderType == typeof(BoxCollider))
            {
                CloneComponent((BoxCollider)collider, childProxy);
            }
            else if (colliderType == typeof(CapsuleCollider))
            {
                CloneComponent((CapsuleCollider)collider, childProxy);
            }
            else if (colliderType == typeof(MeshCollider))
            {
                CloneComponent((MeshCollider)collider, childProxy);
            }
            else
            {
                CloneComponent(collider, childProxy);
            }
        }

        physicsProxy.transform.parent = gameObject.transform.parent;

        physicsProxy.SetActive(false);
    }

    Component CloneComponent(Component originalComponent, GameObject newParent)
    {
        Type componentType = originalComponent.GetType();
        Component component = newParent.AddComponent(componentType);

        FieldInfo[] fields = componentType.GetFields(BindingFlags.Instance | BindingFlags.Public);
        foreach (FieldInfo field in fields)
        {
            object value = field.GetValue(originalComponent);
            field.SetValue(component, value);
        }
        PropertyInfo[] props = componentType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (PropertyInfo prop in props)
        {
            object[] obsoleteProps = prop.GetCustomAttributes(typeof(ObsoleteAttribute), true);
            if (obsoleteProps == null || obsoleteProps.Length == 0)
            {
                if (prop.CanWrite && prop.CanRead)
                {
                    object value = prop.GetValue(originalComponent, null);
                    prop.SetValue(component, value, null);
                }
            }
        }

        return component;
    }

    Rigidbody CloneComponent(Rigidbody originalComponent, GameObject newParent)
    {
        Rigidbody component = newParent.AddComponent<Rigidbody>();

        component.angularVelocity = originalComponent.angularVelocity;
        component.drag = originalComponent.drag;
        component.angularDrag = originalComponent.angularDrag;
        component.mass = originalComponent.mass;
        component.useGravity = originalComponent.useGravity;
        component.maxDepenetrationVelocity = originalComponent.maxDepenetrationVelocity;
        component.isKinematic = originalComponent.isKinematic;
        component.freezeRotation = originalComponent.freezeRotation;
        component.constraints = originalComponent.constraints;
        component.collisionDetectionMode = originalComponent.collisionDetectionMode;
        component.centerOfMass = originalComponent.centerOfMass;
        component.inertiaTensorRotation = originalComponent.inertiaTensorRotation;
        // component.inertiaTensor = originalComponent.inertiaTensor;
        component.detectCollisions = originalComponent.detectCollisions;
        component.interpolation = originalComponent.interpolation;
        component.solverIterations = originalComponent.solverIterations;
        component.solverVelocityIterations = originalComponent.solverVelocityIterations;
        component.sleepThreshold = originalComponent.sleepThreshold;
        component.maxAngularVelocity = originalComponent.maxAngularVelocity;

        return component;
    }

    SphereCollider CloneComponent(SphereCollider originalComponent, GameObject newParent)
    {
        SphereCollider component = newParent.AddComponent<SphereCollider>();

        CopyColliderProps(originalComponent, component);
        component.center = originalComponent.center;
        component.radius = originalComponent.radius;

#if DRAW_PHYSICS_PROXIES
        GameObject vis = CreateProxyVisualization(newParent, PrimitiveType.Sphere); 
        vis.transform.localPosition = component.center;
        vis.transform.localScale = new Vector3(component.radius, component.radius, component.radius) * 2;
#endif

        return component;
    }
    BoxCollider CloneComponent(BoxCollider originalComponent, GameObject newParent)
    {
        BoxCollider component = newParent.AddComponent<BoxCollider>();

        CopyColliderProps(originalComponent, component);
        component.center = originalComponent.center;
        component.size = originalComponent.size;

        if (drawPhysicsProxy)
        {
            GameObject vis = CreateProxyVisualization(newParent, PrimitiveType.Cube);
            vis.transform.localPosition = component.center;
            vis.transform.localScale = component.size;
        }

        return component;
    }
    CapsuleCollider CloneComponent(CapsuleCollider originalComponent, GameObject newParent)
    {
        CapsuleCollider component = newParent.AddComponent<CapsuleCollider>();

        CopyColliderProps(originalComponent, component);
        component.center = originalComponent.center;
        component.radius = originalComponent.radius;
        component.height = originalComponent.height;
        component.direction = originalComponent.direction;

        if (drawPhysicsProxy)
       { 
            GameObject vis = CreateProxyVisualization(newParent, PrimitiveType.Capsule);
            vis.transform.localPosition = component.center;
            vis.transform.localScale = new Vector3(2 * component.radius, component.height / 2, 2 * component.radius);
            switch (component.direction)
            {
                case 0:
                    {
                        vis.transform.rotation = new Quaternion(0, 0, 0.7071f, 0.7071f);
                        break;
                    }
                default:
                case 1:
                    {
                        // already oriented correctly
                        break;
                    }
                case 2:
                    {
                        vis.transform.rotation = new Quaternion(0.7071f, 0, 0, 0.7071f);
                        break;
                    }

            }
        }

        return component;
    }
    MeshCollider CloneComponent(MeshCollider originalComponent, GameObject newParent)
    {
        MeshCollider component = newParent.AddComponent<MeshCollider>();

        CopyColliderProps(originalComponent, component);
        component.sharedMesh = originalComponent.sharedMesh;
        component.inflateMesh = originalComponent.inflateMesh;
        component.convex = originalComponent.convex;
        component.skinWidth = originalComponent.skinWidth;

        return component;
    }

    void CopyColliderProps(Collider source, Collider target)
    {
        target.enabled = source.enabled;
        target.isTrigger = source.isTrigger;
        target.contactOffset = source.contactOffset;
        target.sharedMaterial = source.sharedMaterial;
    }

    GameObject CreateProxyVisualization(GameObject newParent, PrimitiveType type)
    {
        if (drawPhysicsProxy) { 
            GameObject vis = GameObject.CreatePrimitive(type);
            vis.GetComponent<MeshRenderer>().sharedMaterial = ProxyMaterial;
            vis.transform.parent = newParent.transform;

            Collider coll = vis.GetComponent<Collider>();
            if (coll != null) Destroy(coll);


            return vis;
        }
        return null;
    }

    GameObject GetProxyObject(Dictionary<GameObject, GameObject> proxies, GameObject original)
    {
        GameObject proxy;
        if (proxies.TryGetValue(original, out proxy))
        {
            return proxy;
        }
        proxy = new GameObject();
        proxy.name = original.name + "_physics_proxy";
        proxy.transform.parent = GetProxyObject(proxies, original.transform.parent.gameObject).transform;
        proxy.transform.localPosition = original.transform.localPosition;
        proxy.transform.localRotation = original.transform.localRotation;
        proxy.transform.localScale = original.transform.localScale;

        proxies[original] = proxy;
        return proxy;
    }
}
