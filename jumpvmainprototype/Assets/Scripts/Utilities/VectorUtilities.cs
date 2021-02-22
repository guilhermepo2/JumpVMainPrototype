using UnityEngine;
public static class MathUtilities {
	public static int ManhattanDistance(Vector2 a, Vector2 b) {
		return (int)((Mathf.Abs((a.x - b.x)) + Mathf.Abs((a.y - b.y))));
	}

	public static float RoundToNearestMidpoint(float value) {
		return (Mathf.Round(2 * value) / 2f);
	}
}