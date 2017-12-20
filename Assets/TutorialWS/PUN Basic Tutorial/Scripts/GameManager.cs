using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BRB.PUNBasicTutorial
{
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
