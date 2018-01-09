using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BRB.PUNBasicTutorial
{
    [RequireComponent(typeof(PhotonView))]
    public class GameManager : Photon.PunBehaviour
    {
        public GameObject playerA;
        public GameObject playerB;

        private int scoreA = 0;
        private int scoreB = 0;

        public TextMesh scoreAText;
        public TextMesh scoreBText;

        public GameObject Ball;

        private GameObject playerObj;
        public GameObject Player {
            get { return playerObj; }
        }

        private void Start()
        {
            // master client sets ownership of player objects
            if (PhotonNetwork.isMasterClient)
            {
                PhotonView playerAPV = playerA.GetComponent<PhotonView>();
                PhotonView playerBPV = playerB.GetComponent<PhotonView>();

                int counter = 0;
                for (int i = 0; i < PhotonNetwork.playerList.Length; i++) {
                    if (PhotonNetwork.playerList[i] != PhotonNetwork.player) {
                        if (counter == 0)
                        {
                            playerAPV.TransferOwnership(PhotonNetwork.playerList[i]);
                        }
                        else {
                            playerBPV.TransferOwnership(PhotonNetwork.playerList[i]);
                        }
                        
                        this.photonView.RPC("SetPingPongPlayer", PhotonTargets.OthersBuffered, PhotonNetwork.playerList[i].NickName, counter);
                        counter++;
                    }
                }
            } 
        }

        [PunRPC]
        public void SetPingPongPlayer(string playerName, int playerID)
        {
            Debug.Log("RPC received: playerName: " + playerName + ", playerID: " + playerID);

            if (PhotonNetwork.playerName == playerName) {
                // message is for us, we should do something

                playerObj = playerID == 0 ? playerA : playerB;
                PhotonView playerPV = playerObj.GetComponent<PhotonView>();
                playerPV.TransferOwnership(PhotonNetwork.player);

                InputManager im = FindObjectOfType<InputManager>();
                im.player = playerObj;
                im.playerRB = playerObj.GetComponent<Rigidbody>();
                im.zStartPos = playerObj.transform.position.z;
            }
        }  

        private void AddPointForPlayer(int player)
        {
            if (player == 0)
            {
                scoreA++;
            }
            else
            {
                scoreB++;
            }

            // TODO: update scores
            scoreAText.text = "" + scoreA;
            scoreBText.text = "" + scoreB;
        }


        private int receivedSamplesCounter = 0;
        private int samplesForPingAverage = 10;
        private float currentPingAverage = 0;
        private float currentTimeBetweenUpdateAverage = 0;
        private float totalPingAverage = 0;
        private float totalLatency = 0;
        private float totalTimeBetweenUpdates = 0;
        float lastReceivedTime = 0;

        // this is used to display the average ping
        // idea: measure the time between two serializations to get detailed information about the general update rate and ping
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            float currenTime = Time.realtimeSinceStartup;
            if (stream.isWriting)
            {
                stream.SendNext(currenTime);
            }
            else
            {
                receivedSamplesCounter++;
                float sendTime = (float)stream.ReceiveNext();

                float timeBetweenUpdates = lastReceivedTime > 0 ? currenTime - lastReceivedTime : 0;
                totalTimeBetweenUpdates += timeBetweenUpdates;

                // important: there are a multitude of factors influencing latency and the time between two updates
                // influencing factors:
                // send rate: 
                //  - can be adjusted in each application upon build time, 
                //  - defines the amount of updates per second,
                //  - the high this is the higher the used bandwidth is and also synchronicity is increased

                // server wait time
                // (need to reallocate where I can adjust this)
                // time the server waits before a new update is sent
                // this is due to the fact that the network can be relaxed by aggregating messanges,
                // this is a technique to decrease the overhead needed to send information
                // it introduces an artificial latency to lower bandwidth usage
                // very suited for large servers

                float latency =  currenTime - sendTime;
                totalLatency += latency;

                if (receivedSamplesCounter == samplesForPingAverage)
                {
                    currentPingAverage = totalLatency / samplesForPingAverage;
                    totalPingAverage = (currentPingAverage + totalPingAverage) / 2;
                    currentTimeBetweenUpdateAverage = totalTimeBetweenUpdates / samplesForPingAverage;

                    totalTimeBetweenUpdates = 0;
                    receivedSamplesCounter = 0;
                    totalLatency = 0;

                    PingDisplay pd = FindObjectOfType<PingDisplay>();
                    if(pd != null)
                    {
                        pd.UpdatePing(currentPingAverage, totalPingAverage, currentTimeBetweenUpdateAverage);
                    }
                }

                lastReceivedTime = currenTime;
            }
            
        }

        /// <summary>
        /// Called when the local player left the room. We need to load the launcher scene.
        /// </summary>
        public override void OnLeftRoom()
        {
            SceneManager.LoadScene("Launcher");
        }

        public void LeaveRoom()
        {
            PhotonNetwork.LeaveRoom();
        }

        public void BackToLauncher()
        {
            if (PhotonNetwork.isMasterClient)
            {
                PhotonNetwork.LoadLevel("Launcher");
            }
            else
            {
                Debug.Log("unable to execute command, client is not the master client");
            }
        }

        //void LoadArena()
        //{
        //    if (!PhotonNetwork.isMasterClient)
        //    {
        //        Debug.LogError("PhotonNetwork : Trying to Load a level but we are not the master Client");
        //    }
        //    Debug.Log("PhotonNetwork : Loading Level : " + PhotonNetwork.room.PlayerCount);
        //    PhotonNetwork.LoadLevel("Room for " + 1);
        //}


        //public override void OnPhotonPlayerConnected(PhotonPlayer other)
        //{
        //    Debug.Log("OnPhotonPlayerConnected() " + other.NickName); // not seen if you're the player connecting

        //    if (PhotonNetwork.isMasterClient)
        //    {
        //        Debug.Log("OnPhotonPlayerConnected isMasterClient " + PhotonNetwork.isMasterClient); // called before OnPhotonPlayerDisconnected
        //        LoadArena();
        //    }
        //}

        internal void StartGame()
        {
            Debug.Log("Game started");
            // TODO: start game by shooting presented ball from master client to other player
            Rigidbody ballRB = Ball.GetComponent<Rigidbody>();
            ballRB.velocity = new Vector3(0, 0, 10);
        }

        public override void OnPhotonPlayerDisconnected(PhotonPlayer other)
        {
            Debug.Log("OnPhotonPlayerDisconnected() " + other.NickName); // seen when other disconnects

            if (PhotonNetwork.isMasterClient)
            {
                Debug.Log("OnPhotonPlayerDisonnected isMasterClient " + PhotonNetwork.isMasterClient); // called before OnPhotonPlayerDisconnected
                //LoadArena();
                LeaveRoom();
            }
        }
    }
}
