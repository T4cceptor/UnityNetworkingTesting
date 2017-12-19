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

        private void Awake()
        {
            if (playerObj == null)
            {
                PhotonView playerAPV = playerA.GetComponent<PhotonView>();
                PhotonView playerBPV = playerB.GetComponent<PhotonView>();

                PhotonPlayer thisPlayer = PhotonNetwork.player;
                PhotonPlayer otherPlayer = PhotonNetwork.otherPlayers.Length > 0 && PhotonNetwork.otherPlayers[0] != null ? PhotonNetwork.otherPlayers[0] : null;

                if (PhotonNetwork.isMasterClient)
                {
                    playerObj = playerA;
                    playerAPV.TransferOwnership(thisPlayer);
                    playerBPV.TransferOwnership(otherPlayer);
                }
                else
                {
                    playerObj = playerB;
                    playerBPV.TransferOwnership(thisPlayer);
                    playerAPV.TransferOwnership(otherPlayer);
                }
            }
        }

        private void GetReadyForStart()
        {

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
