using System;
using System.Collections.Generic;

namespace hmofvoska
{
	public class State : ICloneable
	{
		// TODO: implement all constraint checks on the solution.
		private Instance Instance;
		private int[] VmsToServerAllocation;
		private Dictionary<Tuple<int, int>, List<int>> Routes = new Dictionary<Tuple<int, int>, List<int>>();

		public State(Instance instance)
		{
			this.Instance = instance;
			VmsToServerAllocation = new int[Instance.numVms];
		}

		public bool PutVmsOnServer(int vms, int srv) {
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

		public double ServerUsageCPU(int srv) {
			double usage = 0;
			for (int vms = 0; vms < Instance.numVms; vms++) {
				if (VmsToServerAllocation[vms] == srv) {
					usage += Instance.RequiredCPU(vms + 1);
				}
			}
			return usage;
		}

		public double ServerUsageRAM(int srv) {
			double usage = 0;
			for (int vms = 0; vms < Instance.numVms; vms++) {
				if (VmsToServerAllocation[vms] == srv) {
					usage += Instance.RequiredRAM(vms + 1);
				}
			}
			return usage;
		}

		public bool SetRoute(int vms1, int vms2, List<int> route) {
			if (vms1 <= 0 || vms1 > Instance.numVms)
				return false;
			if (vms2 <= 0 || vms2 > Instance.numVms)
				return false;
			Routes.Add(new Tuple<int, int>(vms1, vms2), route);
			return true;
		}

		public override string ToString()
		{
			string s = "";
			s += PrintX();
			s += "\n\n";
			s += PrintR();
			return s;
		}

		public string PrintAllocations() {
			return string.Join(",", VmsToServerAllocation);
		}

		public string PrintX() {
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
			string s = "routes={\n";
			List<string> routes = new List<string>();
			foreach (var route in Routes) {
				routes.Add("<" + route.Key.Item1 + "," + route.Key.Item2 + "," + "[" + string.Join(",", route.Value) + "]>");
			}
			s += string.Join(",\n", routes);
			s += "\n};";
			return s;
		}

		public void SaveToFile(string filename) {
			System.IO.File.WriteAllText(filename, this.ToString());
		}

		public bool BServerActive(int srv) {
			for (int i = 0; i < Instance.numVms; i++) {
				if (VmsToServerAllocation[i] == srv)
					return true;
			}
			return false;
		}

		public int ServerActive(int srv) {
			for (int i = 0; i < Instance.numVms; i++) {
				if (VmsToServerAllocation[i] == srv)
					return 1;
			}
			return 0;
		}

		public bool BNodeActive(int node) {
			// Node is also active if there is traffic going through the node.
			foreach(var usedLink in UsedLinks()) {
				if (usedLink.Item1 == node || usedLink.Item2 == node) {
					return true;
				}
			}
			return false;
		}

		public int NodeActive(int node) {
			/* Node IS NOT active if there is no communication with another node, even if there are active servers on that node.
			for (int s = 1; s <= Instance.numServers; s++) {
				if (BServerActive(s) && Instance.ServerOnNode(s, node)) {
					return 1;
				}
			}*/
			// Node is active if there is communication with anohter node.
			foreach(var usedLink in UsedLinks()) {
				if (usedLink.Item1 == node || usedLink.Item2 == node) {
					return 1;
				}
			}
			return 0;
		}

		public bool BComponentOnServer(int component, int server) {
			if (VmsToServerAllocation[component - 1] == server)
				return true;
			return false;
		}

		public int ComponentOnServer(int component, int server) {
			if (VmsToServerAllocation[component - 1] == server)
				return 1;
			return 0;
		}

		public List<Tuple<int, int>> UsedLinks() {
			List<Tuple<int, int>> usedLinks = new List<Tuple<int, int>>();
			foreach (var route in Routes.Values) {
				if (route.Count == 1) {
					// Components are on same node, they do not use links.
					continue;
				}
				for (var i = 0; i < route.Count - 1; i++) {
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
					Console.WriteLine("Server {0} active ({1}/{2}={3})", s, srvCpuUsage, Instance.AvailableCPU(s), Instance.PwrSrvMin(s) + powerDiff * (srvCpuUsage / Instance.AvailableCPU(s)));
					if (srvCpuUsage > Instance.AvailableCPU(s)) {
						// Components use more CPU than the server has available.
						// Invalid solution.
						Console.WriteLine("Server {0} does not have enough CPU ({1}/{2})", s, srvCpuUsage, Instance.AvailableCPU(s));
						return double.NaN;
					}

					serverCosts += powerDiff * (srvCpuUsage / Instance.AvailableCPU(s));
				}
			}
			for (int n = 1; n <= Instance.numNodes; n++) {
				if (BNodeActive(n)) {
					Console.WriteLine("Node {0} active ({1})", n, Instance.PwrNode(n));
				}
				nodeCosts += NodeActive(n) * Instance.PwrNode(n);
			}
			var usedLinks = UsedLinks();
			foreach (var link in Instance.Links) {
				if (usedLinks.Contains(link.Key)) {
					Console.WriteLine("Link <{0},{1}> used ({2})", link.Key.Item1, link.Key.Item2, link.Value.Power);
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

