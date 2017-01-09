using System;

namespace hmofvoska
{
	public class InitialVms
	{
		private Instance Instance;
		private State State;

		public InitialVms(Instance instance)
		{
			Instance = instance;
			State = new State(Instance);
		}

		public State InitialPlacement() {
			// Fill most efficient servers first.
			var servers = Instance.OrderServersByEfficency();
			int serverIndex = 0;
			foreach(var component in Instance.ComponentsToPlace()) {
				bool canPutVmsOnServer = true;
				do {
					canPutVmsOnServer = State.PutVmsOnServer(component, servers[serverIndex].Item1);
					if (!canPutVmsOnServer) {
						// If Vms placement on server fails, it means that server's CPU and RAM are used up.
						if (serverIndex + 1 <= Instance.numServers) {
							// Go to next server.
							serverIndex += 1;
						}
					}
					// Loop repeats until we find a server which has enough CPU and RAM for this Vms.
				} while(!canPutVmsOnServer);
			}
			return State;
		}
	}
}