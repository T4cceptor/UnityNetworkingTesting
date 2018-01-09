using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PingDisplay : MonoBehaviour {

    [SerializeField]
    TextMesh pingText;

    [SerializeField]
    TextMesh avgPingText;

    float avgPing;
    int avgPingWindowSize = 10;
    int[] pings = new int[10];
    int counter = 0;
	
	// Update is called once per frame
	void Update () {
        //// Get ping from photon
        //int ping = PhotonNetwork.GetPing();

        //// display current ping
        //pingText.text = "Ping: " + ping;

        //// save current ping for calculation of average ping over avgPingWindowSize samples
        //pings[counter] = ping;

        //// increment counter, and calculate the modulo from it to stay in range of the array
        //counter = (counter + 1) % avgPingWindowSize;

        //float pingSum = 0.0f;
        //for (int i = 0; i < pings.Length; i++)
        //{
        //    pingSum += pings[i];
        //}
        //avgPing = pingSum / avgPingWindowSize;
        //avgPingText.text = "Avg. Ping: " + avgPing;

    }

    public void UpdatePing(float currentPing, float totalPing, float timeBetweenUpdates)
    {
        pingText.text = "Current avg. ping: " + currentPing + "\n timeBetweenUpdates: " + timeBetweenUpdates;
        avgPingText.text = "Total avg. ping: " + totalPing;
    }
}
