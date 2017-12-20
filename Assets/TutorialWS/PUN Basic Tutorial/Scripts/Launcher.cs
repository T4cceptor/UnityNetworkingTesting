using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using UnityEngine;
using UnityEngine.UI;

namespace BRB.PUNBasicTutorial { 
    public class Launcher : Photon.PunBehaviour
    {
        #region public variables
        public PhotonLogLevel Loglevel = PhotonLogLevel.Informational;

        [Tooltip("The maximum number of players per room. When a room is full, it can't be joined by new players, and so new room will be created")]
        public byte MaxPlayersPerRoom = 4;

        [Tooltip("The Ui Panel to let the user enter name, connect and play")]
        public GameObject controlPanel;
        public Text buttonText;

        [Tooltip("The UI Label to inform the user that the connection is in progress")]
        public GameObject progressLabel;
        public Text progressText;

        public int SendRate = 100;
        #endregion

        #region private variables
        private string _gameVersion = "1";

        #endregion

        #region unity magic
        void Awake()
        {
            // #Critical
            // we don't join the lobby. There is no need to join a lobby to get the list of rooms.
            PhotonNetwork.autoJoinLobby = false;

            // #Critical
            // this makes sure we can use PhotonNetwork.LoadLevel() on the master client and all clients in the same room sync their level automatically
            PhotonNetwork.automaticallySyncScene = true;

            PhotonNetwork.logLevel = Loglevel;

            Debug.Log("Send rate was: " + PhotonNetwork.sendRate);
            PhotonNetwork.sendRate = SendRate;
            PhotonNetwork.sendRateOnSerialize = SendRate;
            Debug.Log("Send rate now: " + PhotonNetwork.sendRate);
        }

        // Use this for initialization
        void Start()
        {
            // Connect(); // old tutorial code
            progressLabel.SetActive(false);
            controlPanel.SetActive(true);
        }
        #endregion

        private bool tryingToRejoinNewRoom = false;

        public void Connect()
        {
            ToggleStatus(true, "Connecting...");
            PhotonNetwork.ConnectUsingSettings(_gameVersion);
        }

        public void JoinRoom()
        {
            // we check if we are connected or not, we join if we are , else we initiate the connection to the server.
            if (PhotonNetwork.connected)
            {
                // #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnPhotonRandomJoinFailed() and we'll create one.
                PhotonNetwork.JoinRandomRoom();
            }
        }

        private void ToggleStatus(bool showStatus, string text)
        {
            progressLabel.SetActive(showStatus);
            controlPanel.SetActive(!showStatus);

            if (text != null)
            {
                if (showStatus)
                {
                    progressText.text = text;
                }
                else
                {
                    buttonText.text = text;
                }
            }
        }


        #region photon magic
        public override void OnConnectedToMaster()
        {
            Debug.Log("DemoAnimator/Launcher: OnConnectedToMaster() was called by PUN");

            // #Critical: 
            // The first we try to do is to join a potential existing room. 
            // If there is, good, else, we'll be called back with OnPhotonRandomJoinFailed()  
            JoinRoom();
        }

        public override void OnJoinedRoom()
        {
            Debug.Log("SUCCESSFULLY JOINED A ROOM with name: " + PhotonNetwork.playerName);

            // #Critical: We only load if we are the first player, else we rely on  PhotonNetwork.automaticallySyncScene to sync our instance scene.
            CheckForGameStart();

            if (PhotonNetwork.isMasterClient) {
                ToggleStatus(true, "MASTERCLIENT, Waiting for other players ... " + PhotonNetwork.playerList.Length);
            }
        }

        public override void OnPhotonPlayerConnected(PhotonPlayer newPlayer)
        {
            CheckForGameStart();

            if (PhotonNetwork.isMasterClient)
            {
                ToggleStatus(true, "MASTERCLIENT, Waiting for other players ... " + PhotonNetwork.playerList.Length);
            }
        }

        private void CheckForGameStart()
        {
            if (PhotonNetwork.room.PlayerCount == 3 && PhotonNetwork.isMasterClient)
            {
                Debug.Log("We load the game");

                PhotonNetwork.room.IsOpen = false;

                // #Critical
                // Load the Room Level. 
                PhotonNetwork.LoadLevel("PingpongGame");
            }
            else
            {
                ToggleStatus(true, "Waiting for player 2.");
            }
        }

        public override void OnLeftRoom()
        {
            if(tryingToRejoinNewRoom == true)
            {
                PhotonNetwork.JoinRandomRoom();
                tryingToRejoinNewRoom = false;
            }
            
        }

        public override void OnPhotonRandomJoinFailed(object[] codeAndMsg)
        {
            Debug.Log("DemoAnimator/Launcher:OnPhotonRandomJoinFailed() was called by PUN. No random room available, so we create one.\n"
                + "Calling: PhotonNetwork.CreateRoom(null, new RoomOptions() {maxPlayers = 4}, null);");

            // #Critical: we failed to join a random room, maybe none exists or they are all full. No worries, we create a new room.
            PhotonNetwork.CreateRoom("testRoom1", new RoomOptions() { MaxPlayers = 4 }, null);
        }

        public override void OnDisconnectedFromPhoton()
        {
            Debug.LogWarning("DemoAnimator/Launcher: OnDisconnectedFromPhoton() was called by PUN");
            progressLabel.SetActive(false);
            controlPanel.SetActive(true);
        }
        #endregion
    }
}