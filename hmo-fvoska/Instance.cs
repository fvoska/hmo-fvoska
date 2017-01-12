using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace hmofvoska {
	public class LinkProperties {
		public double Capacity;
		public double Power;
		public double Latency;

		public LinkProperties(double c, double p, double l) {
			Capacity = c;
			Power = p;
			Latency = l;
		}

		public override string ToString() {
			return "c=" + Capacity + "; p=" + Power + "; l=" + Latency;
		}
	}

	public class SCProperties {
		public int ID = 0;
		public double MaxLatency;
		public List<int> Components = new List<int>();

		public SCProperties(int id, double maxLatency, List<int> components) {
			ID = id;
			MaxLatency = maxLatency;
			Components = components;
		}
	}

	public class Instance {
		private string InstanceFile;
		public int numServers { get; private set; }
		public int numVms { get; private set; }
		public int numNodes { get; private set; }
		public int numRes { get; private set; }
		public int numServiceChains { get; private set; }
		private double[] p_max;
		public double PwrSrvMax(int srv) {
			return p_max[srv - 1];
		}
		private double[] p_min;
		public double PwrSrvMin(int srv) {
			return p_min[srv - 1];
		}
		private double[] p;
		public double PwrNode(int node) {
			return p[node - 1];
		}
		private int[,] sc;
		public Dictionary<int, SCProperties> ServiceChains() {
			var serviceChains = new Dictionary<int, SCProperties>();
			for (int chain = 0; chain < numServiceChains; chain++) {
				var chainComponents = new List<int>();
				for (int component = 0; component < numVms; component++) {
					if (sc[chain, component] == 1) {
						chainComponents.Add(component + 1);
					}
				}
				var scp = new SCProperties(chain + 1, lat[chain], chainComponents);
				serviceChains.Add(chain + 1, scp);
			}
			return serviceChains;
		}
		private double[,] req;
		public double RequiredCPU(int component) {
			return req[0, component - 1];
		}
		public double RequiredRAM(int component) {
			return req[1, component - 1];
		}
		private double[,] av;
		public double AvailableCPU(int srv) {
			return av[0, srv - 1];
		}
		public double AvailableRAM(int srv) {
			return av[1, srv - 1];
		}
		private int[,] al;
		public int ServerOnNode(int srv) {
			for (int i = 0; i < numNodes; i++) {
				if (al[srv - 1, i] == 1) {
					return i + 1;
				}
			}
			return -1;
		}
		public bool ServerOnNode(int srv, int node) {
			if (al[srv - 1, node - 1] == 1) {
				return true;
			}
			return false;
		}
		public Dictionary<Tuple<int, int>, LinkProperties> Links { get; private set; }
		public Dictionary<Tuple<int, int>, double> VmDemands { get; private set; }
		public double[] lat { get; private set; }

		public Instance(string instanceFile) {
			Links = new Dictionary<Tuple<int, int>, LinkProperties>();
			VmDemands = new Dictionary<Tuple<int, int>, double>();
			InstanceFile = instanceFile;
			Parse();
		}

		public void Parse() {
			var provider = new NumberFormatInfo();
			provider.NumberDecimalSeparator = ".";
			provider.NumberGroupSeparator = ",";

			// Read instance file.
			var text = File.ReadAllText(InstanceFile);

			// Remove comments from file.
			var blockComments = @"/\*(.*?)\*/";
			string noComments = Regex.Replace(text, blockComments,
				me => {
					if (me.Value.StartsWith("/*") || me.Value.StartsWith("//"))
						return me.Value.StartsWith("//") ? Environment.NewLine : "";
					// Keep the literal strings
					return me.Value;
				},
				RegexOptions.Singleline);

			// Remove empty lines.
			string neatText = Regex.Replace(noComments, @"^\s+$[\r\n]*", "", RegexOptions.Multiline);

			// Parse number of servers.
			MatchCollection numServersMatches = Regex.Matches(neatText, @"numServers\s*=\s*(\d*);");
			numServers = Convert.ToInt32(numServersMatches[0].Groups[1].Value);

			// Parse number of components.
			MatchCollection numVmsMatches = Regex.Matches(neatText, @"numVms\s*=\s*(\d*);");
			numVms = Convert.ToInt32(numVmsMatches[0].Groups[1].Value);

			// Parse number of nodes.
			MatchCollection numNodesMatches = Regex.Matches(neatText, @"numNodes\s*=\s*(\d*);");
			numNodes = Convert.ToInt32(numNodesMatches[0].Groups[1].Value);

			// Parse number of resource types.
			MatchCollection numResMatches = Regex.Matches(neatText, @"numRes\s*=\s*(\d*);");
			numRes = Convert.ToInt32(numResMatches[0].Groups[1].Value);

			// Parse number of service chains.
			MatchCollection numServiceChainsMatches = Regex.Matches(neatText, @"numServiceChains\s*=\s*(\d*);");
			numServiceChains = Convert.ToInt32(numServiceChainsMatches[0].Groups[1].Value);

			// Parse minimum and maximum energy consumption for each server.
			p_max = new double[numServers];
			p_min = new double[numServers];

			int counter = 0;
			MatchCollection p_maxMatches = Regex.Matches(neatText, @"P_max\s*=\s*\[(.*)\];");
			foreach (var value in p_maxMatches[0].Groups[1].Value.Split(',')) {
				p_max[counter] = Convert.ToDouble(value.Trim(), provider);
				counter++;
			}

			counter = 0;
			MatchCollection p_minMatches = Regex.Matches(neatText, @"P_min\s*=\s*\[(.*)\];");
			foreach (var value in p_minMatches[0].Groups[1].Value.Split(',')) {
				p_min[counter] = Convert.ToDouble(value.Trim(), provider);
				counter++;
			}

			// Parse energy consumption of nodes.
			p = new double[numNodes];

			counter = 0;
			MatchCollection PMatches = Regex.Matches(neatText, @"P\s*=\s*\[(.*)\];");
			foreach (var value in PMatches[0].Groups[1].Value.Split(',')) {
				p[counter] = Convert.ToDouble(value.Trim(), provider);
				counter++;
			}

			// Parse which components are part of which service chain.
			sc = new int[numServiceChains, numVms];

			counter = 0;
			MatchCollection scMatches = Regex.Matches(neatText, @"sc\s*=\s*\[\s*(\[.*\]\s*)*\s*];");
			for (int i = 0; i < scMatches[0].Groups[1].Captures.Count; i++) {
				var serviceChain = scMatches[0].Groups[1].Captures[i].Value.Trim().Replace("[", "").Replace("]", "");
				var split = serviceChain.Split(',');
				for (int j = 0; j < split.Length; j++) {
					sc[i, j] = Convert.ToInt32(split[j]);
				}
				counter++;
			}

			// Parse resource requirements for every component.
			req = new double[numRes, numVms];

			MatchCollection reqMatches = Regex.Matches(neatText, @"req\s*=\s*\[\s*(\[.*\]\s*)*\s*];");
			for (int i = 0; i < reqMatches[0].Groups[1].Captures.Count; i++) {
				var requirements = reqMatches[0].Groups[1].Captures[i].Value.Trim().Replace("[", "").Replace("]", "");
				var split = requirements.Split(',');
				for (int j = 0; j < split.Length; j++) {
					req[i, j] = Convert.ToDouble(split[j], provider);
				}
			}

			// Parse available resources on every server.
			av = new double[numRes, numServers];

			MatchCollection avMatches = Regex.Matches(neatText, @"av\s*=\s*\[\s*(\[.*\]\s*)*\s*];");
			for (int i = 0; i < avMatches[0].Groups[1].Captures.Count; i++) {
				var availableResources = avMatches[0].Groups[1].Captures[i].Value.Trim().Replace("[", "").Replace("]", "");
				var split = availableResources.Split(',');
				for (int j = 0; j < split.Length; j++) {
					av[i, j] = Convert.ToDouble(split[j], provider);
				}
			}

			// Parse server locations on nodes.
			al = new int[numServers, numNodes];

			MatchCollection alMatches = Regex.Matches(neatText, @"al\s*=\s*\[\s*(\[.*\]\s*)*\s*];");
			for (int i = 0; i < alMatches[0].Groups[1].Captures.Count; i++) {
				var locations = alMatches[0].Groups[1].Captures[i].Value.Trim().Replace("[", "").Replace("]", "");
				var split = locations.Split(',');
				for (int j = 0; j < split.Length; j++) {
					al[i, j] = Convert.ToInt32(split[j]);
				}
			}

			// Parse server locations on nodes.
			MatchCollection linksMatches = Regex.Matches(neatText, @"Edges\s*=\s*{\s*(<.*>,*\s*)*};");
			for (int i = 0; i < linksMatches[0].Groups[1].Captures.Count; i++) {
				var links = linksMatches[0].Groups[1].Captures[i].Value.Trim().Replace("<", "").Replace(">", "");
				var split = links.Split(',');
				int n1 = Convert.ToInt32(split[0]);
				int n2 = Convert.ToInt32(split[1]);
				double c = Convert.ToDouble(split[2], provider);
				double p = Convert.ToDouble(split[3], provider);
				double l = Convert.ToDouble(split[4], provider);
				Links.Add(new Tuple<int, int>(n1, n2), new LinkProperties(c, p, l));
			}

			// Parse capacity demands for communication between two components.
			MatchCollection VmDemandsMatches = Regex.Matches(neatText, @"VmDemands\s*=\s*{\s*(<.*>,*\s*)*};");
			for (int i = 0; i < VmDemandsMatches[0].Groups[1].Captures.Count; i++) {
				var demands = VmDemandsMatches[0].Groups[1].Captures[i].Value.Trim().Replace("<", "").Replace(">", "");
				var split = demands.Split(',');
				int c1 = Convert.ToInt32(split[0]);
				int c2 = Convert.ToInt32(split[1]);
				double throughput = Convert.ToDouble(split[2], provider);
				VmDemands.Add(new Tuple<int, int>(c1, c2), throughput);
			}

			// Parse latency for each service chain.
			lat = new double[numServiceChains];

			counter = 0;
			MatchCollection latMatches = Regex.Matches(neatText, @"lat\s*=\s*\[(.*)\];");
			foreach (var value in latMatches[0].Groups[1].Value.Split(',')) {
				try {
					lat[counter] = Convert.ToDouble(value.Trim(), provider);
				} catch (Exception ex) {
					Console.WriteLine(ex.Message);
				}
				counter++;
			}
		}

		public List<Tuple<int, double>> OrderServersByEfficency(bool order = true) {
			// Returns list of servers ordered by efficency (Available CPU / Max power usage).
			var list = new List<Tuple<int, double>>();
			for (int s = 1; s <= numServers; s++) {
				double efficency = AvailableCPU(s) / PwrSrvMax(s);
				list.Add(new Tuple<int, double>(s, efficency));
			}
			if (order) list.Sort((el1, el2) => el2.Item2.CompareTo(el1.Item2));
			return list;
		}

		public bool ComponentInChain(int comp) {
			// Checks if component is part of at least one service chain.
			for (int chain = 0; chain < numServiceChains; chain++) {
				if (sc[chain, comp - 1] == 1)
					return true;
			}
			return false;
		}

		public List<int> ComponentsToPlace() {
			// Returns all components that are part of at least one service chain.
			var componentsToPlace = new List<int>();
			for (int comp = 1; comp <= numVms; comp++) {
				if (ComponentInChain(comp))
					componentsToPlace.Add(comp);
			}
			return componentsToPlace;
		}

		public List<int> ComponentsToIgnore() {
			// Returns all components which are not part of any service chain.
			var componentsToIgnore = new List<int>();
			for (int comp = 1; comp <= numVms; comp++) {
				if (!ComponentInChain(comp))
					componentsToIgnore.Add(comp);
			}
			return componentsToIgnore;
		}

		public IOrderedEnumerable<KeyValuePair<Tuple<int, int>, int>> AllNeededRoutes() {
			// Returns all pairs of components which need to communicate directly.
			// Key == components that communicate.
			// Value == in how many service chains they have to communicate.
			// Sorted by number of service chains in which the components communicate, descending.
			var routes = new Dictionary<Tuple<int, int>, int>();
			foreach (var chain in ServiceChains()) {
				if (chain.Value.Components.Count == 1) {
					// Only one item in service chain, no need for communication.
					continue;
				}
				for (var componentIndex = 0; componentIndex < chain.Value.Components.Count - 1; componentIndex++) {
					var t = new Tuple<int, int>(chain.Value.Components[componentIndex], chain.Value.Components[componentIndex + 1]);
					if (!routes.ContainsKey(t)) {
						// First occurrence.
						routes.Add(t, 1);
					} else {
						// Incement number of chains in which components communicate.
						routes[t] += 1;
					}
				}
			}
			return from entry in routes orderby entry.Value descending select entry;
		}

		public string PrintNeededRoutes() {
			string s = "";
			foreach (var t in AllNeededRoutes()) {
				s += "Components " + t.Key.Item1 + " and " + t.Key.Item2 + " need communication (in " + t.Value + " chains)\n";
			}
			return s;
		}

		public override string ToString() {
			string s = "";
			s += "numServers = " + numServers + '\n';
			s += "numVms = " + numVms + '\n';
			s += "numNodes = " + numNodes + '\n';
			s += "numRes = " + numRes + '\n';
			s += "numServiceChains = " + numServiceChains + '\n';
			s += "maximum server power consumption = [" + string.Join(", ", p_max) + "]\n";
			s += "minumum server power consumption = [" + string.Join(", ", p_min) + "]\n";
			s += "node energy consumption = [" + string.Join(", ", p) + "]\n";
			s += "which components are in which service chains = \n";
			int scRowLength = sc.GetLength(0);
			int scColLength = sc.GetLength(1);
			for (int i = 0; i < scRowLength; i++) {
				s += '\t';
				for (int j = 0; j < scColLength; j++) {
					s += sc[i, j] + " ";
				}
				s += '\n';
			}
			s += "resource requirements for each component = \n";
			int reqRowLength = req.GetLength(0);
			int reqColLength = req.GetLength(1);
			for (int i = 0; i < reqRowLength; i++) {
				s += '\t';
				for (int j = 0; j < reqColLength; j++) {
					s += req[i, j] + " ";
				}
				s += '\n';
			}
			s += "resource availability = \n";
			int avRowLength = av.GetLength(0);
			int avColLength = av.GetLength(1);
			for (int i = 0; i < avRowLength; i++) {
				s += '\t';
				for (int j = 0; j < avColLength; j++) {
					s += av[i, j] + " ";
				}
				s += '\n';
			}
			s += "server locations on nodes = \n";
			int alRowLength = al.GetLength(0);
			int alColLength = al.GetLength(1);
			for (int i = 0; i < alRowLength; i++) {
				s += '\t';
				for (int j = 0; j < alColLength; j++) {
					s += al[i, j] + " ";
				}
				s += '\n';
			}
			foreach (var k in Links.Keys) {
				s += "link properties <" + k.Item1 + ", " + k.Item2 + "> " + Links[k].ToString() + '\n';
			}
			foreach (var k in VmDemands.Keys) {
				s += "required throughput between components <" + k.Item1 + ", " + k.Item2 + "> " + VmDemands[k].ToString() + '\n';
			}
			s += "maximum latency for each service chain = [" + string.Join(", ", lat) + "]\n";
			return s;
		}

		public string PrintServiceChains() {
			string s = "";
			var scs = ServiceChains();
			foreach (var chain in scs) {
				s += "SC #" + chain.Key + " components: <";
				s += string.Join(",", chain.Value.Components);
				s += "> maxLat: " + chain.Value.MaxLatency + "\n";
			}
			return s;
		}
	}
}