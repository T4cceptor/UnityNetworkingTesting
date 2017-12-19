using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BRB.PUNBasicTutorial
{
    public class InputManager : MonoBehaviour
    {
        private GameObject player;
        private Rigidbody playerRB;
        private GameManager gm;

        static public float speed = 10f;

        public Vector3 playerADirection;
        private Vector3 moveLeft = new Vector3(-speed, 0, 0);
        private Vector3 moveRight = new Vector3(speed, 0, 0);
        private Vector3 moveUp = new Vector3(0, 0, speed);
        private Vector3 moveDown = new Vector3(0, 0, -speed);

        private float zStartPos;
        private float maxZMovement = 10f;

        void Start()
        {
            gm = FindObjectOfType<GameManager>();
            player = gm.Player;
            playerRB = player.GetComponent<Rigidbody>();

            zStartPos = player.transform.position.z;
        }

        private void Update()
        {
            Vector3 velocity = Vector3.zero;
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                velocity += moveLeft;
            }
            if (Input.GetKey(KeyCode.RightArrow))
            {
                velocity += moveRight;
            }
            if (Input.GetKey(KeyCode.UpArrow))
            {
                velocity += moveUp;
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                velocity += moveDown;
            }
            playerRB.velocity = velocity;

            Vector3 currentPosition = player.transform.position;
            if (player.transform.position.x > 8 || player.transform.position.x < -8)
            {
                player.transform.position = currentPosition;
            }

            if (player.transform.position.z > zStartPos + maxZMovement || player.transform.position.z < zStartPos - maxZMovement)
            {
                player.transform.position = currentPosition;
            }


            if (Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("space presed");
                gm.StartGame();
            }
        }

    }
}
