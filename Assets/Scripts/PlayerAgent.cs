using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

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
        public ObservationMode mode = ObservationMode.PLAYER_TO_SHEEP_TO_EXIT;

        private Player player;
        private int lastAction = 0;
        private const int totalObservers = 44;

        private float movePenalty = -0.01f;
        private float sheepDistancePenalty = -1.0f;

        void Start()
        {
            player = GetComponent<Player>();
        }

        public override void OnEpisodeBegin()
        {
            GameManager.instance.CreateNewLevel();
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
            float reward = 5;
            AddReward(reward);
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
            switch(mode)
            {
                case ObservationMode.PLAYER_TO_SHEEP_TO_EXIT:
                    CollectObservationsPlayerSheepExit(sensor);
                    break;
                case ObservationMode.PLAYER_RELATIVE:
                    CollectObservationsPlayerRelative(sensor);
                    break;
            }
        }

        private void CollectObservationsPlayerSheepExit (VectorSensor sensor)
        {
            int count = 0;
            foreach (Sheep sheep in GameManager.instance.sheep)
            {
                // Player to sheep vector
                Vector2 player2Sheep = sheep.transform.position - player.transform.position;
                sensor.AddObservation(player2Sheep);

                // Sheep to exit vector
                Vector2 sheep2Exit = sheep.transform.position - GameManager.instance.exit.transform.position;
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
            Vector2 player2Exit = GameManager.instance.exit.transform.position;
            sensor.AddObservation(player2Exit);

            int count = 2;
            foreach (Sheep sheep in GameManager.instance.sheep)
            {
                // Player to sheep vector
                Vector2 player2Sheep = sheep.transform.position - player.transform.position;
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

            switch (lastAction)
            {
                case 0:
                    break;
                case 1:
                    player.AttemptMove<Wall>(-1, 0);
                    break;
                case 2:
                    player.AttemptMove<Wall>(1, 0);
                    break;
                case 3:
                    player.AttemptMove<Wall>(0, -1);
                    break;
                case 4:
                    player.AttemptMove<Wall>(0, 1);
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
