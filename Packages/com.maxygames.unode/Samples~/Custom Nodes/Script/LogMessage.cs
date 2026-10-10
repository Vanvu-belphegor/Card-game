using UnityEngine;

namespace MaxyGames.UNode.Nodes.Sample {
	[NodeMenu("Samples/Flow", "Log Message")]
	public class LogMessage : IStaticNode {
		[Input(typeof(string), description = "The message to log")]
		public static ValuePortDefinition message;
		[Output(primary = true)]
		public static FlowPortDefinition exit;

		[Input("", exit = nameof(exit))]
		public static void Execute(string message) {
			Debug.Log(message);
		}
	}
}