using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallManager : MonoBehaviour {

    public Collider playerAGoal;
    public Collider playerBGoal;

    private float factor = 3.0f;

    private void OnTriggerEnter(Collider other)
    {
        if (other == playerAGoal)
        {

        }
        if (other == playerBGoal) { }

    }

    //private void OnCollisionEnter(Collision collision)
    //{
    //    Rigidbody thisRB = GetComponent<Rigidbody>();
    //    thisRB.velocity += thisRB.velocity * factor;
    //}
}
