using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using System.Collections.Generic;

namespace Completed
{
    public enum ObservationMode
    {
        PLAYER_TO_SHEEP_TO_EXIT,
        PLAYER_RELATIVE
    }

    //Player inherits from MovingObject, our base class for objects that can move, Enemy also inherits from this.
    public class PlayerAgent : Agent
    {
        public ObservationMode observationMode = ObservationMode.PLAYER_TO_SHEEP_TO_EXIT;

        public float movePenalty = -0.01f;
        public float sheepDistancePenalty = -1.0f;
        public float finishReward = 10.0f;
        public bool transformWorldToExit = true;
        public bool sortSheep = true;

        private Player player;
        private int lastAction = 0;
        private const int totalObservers = 44;

        void Start()
        {
            player = GetComponent<Player>();
        }

        public override void OnEpisodeBegin()
        {
            GameManager.instance.CreateNewLevel();
        }

        public Vector2 ExitSign()
        {
            if (transformWorldToExit)
            {
                Vector2 pos = GameManager.instance.exit.transform.position;
                pos -= new Vector2(3.5f, 3.5f); // TODO: Make this scale to other board sizes
                return new Vector2(
                        Mathf.Sign(pos.x),
                        Mathf.Sign(pos.y)
                        );
            }
            else
            {
                return new Vector2(1.0f, 1.0f);
            }
        }

        public void HandleRestartTest()
        {
            //AddReward(-1.0f);
        }

        public void HandleSheepScore()
        {
            Vector3 dest = GameManager.instance.exit.transform.position;
            foreach (Sheep sheep in GameManager.instance.sheep)
            {
                AddReward(sheepDistancePenalty * Vector3.Distance(sheep.transform.position, dest));
            }
        }

        public void HandleAttemptMove()
        {
            // TODO: Change the reward below as appropriate. If you want to add a cost per move, you could change the reward to -1.0f (for example).
            AddReward(movePenalty);
        }

        public void HandleFinishlevel(bool restart)
        {
            if (restart)
                return;
            AddReward(finishReward);
        }

        public void HandleLevelRestart(bool gameOver)
        {
            if (gameOver)
            {
                Academy.Instance.StatsRecorder.Add("Level Reached", GameManager.instance.level);
                EndEpisode();
            }   
            else
            {
                // Probably *is* best to consider episodes finished when the exit is reached
                EndEpisode();
            }

        }

        public override void CollectObservations(VectorSensor sensor)
        {
            switch(observationMode)
            {
                case ObservationMode.PLAYER_TO_SHEEP_TO_EXIT:
                    CollectObservationsPlayerSheepExit(sensor);
                    break;
                case ObservationMode.PLAYER_RELATIVE:
                    CollectObservationsPlayerRelative(sensor);
                    break;
            }
        }

        private List<Sheep> GetSheep()
        {
            Vector2 pos = player.transform.position;
            List<Sheep> result = GameManager.instance.sheep;
            if (sortSheep)
            {
                result.Sort((a, b) =>
                        Vector2.Distance(a.transform.position, pos).CompareTo(
                            Vector2.Distance(b.transform.position, pos)));
            }
            return result;
        }

        private void CollectObservationsPlayerSheepExit (VectorSensor sensor)
        {
            int count = 0;
            foreach (Sheep sheep in GetSheep())
            {
                // Player to sheep vector
                Vector2 player2Sheep = sheep.transform.position - player.transform.position;
                player2Sheep *= ExitSign();
                sensor.AddObservation(player2Sheep);

                // Sheep to exit vector
                Vector2 sheep2Exit = sheep.transform.position - GameManager.instance.exit.transform.position;
                sheep2Exit *= ExitSign();
                sensor.AddObservation(sheep2Exit);

                count += 4;
            }

            // Add difference between player and sheep
            for (int i = 0; i < totalObservers - count; i++)
            {
                sensor.AddObservation(0.0f);
            }

            base.CollectObservations(sensor);
        }

        private void CollectObservationsPlayerRelative (VectorSensor sensor)
        {
            Vector2 player2Exit = GameManager.instance.exit.transform.position - player.transform.position;
            Debug.Log("Player2Exit: " + player2Exit);
            player2Exit *= ExitSign();
            sensor.AddObservation(player2Exit);

            int count = 2;
            foreach (Sheep sheep in GetSheep())
            {
                // Player to sheep vector
                Vector2 player2Sheep = sheep.transform.position - player.transform.position;
                player2Sheep *= ExitSign();
                sensor.AddObservation(player2Sheep);

                count += 2;
            }

            // Add difference between player and sheep
            for (int i = 0; i < totalObservers - count; i++)
            {
                sensor.AddObservation(0.0f);
            }

            base.CollectObservations(sensor);
        }

        private bool CanMove()
        {
            return !(player.isMoving || player.levelFinished || player.gameOver || GameManager.instance.doingSetup);
        }

        public override void OnActionReceived(float[] vectorAction)
        {
            //If it's not the player's turn, exit the function.
            if (!CanMove()) return;

            lastAction = (int)vectorAction[0] + 1; // To allow standing still as an action, remove the +1 and change "Branch 0 size" to 5.

            Vector2 signf = ExitSign();
            Vector2Int sign = new Vector2Int(Mathf.RoundToInt(signf.x), Mathf.RoundToInt(signf.y));
            switch (lastAction)
            {
                case 0:
                    break;
                case 1:
                    player.AttemptMove<Wall>(-sign.x, 0);
                    break;
                case 2:
                    player.AttemptMove<Wall>(sign.x, 0);
                    break;
                case 3:
                    player.AttemptMove<Wall>(0, -sign.y);
                    break;
                case 4:
                    player.AttemptMove<Wall>(0, sign.y);
                    break;
                default:
                    break;
            }
        }

        public override void Heuristic(float[] actionsOut)
        {
            GameManager.instance.HandleHeuristicMode();
            GetComponent<DecisionRequester>().DecisionPeriod = 1;

            //If it's not the player's turn, exit the function.
            if (!CanMove())
            {
                actionsOut[0] = lastAction;
                return;
            }

            int horizontal = 0;     //Used to store the horizontal move direction.
            int vertical = 0;       //Used to store the vertical move direction.

            //Check if we are running either in the Unity editor or in a standalone build.
#if UNITY_STANDALONE || UNITY_WEBPLAYER

            //Get input from the input manager, round it to an integer and store in horizontal to set x axis move direction
            horizontal = (int)(Input.GetAxisRaw("Horizontal"));

            //Get input from the input manager, round it to an integer and store in vertical to set y axis move direction
            vertical = (int)(Input.GetAxisRaw("Vertical"));

            //Check if moving horizontally, if so set vertical to zero.
            if (horizontal != 0)
            {
                vertical = 0;
            }
            //Check if we are running on iOS, Android, Windows Phone 8 or Unity iPhone
#elif UNITY_IOS || UNITY_ANDROID || UNITY_WP8 || UNITY_IPHONE

            //Check if Input has registered more than zero touches
            if (Input.touchCount > 0)
            {
                //Store the first touch detected.
                Touch myTouch = Input.touches[0];

                //Check if the phase of that touch equals Began
                if (myTouch.phase == TouchPhase.Began)
                {
                    //If so, set touchOrigin to the position of that touch
                    touchOrigin = myTouch.position;
                }

                //If the touch phase is not Began, and instead is equal to Ended and the x of touchOrigin is greater or equal to zero:
                else if (myTouch.phase == TouchPhase.Ended && touchOrigin.x >= 0)
                {
                    //Set touchEnd to equal the position of this touch
                    Vector2 touchEnd = myTouch.position;

                    //Calculate the difference between the beginning and end of the touch on the x axis.
                    float x = touchEnd.x - touchOrigin.x;

                    //Calculate the difference between the beginning and end of the touch on the y axis.
                    float y = touchEnd.y - touchOrigin.y;

                    //Set touchOrigin.x to -1 so that our else if statement will evaluate false and not repeat immediately.
                    touchOrigin.x = -1;

                    //Check if the difference along the x axis is greater than the difference along the y axis.
                    if (Mathf.Abs(x) > Mathf.Abs(y))
                        //If x is greater than zero, set horizontal to 1, otherwise set it to -1
                        horizontal = x > 0 ? 1 : -1;
                    else
                        //If y is greater than zero, set horizontal to 1, otherwise set it to -1
                        vertical = y > 0 ? 1 : -1;
                }
            }

#endif //End of mobile platform dependendent compilation section started above with #elif

            if (horizontal == 0 && vertical == 0)
            {
                actionsOut[0] = 0;
            }
            else if (horizontal < 0)
            {
                actionsOut[0] = 1;
            }
            else if (horizontal > 0)
            {
                actionsOut[0] = 2;
            }
            else if (vertical < 0)
            {
                actionsOut[0] = 3;
            }
            else if (vertical > 0)
            {
                actionsOut[0] = 4;
            }

            actionsOut[0] = actionsOut[0] - 1; // TODO: Remove this line if zero movement is allowed
        }
    }
}
