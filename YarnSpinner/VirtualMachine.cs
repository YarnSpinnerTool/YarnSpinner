using System;

namespace Yarn
{
	public class VirtualMachine
	{
		public VirtualMachine (Program p)
		{
			program = p;
			state = new State ();
		}

		void Reset() {
			state = new State();
		}

		private Program program;
		private State state;

		public void RunNext() {

			var currentNode = program.nodes[state.currentNode];

			var currentInstruction = currentNode.instructions [state.programCounter];

		}
	}
}

