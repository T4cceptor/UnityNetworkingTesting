using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using UnityEngine;

namespace BRB.PUNBasicTutorial { 
    public class Launcher : Photon.PunBehaviour
    {

        string _gameVersion = "1";

        void Awake()
        {
            // #Critical
            // we don't join the lobby. There is no need to join a lobby to get the list of rooms.
            PhotonNetwork.autoJoinLobby = false;

            // #Critical
            // this makes sure we can use PhotonNetwork.LoadLevel() on the master client and all clients in the same room sync their level automatically
            PhotonNetwork.automaticallySyncScene = true;
        }

        // Use this for initialization
        void Start()
        {
            Connect();
        }

        public void Connect()
        {
            // we check if we are connected or not, we join if we are , else we initiate the connection to the server.
            if (PhotonNetwork.connected)
            {
                // #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnPhotonRandomJoinFailed() and we'll create one.
                PhotonNetwork.JoinRandomRoom();
            }
            else
            {
                // #Critical, we must first and foremost connect to Photon Online Server.
                PhotonNetwork.ConnectUsingSettings(_gameVersion);
            }

        }

        private void OnConnectedToServer()
        {
            Debug.Log("DemoAnimator/Launcher: OnConnectedToMaster() was called by PUN");

            // #Critical: 
            // The first we try to do is to join a potential existing room. 
            // If there is, good, else, we'll be called back with OnPhotonRandomJoinFailed()  
            PhotonNetwork.JoinRandomRoom();
        }

        private void OnDisconnectedFromServer(NetworkDisconnection info)
        {
            Debug.LogWarning("DemoAnimator/Launcher: OnDisconnectedFromPhoton() was called by PUN");
        }
    }
}