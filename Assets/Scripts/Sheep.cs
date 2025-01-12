﻿using UnityEngine;
using System;
using System.Collections;

namespace Completed
{
	//Sheep inherits from MovingObject, our base class for objects that can move, Player also inherits from this.
	public class Sheep : MovingObject
	{
		public int playerDamage; 							//The amount of food points to subtract from the player when attacking.
		public AudioClip attackSound1;						//First of two audio clips to play when attacking the player.
		public AudioClip attackSound2;						//Second of two audio clips to play when attacking the player.
		
		
		private Animator animator;							//Variable of type Animator to store a reference to the sheep's Animator component.
		private GameObject target;                          //Transform to attempt to move toward each turn.
		private Player player;
		private bool skipMove;                              //Boolean to determine whether or not sheep should skip a turn or move this turn.
		public float distToExit;
		
		//Start overrides the virtual Start function of the base class.
		protected override void Start ()
		{
			//Register this sheep with our instance of GameManager by adding it to a list of Sheep objects. 
			//This allows the GameManager to issue movement commands.
			GameManager.instance.AddSheepToList (this);
			
			//Get and store a reference to the attached Animator component.
			animator = GetComponent<Animator> ();
			
			//Find the Player GameObject using it's tag and store a reference to its transform component.
			target = GameObject.FindGameObjectWithTag ("Player");

			player = target.GetComponent<Player>();
			//Call the start function of our base class MovingObject.
			base.Start ();
		}
		
		
		//Override the AttemptMove function of MovingObject to include functionality needed for Sheep to skip turns.
		//See comments in MovingObject for more on how base AttemptMove function works.
		public override void AttemptMove <T> (int xDir, int yDir)
		{
			//Check if skipMove is true, if so set it to false and skip this turn.
			if(skipMove)
			{
				skipMove = false;
				return;
				
			}

			float oldPosDis = Vector3.Distance(GameManager.instance.exit.transform.position, transform.position);

			//Call the AttemptMove function from MovingObject.
			base.AttemptMove <T> (xDir, yDir);
			
			//Now that Sheep has moved, set skipMove to true to skip next move.
			skipMove = true;

			distToExit = Vector3.Distance(GameManager.instance.exit.transform.position, transform.position);
		}
		
		
		//MoveSheep is called by the GameManger each turn to tell each Sheep to try to away from the player.
		public void MoveSheep ()
		{
			if (Vector3.Distance(transform.position, target.transform.position) > 3)
				return;

            Vector2 pos = new Vector2(transform.position.x, transform.position.y);
            Vector2 tpos = new Vector2(target.transform.position.x, target.transform.position.y);

            // Sort the possible directions by which one will get the sheep the furthest from the target (player)
            Vector2Int[] directions = {Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right};
            Array.Sort(directions, delegate (Vector2Int a, Vector2Int b) {
                    Vector2 adiff = pos + a - tpos;
                    Vector2 bdiff = pos + b - tpos;
                    return adiff.sqrMagnitude > bdiff.sqrMagnitude ? -1 : 1;
                    });
            

            // Look for a direction in which we can move
            foreach (var dir in directions)
            {
                if (CanMove(dir.x, dir.y))
                {
                    //Call the AttemptMove function and pass in the generic parameter Player, because Sheep is moving and expecting to potentially encounter a Player
                    AttemptMove <Player> (dir.x, dir.y);
                    break;
                }
            }
		}
		
		
		//OnCantMove is called if Sheep attempts to move into a space occupied by a Player, it overrides the OnCantMove function of MovingObject 
		//and takes a generic parameter T which we use to pass in the component we expect to encounter, in this case Player
		protected override void OnCantMove <T> (T component)
		{
			//Declare hitPlayer and set it to equal the encountered component.
			Player hitPlayer = component as Player;
			
			//Set the attack trigger of animator to trigger Sheep attack animation.
			animator.SetTrigger ("sheepAttack");
			
			//Call the RandomizeSfx function of SoundManager passing in the two audio clips to choose randomly between.
			SoundManager.instance.RandomizeSfx (attackSound1, attackSound2);
		}

		private void OnTriggerEnter2D(Collider2D collision)
		{
			// Check if sheep hits the exit sign
			if (collision.tag == "Exit")
			{
				// Disable the sheep
				this.gameObject.SetActive(false);
				GameManager.instance.RemoveSheepFromList(this);
				GameManager.instance.CheckIfGameOver();
			}
		}
	}
}
