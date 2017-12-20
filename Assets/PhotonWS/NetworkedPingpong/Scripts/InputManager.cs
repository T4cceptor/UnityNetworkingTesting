using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BRB.PUNBasicTutorial
{
    public class InputManager : MonoBehaviour
    {
        public GameObject player;
        public Rigidbody playerRB;

        private GameManager gm;

        static public float speed = 10f;
        
        private Vector3 moveLeft = new Vector3(-speed, 0, 0);
        private Vector3 moveRight = new Vector3(speed, 0, 0);
        private Vector3 moveUp = new Vector3(0, 0, speed);
        private Vector3 moveDown = new Vector3(0, 0, -speed);

        public float zStartPos;
        private float maxZMovement = 10f;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Debug.Log("space presed");
                FindObjectOfType<GameManager>().StartGame();
            }

            if (player == null) { return; }

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
        }
    }
}
