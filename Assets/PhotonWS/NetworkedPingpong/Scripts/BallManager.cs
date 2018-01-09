using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class BallManager : MonoBehaviour {

    PhotonView thisPV;

    public Collider playerAGoal;
    public Collider playerBGoal;

    private float factor = 3.0f;

    private void Start()
    {
        thisPV = GetComponent<PhotonView>();
        thisPV.TransferOwnership(PhotonNetwork.masterClient);
    }

    //private void OnTriggerEnter(Collider other)
    //{
    //    if (other == playerAGoal)
    //    {

    //    }
    //    if (other == playerBGoal) { }

    //}

    //private void OnCollisionEnter(Collision collision)
    //{

    //    Rigidbody thisRB = GetComponent<Rigidbody>();
    //    //thisRB.velocity += thisRB.velocity * factor;

    //    // get the PhotonView of the collider, if one is present
    //    PhotonView pv = collision.collider.GetComponent<PhotonView>();
    //    if (pv != null)
    //    {
    //        // if we collided with a PhotonView object, 
    //        // we check if the owner of the PhotonView is the same as the owner of the ball
    //        // if this is not the case we want to transfer ownership of the ball
    //        //if (pv.ownerId != thisPV.ownerId && !PhotonNetwork.isMasterClient)
    //        //{
    //        //    thisPV.TransferOwnershipLocally(pv.owner);
    //        //    // TODO: send message to master client that we are controlling the object now
    //        //}
    //        thisPV.TransferOwnership(pv.owner);
    //    }
    //}

    //private void OnCollisionExit(Collision collision)
    //{
    //    // if we exit a collision we transfer ownership back to the MasterClient
    //    //if (thisPV.owner != PhotonNetwork.masterClient)
    //    //{
    //    //    thisPV.TransferOwnershipLocally(PhotonNetwork.masterClient);
    //    //}
    //    //else {
            
    //    //}
    //    thisPV.TransferOwnership(PhotonNetwork.masterClient);

    //}
}
