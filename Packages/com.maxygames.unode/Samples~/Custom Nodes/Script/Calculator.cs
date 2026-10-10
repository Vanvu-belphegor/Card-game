using UnityEngine;

namespace MaxyGames.UNode.Nodes.Sample {
	[NodeMenu("Samples/Value", "Calculator")]
	public class Calculator : IStaticNode {
		[Input(typeof(float))]
		public static ValuePortDefinition first;
		[Input(typeof(float))]
		public static ValuePortDefinition second;

		[Output("Add")]
		public static float Add(float first, float second) {
			return first + second;
		}

		[Output("Divide")]
		public static float Div(float first, float second) {
			return first / second;
		}

		[Output("Multiple")]
		public static float Mul(float first, float second) {
			return first * second;
		}

		[Output("Substract")]
		public static float Sub(float first, float second) {
			return first - second;
		}
	}
}