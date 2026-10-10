using UnityEngine;

namespace MaxyGames.UNode.Nodes.Sample {
	[NodeMenu("Samples/Flow", "If")]
	public class If : IStaticNode {
		[Input(typeof(bool))]
		public static ValuePortDefinition condition;
		[Output(type = typeof(bool))]
		public static FlowPortDefinition state;
		[Output("Next", primary = true)]
		public static FlowPortDefinition exit;

		[Input("", exit = nameof(exit))]
		public static void Execute(bool condition, out bool state) {
			if(condition) {
				state = true;
			}
			else {
				state = false;
			}
		}
	}
}