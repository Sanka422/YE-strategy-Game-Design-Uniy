using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Core : MonoBehaviour, IPlayerInterface {
	public GameObject marker;
	private List<Unit> units;
	private bool waiting = false;

	/// <summary>
	/// Each turn a player will perform 
	/// selecting an PC,
	/// move then act, act then move, double movement, double action
	/// end phase
	/// </summary>
	private enum Phase {
		SELECT,
		MOVE,
		ATTACK
	}

	private Phase phase = Phase.SELECT;
	public int PLAYERS = 2;
	private int player = 0;
	private HexPosition mouse = null;
	private HexPosition selection = null;
	private HexPosition moveFromPos = null;
	private HexPosition[] path = null;
	private AI ai;
	bool gameOver = false;
	bool modeSelected = false;
	bool computerPlayer;

	public void wait () {
		waiting = true;
	}

	public void moveComplete () {
		waiting = false;
	}

	public void attackComplete () {
		//Since it currently doesn't wait for an attack, this is empty.
	}

	public void addUnit (Unit unit) {
		units.Add (unit);
		unit.Coordinates = new HexPosition (unit.transform.position);
	}

	public void removeUnit (Unit unit) {
		units.Remove (unit);
	}

	//Returns true if there are any selectable units.
	private bool selectSelectable () {
		bool nonempty = false;
		foreach (Unit unit in units) {
			if (unit.PLAYER == player && unit.Status != Unit.State.WAIT) {
				unit.Coordinates.select ("Selectable");
				nonempty = true;
			}
		}
		return nonempty;
	}

	//TODO: Move to Unit.cs
	private bool isAttackable (Unit attacker, Unit attacked, HexPosition coordinates) {
		return attacked.PLAYER != player && coordinates.dist (attacked.Coordinates) <= attacker.RANGE;
	}

	private bool isAttackable (Unit attacker, Unit attacked) {
		return isAttackable (attacker, attacked, attacker.Coordinates);
	}

	//Returns true if there's at least one attackable unit.
	private bool selectAttackable (Unit attacker, HexPosition coordinates) {
		bool nonempty = false;
		foreach (Unit unit in units) {
			if (isAttackable (attacker, unit, coordinates)) {
				unit.Coordinates.select ("Attack");
				nonempty = true;
			}
		}
		return nonempty;
	}

	//Returns true if there's at least one attackable unit.
	private bool selectAttackable (Unit attacker) {
		return selectAttackable (attacker, attacker.Coordinates);
	}


	/// <summary>
	/// return true if pos x is reachable
	/// </summary>
	/// <returns><c>true</c>, if movable was ised, <c>false</c> otherwise.</returns>
	/// <param name="unit">Unit.</param>
	/// <param name="coordinates">Coordinates.</param>
	private bool isMovable (Unit unit, HexPosition coordinates) {
		if (unit.Coordinates.dist (coordinates) <= unit.SPEED) {
			if (AStar.search (unit.Coordinates, coordinates, unit.SPEED).Length != 0) {
				if (!coordinates.containsKey ("Obstacle")) {
					return true;
				}
			}
		}
		return false;
	}

	private bool selectMovable (Unit unit) {
		bool nonempty = false;
		for (int i = unit.Coordinates.U - unit.SPEED; i <= unit.Coordinates.U + unit.SPEED; i++) {
			for (int j = unit.Coordinates.V - unit.SPEED; j <= unit.Coordinates.V + unit.SPEED; j++) {
				HexPosition moveAbleHex = new HexPosition (i, j);
				if (isMovable (unit, moveAbleHex)) {
					moveAbleHex.select ("Movable");
				}
			}
		}
		return nonempty;
	}



	void Start () {
		HexPosition.setColor ("Path", Color.yellow, 2);
		HexPosition.setColor ("Selection", Color.green, 3);
		HexPosition.setColor ("Selectable", Color.green, 4);
		HexPosition.setColor ("Attack", Color.red, 5);
		HexPosition.setColor ("Cursor", Color.blue, 6);
		HexPosition.setColor ("Movable", Color.cyan, 1);
		HexPosition.Marker = marker;
		foreach (GameObject child in GameObject.FindGameObjectsWithTag("Obstacle")) {
			HexPosition position = new HexPosition (child.transform.position);
			child.transform.position = position.getPosition ();
			position.flag ("Obstacle");
		}
		units = new List<Unit> (Object.FindObjectsOfType<Unit> ());
		foreach (Unit unit in units) {
			unit.setPlayerInterface (this, true);
		}
	}

	private void select () {
		if (mouse.isSelected ("Selectable")) {
			HexPosition.clearSelection ("Selectable");
			selection = mouse;
			mouse.select ("Selection");
			Unit unit = mouse.getUnit ();
			selectMovable (unit);
			moveFromPos = unit.Coordinates;
			switch (unit.Status) {
			case Unit.State.MOVE:
				phase = Phase.MOVE;
				break;
			case Unit.State.ATTACK:
				phase = Phase.ATTACK;
				break;
			default:
				print ("Error: Action " + unit.Status + " not implemented.");
				break;
			}
		}
	}

	public void endPhase () {
		HexPosition.clearSelection ();
		foreach (Unit unit in units) {	
			unit.newPhase ();
		}
		player = (player + 1) % PLAYERS;
		if (player == 0 || !computerPlayer) {
			selectSelectable ();
		}
	}

	private void unselect () {
		HexPosition.clearSelection ();
		selection = null;
		mouse.select ("Cursor");
		if (!(selectSelectable () || gameOver)) {
			endPhase ();
		}
		phase = Phase.SELECT;
	}

	private void checkGameOver () {
		gameOver = true;
		foreach (Unit unit in units) {
			if (unit.PLAYER != player) {
				gameOver = false;
				break;
			}
		}
	}

	private void actuallyAttack () {
		Unit unit = selection.getUnit ();
		unit.attack (mouse, unit.getDamage ());
		checkGameOver ();
		unselect ();
	}

	private void move () {
		if (mouse.Equals (selection)) {
			unselect ();
		} else if (!mouse.containsKey ("Unit")) {
			if (path.Length > 0) {
				Unit myUnit = selection.getUnit ();
				myUnit.move (path);
				HexPosition.clearSelection ();
				selection = mouse;
				selection.select ("Selection");
				if (selectAttackable (myUnit)) {
					phase = Phase.ATTACK;
				} else {
					myUnit.skipAttack ();
					unselect ();
				}
			}
		} 
	}

	private void attack () {
		if (mouse.isSelected ("Attack")) {
			actuallyAttack ();
		}
	}

	/// <summary>
	/// return the mouse position in the world map view
	/// </summary>
	/// <returns>The mouse hex.</returns>
	//
	private HexPosition getMouseHex () {
		Ray ray = Camera.main.ScreenPointToRay (Input.mousePosition);
		RaycastHit[] hits = Physics.RaycastAll (ray);
		if (hits.Length == 0) {
			return null;
		} else {
			float minDist = float.PositiveInfinity;
			int min = 0;
			for (int i = 0; i < hits.Length; ++i) {
				if (hits [i].distance < minDist) {
					minDist = hits [i].distance;
					min = i;
				}
			}
			return (new HexPosition (hits [min].point));
		}
	}

	void Update () {
		if (waiting || gameOver || !modeSelected) {
			return;
		}
		if (player == 1 && computerPlayer) {
			if (ai.go ()) {
				endPhase ();
			}
			checkGameOver ();
			return;
		}
		if (!Input.mousePresent) {
			mouse = null;
		} else {
			HexPosition newMouse = getMouseHex ();
			if (newMouse == null) {
				HexPosition.clearSelection ("Path");
				HexPosition.clearSelection ("Attack");
				path = null;
			} else {
				if (newMouse != mouse) {
					if (mouse != null) {
						mouse.unselect ("Cursor");
					}
					if (newMouse.containsKey ("Obstacle")) {	//The Obstacle tag is being used to make the tile unselectable.
						if (mouse != null && phase == Phase.MOVE) {
							HexPosition.clearSelection ("Path");
							HexPosition.clearSelection ("Attack");
							path = null;
						}
						mouse = null;
						return;
					}
					mouse = newMouse;
					//display where the cursor is pointing at
					mouse.select ("Cursor");
					//if is in move phase, also display the route toward where the cursor is current at
					if (phase == Phase.MOVE) {
						Unit unit = selection.getUnit ();
						HexPosition.clearSelection ("Path");
						HexPosition.clearSelection ("Attack");
						path = AStar.search (selection, mouse, unit.SPEED);
						HexPosition.select ("Path", path);
					}
				}
				if (Input.GetMouseButtonDown (0)) {
					switch (phase) {
					case Phase.SELECT:
						select ();
						break;
					case Phase.MOVE:
						move ();
						break;
					case Phase.ATTACK:
						attack ();
						break;
					default:
						print ("Error: Turn " + phase + " not implemented.");
						break;
					}
					return;
				} else if (Input.GetMouseButtonDown (1)) {
					HexPosition.clearSelection ("Path");
					HexPosition.clearSelection ("Attack");
					HexPosition.clearSelection ("Movable");
					HexPosition.clearSelection ("Selection");
					phase = Phase.SELECT;
					Unit unit = selection.getUnit ();
					unit.undoMovement (moveFromPos);
					unit.setState (Unit.State.MOVE);
					selectSelectable ();
				}
			}
			
		}
	}

	void OnGUI () {
		if (!modeSelected) {
			if (GUI.Button (new Rect (10, 10, 90, 20), "1 Player")) {
				selectSelectable ();
				computerPlayer = true;
				modeSelected = true;
				ai = new AI (units, 1);
				return;
			}
			if (GUI.Button (new Rect (10, 40, 90, 20), "2 Player")) {
				selectSelectable ();
				computerPlayer = false;
				modeSelected = true;
				return;
			}
			return;
		}
		if (gameOver) {
			GUIStyle style = new GUIStyle ();
			style.fontSize = 72;
			style.alignment = TextAnchor.MiddleCenter;
			GUI.Box (new Rect (10, 10, Screen.width - 20, Screen.height - 20), "Player " + (player + 1) + " Wins!", style);
			return;
		}
		if (waiting || (player == 1 && computerPlayer)) {
			return;
		}
		GUI.Box (new Rect (10, 10, 90, 20), "Player " + (player + 1));
		switch (phase) {
		case Phase.SELECT:
			GUI.Box (new Rect (10, 40, 90, 20), "Select");
			if (GUI.Button (new Rect (10, 70, 90, 20), "End Turn")) {
				endPhase ();
			}
			break;
		case Phase.MOVE:
			GUI.Box (new Rect (10, 40, 90, 20), "Move");
			if (GUI.Button (new Rect (10, 70, 90, 20), "Cancel Move")) {
				unselect ();
			}
			break;
		case Phase.ATTACK:
			GUI.Box (new Rect (10, 40, 90, 20), "Attack");
			if (GUI.Button (new Rect (10, 70, 90, 20), "Skip Attack")) {
				HexPosition.clearSelection ();
				selection = null;
				if (mouse != null) {
					mouse.select ("Cursor");
				}
				selectSelectable ();
				phase = Phase.SELECT;
			}
			break;
		}
	}
}
