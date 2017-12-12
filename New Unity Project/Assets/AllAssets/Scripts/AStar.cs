using System.Collections;
using System.Collections.Generic;

public class AStar {
	
	private static HexPosition getMin (HashSet<HexPosition> set, Dictionary<HexPosition, int> function) {
		int min = int.MaxValue;
		HexPosition minHex = null;
		foreach (HexPosition hex in set) {
			int value;
			if (function.TryGetValue (hex, out value)) {
				if (value <= min) {
					min = value;
					minHex = hex;
				}
			}
		}
		return minHex;
	}

	private static HexPosition getMin (Dictionary<HexPosition, int> function) {
		int min = int.MaxValue;
		HexPosition minHex = null;
		foreach (KeyValuePair<HexPosition, int> kvp in function) {
			if (kvp.Value <= min) {
				min = kvp.Value;
				minHex = kvp.Key;
			}
		}
		return minHex;
	}

	private static HexPosition[] reconstructPath (Dictionary<HexPosition,HexPosition> cameFrom, HexPosition final, int size) {
		HexPosition[] path = new HexPosition[size];
		path [size - 1] = final;
		for (int i = size - 2; i >= 0; --i) {
			cameFrom.TryGetValue (path [i + 1], out path [i]);
		}
		return path;
	}

	/// <summary>
	/// Start from start, move to within distance of goal within max steps.
	/// </summary>
	/// <param name="start">Start.</param>
	/// <param name="goal">Goal.</param>
	/// <param name="max">Max.</param>
	/// <param name="distance">Distance.</param>
	public static HexPosition[] search (HexPosition start, HexPosition goal, int max, int distance = 0) {
		max += distance; //Now it's the maximum distance to the goal, instead of just the maximum number of steps.
		//HashSet<HexPosition> closedSet = new HashSet<HexPosition>();	// The set of nodes already evaluated.
		//HashSet<HexPosition> openSet = new HashSet<HexPosition>(start);	// The set of tentative nodes to be evaluated, initially containing the start node
		Dictionary<HexPosition, HexPosition> cameFrom = new Dictionary<HexPosition, HexPosition> ();	// The map of navigated nodes.
		Dictionary<HexPosition, int> gScore = new Dictionary<HexPosition, int> ();	// Cost from start along best known path. Domain is the open and closed sets.
		Dictionary<HexPosition, int> fScore = new Dictionary<HexPosition, int> ();	// Estimated total cost from start to goal through y. Domain is the open set.
		gScore.Add (start, 0);
		fScore.Add (start, start.dist (goal));
		while (fScore.Count > 0) {
			HexPosition current = getMin (fScore);
			if (current.dist (goal) <= distance) {
				int length = 0;
				gScore.TryGetValue (current, out length);
				return reconstructPath (cameFrom, current, length + 1);
			}
			fScore.Remove (current);
			foreach (HexPosition neighbor in current.Neighbors) {
				if (neighbor.containsKey ("Obstacle") || neighbor.containsKey ("Unit")) {
					continue;	//Make this more general.
				}
				if (gScore.ContainsKey (neighbor) && !fScore.ContainsKey (neighbor)) {
					continue;
				}
				int tentativeGScore = 0;
				gScore.TryGetValue (current, out tentativeGScore);
				++tentativeGScore;
				if (tentativeGScore > max) {
					continue;
				}
				int neighborGScore = 0;
				gScore.TryGetValue (current, out neighborGScore);
				if (!fScore.ContainsKey (neighbor) || tentativeGScore < neighborGScore) {
					int newFScore = tentativeGScore + neighbor.dist (goal);
					if (newFScore > max) {
						continue;
					}
					cameFrom.Add (neighbor, current);
					gScore.Add (neighbor, tentativeGScore);
					fScore.Add (neighbor, newFScore);
				}
			}
		}
		return new HexPosition[0] { };
	}
}
