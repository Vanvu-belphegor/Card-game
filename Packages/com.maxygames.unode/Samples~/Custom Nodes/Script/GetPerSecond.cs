using UnityEngine;

namespace MaxyGames.UNode.Nodes.Sample {
	[NodeMenu("Samples/Value", "Per Second")]
	public class GetPerSecond : IStaticNode {
		[Input(type = typeof(float))]
		public static ValuePortDefinition input;

		[Output("Out")]
		public static float GetValue(float input) {
			return Time.deltaTime * input;
		}
	}
}