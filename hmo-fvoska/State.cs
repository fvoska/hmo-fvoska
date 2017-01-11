using System;
using System.Collections.Generic;

namespace hmofvoska
{
	public class State : ICloneable
	{
		// TODO: implement all constraint checks on the solution.
		private Instance Instance;
		private int[] VmsToServerAllocation;
		private Dictionary<Tuple<int, int>, double> LinkUsage = new Dictionary<Tuple<int, int>, double>();
		public Dictionary<Tuple<int, int>, bool> IgnoreLinks { get; private set; } = new Dictionary<Tuple<int, int>, bool>();
		private Dictionary<Tuple<int, int>, List<int>> Routes = new Dictionary<Tuple<int, int>, List<int>>();

		public State(Instance instance)
		{
			Instance = instance;
			VmsToServerAllocation = new int[Instance.numVms];
		}

		public bool PutVmsOnServer(int vms, int srv) {
			// Puts specified component on specified server.
			if (vms <= 0 || vms > Instance.numVms)
				return false;
			if (srv <= 0 || srv > Instance.numServers)
				return false;
			if (Instance.RequiredCPU(vms) + ServerUsageCPU(srv) > Instance.AvailableCPU(srv)) {
				// Putting Vms on this server would overload its CPU.
				return false;
			}
			if (Instance.RequiredRAM(vms) + ServerUsageRAM(srv) > Instance.AvailableRAM(srv)) {
				// Putting Vms on this server would overload its RAM.
				return false;
			}
			VmsToServerAllocation[vms - 1] = srv;
			return true;
		}

		public int ComponentLocationServer(int component) {
			// Returns server on which the component is placed.
			return VmsToServerAllocation[component - 1];
		}

		public int ComponentLocationNode(int component) {
			// Returns node on which the component is placed.
			int server = ComponentLocationServer(component);
			return Instance.ServerOnNode(server);
		}

		public double ServerUsageCPU(int srv) {
			// Checks CPU usage of specified server.
			double usage = 0;
			for (int vms = 0; vms < Instance.numVms; vms++) {
				if (VmsToServerAllocation[vms] == srv) {
					usage += Instance.RequiredCPU(vms + 1);
				}
			}
			return usage;
		}

		public double ServerUsageRAM(int srv) {
			// Checks RAM usage of specified server.
			double usage = 0;
			for (int vms = 0; vms < Instance.numVms; vms++) {
				if (VmsToServerAllocation[vms] == srv) {
					usage += Instance.RequiredRAM(vms + 1);
				}
			}
			return usage;
		}

		public bool SetRoute(int vms1, int vms2, List<int> route) {
			// Sets up route between two components.
			if (vms1 <= 0 || vms1 > Instance.numVms)
				return false;
			if (vms2 <= 0 || vms2 > Instance.numVms)
				return false;
			if (route.Count > 1) {
				// If components are not on same server, we have to check 
			}
			var vmsPair = new Tuple<int, int>(vms1, vms2);
			bool wouldOverload = false;
			var routeLinks = new List<Tuple<int, int>>();
			for (int i = 0; i < route.Count - 1; i++) {
				int node1 = route[i];
				int node2 = route[i + 1];
				var link = new Tuple<int, int>(node1, node2);
				routeLinks.Add(link);

				// Check if link would be over capacity.
				if (LinkUsage.ContainsKey(link)) {
					if (LinkUsage[link] + Instance.VmDemands[vmsPair] > Instance.Links[link].Capacity) {
						// Adding this path would overload the link.
						wouldOverload = true;
						if (!IgnoreLinks.ContainsKey(link)) IgnoreLinks.Add(link, true);
						break;
					}
				}
			}
			if (!wouldOverload) {
				foreach (var link in routeLinks) {
					// Increase link usage for all links that route uses.
					if (!LinkUsage.ContainsKey(link)) {
						LinkUsage.Add(link, Instance.VmDemands[vmsPair]);
					} else {
						LinkUsage[link] += Instance.VmDemands[vmsPair];
					}
				}
				// Add route.
				Routes.Add(new Tuple<int, int>(vms1, vms2), route);
				return true;
			} else {
				return false;	
			}
		}

		public string PrintLinkUsage() {
			string s = "";
			foreach (var link in LinkUsage) {
				s += string.Format("Link <{0},{1}> used {2}/{3}\n", link.Key.Item1, link.Key.Item2, link.Value, Instance.Links[link.Key].Capacity);
			}
			return s;
		}

		public override string ToString()
		{
			// Prints solution.
			string s = "";
			s += PrintX();
			s += "\n\n";
			s += PrintR();
			return s;
		}

		public string PrintAllocations() {
			// Prints components' allocations to servers (vector).
			return string.Join(",", VmsToServerAllocation);
		}

		public string PrintX() {
			// Prints components' allocations to servers (matrix).
			string s = "x=[\n";
			for (int i = 0; i < Instance.numVms; i++) {
				s += "[";
				string[] componentLocation = new string[Instance.numServers];
				for (int j = 0; j < Instance.numServers; j++) {
					if (VmsToServerAllocation[i] == j + 1) {
						componentLocation[j] = "1";
					} else {
						componentLocation[j] = "0";
					}
				}
				s += string.Join(",", componentLocation);
				s += "]\n";
			}
			s += "];";
			return s;
		}

		public string PrintR() {
			// Prints routes.
			string s = "routes={\n";
			var routes = new List<string>();
			foreach (var route in Routes) {
				routes.Add("<" + route.Key.Item1 + "," + route.Key.Item2 + "," + "[" + string.Join(",", route.Value) + "]>");
			}
			s += string.Join(",\n", routes);
			s += "\n};";
			return s;
		}

		public void SaveToFile(string filename) {
			// Saves solution to a file.
			System.IO.File.WriteAllText(filename, this.ToString());
		}

		public bool BServerActive(int srv) {
			// Checks if specified server is active.
			for (int i = 0; i < Instance.numVms; i++) {
				if (VmsToServerAllocation[i] == srv)
					return true;
			}
			return false;
		}

		public int ServerActive(int srv) {
			// Checks if specified server is active.
			for (int i = 0; i < Instance.numVms; i++) {
				if (VmsToServerAllocation[i] == srv)
					return 1;
			}
			return 0;
		}

		public bool BNodeActive(int node) {
			// Node is active if there is communication with anohter node.
			foreach(var usedLink in UsedLinks()) {
				if (usedLink.Item1 == node || usedLink.Item2 == node) {
					return true;
				}
			}
			return false;
		}

		public int NodeActive(int node) {
			// Node is active if there is communication with anohter node.
			foreach(var usedLink in UsedLinks()) {
				if (usedLink.Item1 == node || usedLink.Item2 == node) {
					return 1;
				}
			}
			return 0;
		}

		public bool BComponentOnServer(int component, int server) {
			// Checks if specified component is on specified server.
			if (VmsToServerAllocation[component - 1] == server)
				return true;
			return false;
		}

		public int ComponentOnServer(int component, int server) {
			// Checks if specified component is on specified server.
			if (VmsToServerAllocation[component - 1] == server)
				return 1;
			return 0;
		}

		public List<Tuple<int, int>> UsedLinks() {
			// Based on routes, checks which links are used.
			var usedLinks = new List<Tuple<int, int>>();
			foreach (var route in Routes.Values) {
				if (route.Count == 1) {
					// Components are on same node, they do not use links.
					continue;
				}
				for (var i = 0; i < route.Count - 1; i++) {
					// Check all links along the route and add to used links.
					usedLinks.Add(new Tuple<int, int>(route[i], route[i+1]));
				}
			}
			return usedLinks;
		}

		public double CalculateFitness() {
			double serverCosts = 0;
			double nodeCosts = 0;
			double linkCosts = 0;
			for (int s = 1; s <= Instance.numServers; s++) {
				if (BServerActive(s)) {
					// If server is active, it consumes minimum power.
					serverCosts += ServerActive(s) * Instance.PwrSrvMin(s);

					// Consumption based on CPU utilization.
					double powerDiff = Instance.PwrSrvMax(s) - Instance.PwrSrvMin(s);

					// Server CPU utilization.
					double srvCpuUsage = 0;
					for (int c = 1; c <= Instance.numVms; c++) {
						// Sum all component's requirements for components that are on this server.
						srvCpuUsage += ComponentOnServer(c, s) * Instance.RequiredCPU(c);
					}
					// Console.WriteLine("Server {0} active ({1}/{2}={3})", s, srvCpuUsage, Instance.AvailableCPU(s), Instance.PwrSrvMin(s) + powerDiff * (srvCpuUsage / Instance.AvailableCPU(s)));
					if (srvCpuUsage > Instance.AvailableCPU(s)) {
						// Components use more CPU than the server has available.
						// Invalid solution.
						// Console.WriteLine("Server {0} does not have enough CPU ({1}/{2})", s, srvCpuUsage, Instance.AvailableCPU(s));
						return double.NaN;
					}

					serverCosts += powerDiff * (srvCpuUsage / Instance.AvailableCPU(s));
				}
			}
			for (int n = 1; n <= Instance.numNodes; n++) {
				if (BNodeActive(n)) {
					// Console.WriteLine("Node {0} active ({1})", n, Instance.PwrNode(n));
				}
				nodeCosts += NodeActive(n) * Instance.PwrNode(n);
			}
			var usedLinks = UsedLinks();
			foreach (var link in Instance.Links) {
				if (usedLinks.Contains(link.Key)) {
					// Console.WriteLine("Link <{0},{1}> used ({2})", link.Key.Item1, link.Key.Item2, link.Value.Power);
					linkCosts += link.Value.Power;
				}
			}
			return serverCosts + nodeCosts + linkCosts;
		}

		public object Clone()
		{
			return this.MemberwiseClone();
		}
	}
}

