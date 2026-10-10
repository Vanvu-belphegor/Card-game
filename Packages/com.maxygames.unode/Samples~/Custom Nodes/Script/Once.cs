using UnityEngine;

namespace MaxyGames.UNode.Nodes.Sample {
	[NodeMenu("Samples/Flow", "Once")]
	public class Once : IInstanceNode {
		[Output(type = typeof(Kind))]
		public FlowPortDefinition kind;

		private bool hasEnter = false;

		public enum Kind {
			[PortDescription(description = "The flow to execute only once at first time node get executed")]
			Once,
			[PortDescription(description = "The flow to execute after once the node is executed twice or more")]
			After
		}

		[Input("In", description = "The flow to execute the node")]
		public void Execute(out Kind kind) {
			if(!hasEnter) {
				hasEnter = true;
				kind = Kind.Once;
			}
			else {
				kind = Kind.After;
			}
		}

		[Input(description = "Reset the once state")]
		public void Reset() {
			hasEnter = false;
		}
	}
}