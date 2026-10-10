using UnityEngine;

namespace MaxyGames.UNode.Nodes.Sample {
	[NodeMenu("Samples/Value", "Per Second Unscaled")]
	[Description("Get the timeScale-independent interval in seconds from the last frame to the current one")]
	public class GetPerSecondUnscaled : IStaticNode {
		[Input(type = typeof(float))]
		public static ValuePortDefinition input;

		[Output]
		public static float Execute(float input) {
			return Time.unscaledDeltaTime * input;
		}
	}
}