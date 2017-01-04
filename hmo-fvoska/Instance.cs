using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace hmofvoska
{
	public class LinkProperties {
		public double Capacity;
		public double Power;
		public double Latency;

		public LinkProperties(double c, double p, double l) {
			Capacity = c;
			Power = p;
			Latency = l;
		}

		public override string ToString()
		{
			return "c=" + Capacity + "; p=" + Power + "; l=" + Latency;
		}
	}

	public class Instance
	{
		private string InstanceFile;
		public int numServers = 0;
		public int numVms = 0;
		public int numNodes = 0;
		public int numRes = 0;
		public int numServiceChains = 0;
		public double[] P_max;
		public double[] P_min;
		public double[] P;
		public double[,] sc;
		public double[,] req;
		public double[,] av;
		public double[,] al;
		public Dictionary<Tuple<int, int>, LinkProperties> Links = new Dictionary<Tuple<int, int>, LinkProperties>();
		public Dictionary<Tuple<int, int>, double> VmDemands = new Dictionary<Tuple<int, int>, double>();
		public double[] lat;

		public Instance (string instanceFile)
		{
			InstanceFile = instanceFile;
			Parse();
		}

		public void Parse() {
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
			Console.WriteLine("numServers = " + numServers);

			// Parse number of components.
			MatchCollection numVmsMatches = Regex.Matches(neatText, @"numVms\s*=\s*(\d*);");
			numVms = Convert.ToInt32(numVmsMatches[0].Groups[1].Value);
			Console.WriteLine("numVms = " + numVms);

			// Parse number of nodes.
			MatchCollection numNodesMatches = Regex.Matches(neatText, @"numNodes\s*=\s*(\d*);");
			numNodes = Convert.ToInt32(numNodesMatches[0].Groups[1].Value);
			Console.WriteLine("numNodes = " + numNodes);

			// Parse number of resource types.
			MatchCollection numResMatches = Regex.Matches(neatText, @"numRes\s*=\s*(\d*);");
			numRes = Convert.ToInt32(numResMatches[0].Groups[1].Value);
			Console.WriteLine ("numRes = " + numRes);

			// Parse number of service chains.
			MatchCollection numServiceChainsMatches = Regex.Matches(neatText, @"numServiceChains\s*=\s*(\d*);");
			numServiceChains = Convert.ToInt32(numServiceChainsMatches[0].Groups[1].Value);
			Console.WriteLine("numServiceChains = " + numServiceChains);

			// Parse minimum and maximum energy consumption for each server.
			P_max = new double[numServers];
			P_min = new double[numServers];

			int counter = 0;
			MatchCollection P_maxMatches = Regex.Matches(neatText, @"P_max\s*=\s*\[(.*)\];");
			foreach (var value in P_maxMatches[0].Groups[1].Value.Split(',')) {
				P_max[counter] = Convert.ToDouble(value.Trim());
				counter++;
			}
			Console.WriteLine("maximum server power consumption = [" + string.Join(", ", P_max) + "]");

			counter = 0;
			MatchCollection P_minMatches = Regex.Matches(neatText, @"P_min\s*=\s*\[(.*)\];");
			foreach (var value in P_minMatches[0].Groups[1].Value.Split(',')) {
				P_min[counter] = Convert.ToDouble(value.Trim());
				counter++;
			}
			Console.WriteLine("minumum server power consumption = [" + string.Join(", ", P_min) + "]");

			// Parse energy consumption of nodes.
			P = new double[numNodes];

			counter = 0;
			MatchCollection PMatches = Regex.Matches(neatText, @"P\s*=\s*\[(.*)\];");
			foreach (var value in PMatches[0].Groups[1].Value.Split(',')) {
				P[counter] = Convert.ToDouble(value.Trim());
				counter++;
			}
			Console.WriteLine("node energy consumption = [" + string.Join(", ", P) + "]");

			// Parse which components are part of which service chain.
			sc = new double[numServiceChains, numVms];

			counter = 0;
			MatchCollection scMatches = Regex.Matches(neatText, @"sc\s*=\s*\[\s*(\[.*\]\s*)*\s*];");
			for (int i = 0; i < scMatches[0].Groups[1].Captures.Count; i++) {
				var serviceChain = scMatches[0].Groups[1].Captures[i].Value.Trim().Replace("[", "").Replace("]", "");
				var split = serviceChain.Split(',');
				for (int j = 0; j < split.Length; j++) {
					sc[i, j] = Convert.ToDouble(split[j]);
				}
				counter++;
			}
			Console.WriteLine("which components are in which service chains = ");
			int scRowLength = sc.GetLength(0);
			int scColLength = sc.GetLength(1);
			for (int i = 0; i < scRowLength; i++)
			{
				Console.Write('\t');
				for (int j = 0; j < scColLength; j++)
				{
					Console.Write(string.Format("{0} ", sc[i, j]));
				}
				Console.Write(Environment.NewLine);
			}

			// Parse resource requirements for every component.
			req = new double[numRes, numVms];

			MatchCollection reqMatches = Regex.Matches(neatText, @"req\s*=\s*\[\s*(\[.*\]\s*)*\s*];");
			for (int i = 0; i < reqMatches[0].Groups[1].Captures.Count; i++) {
				var requirements = reqMatches[0].Groups[1].Captures[i].Value.Trim().Replace("[", "").Replace("]", "");
				var split = requirements.Split(',');
				for (int j = 0; j < split.Length; j++) {
					req[i, j] = Convert.ToDouble(split[j]);
				}
			}
			Console.WriteLine("resource requirements for each component = ");
			int reqRowLength = req.GetLength(0);
			int reqColLength = req.GetLength(1);
			for (int i = 0; i < reqRowLength; i++)
			{
				Console.Write('\t');
				for (int j = 0; j < reqColLength; j++)
				{
					Console.Write(string.Format("{0} ", req[i, j]));
				}
				Console.Write(Environment.NewLine);
			}

			// Parse available resources on every server.
			av = new double[numRes, numServers];

			MatchCollection avMatches = Regex.Matches(neatText, @"av\s*=\s*\[\s*(\[.*\]\s*)*\s*];");
			for (int i = 0; i < avMatches[0].Groups[1].Captures.Count; i++) {
				var availableResources = avMatches[0].Groups[1].Captures[i].Value.Trim().Replace("[", "").Replace("]", "");
				var split = availableResources.Split(',');
				for (int j = 0; j < split.Length; j++) {
					av[i, j] = Convert.ToDouble(split[j]);
				}
			}
			Console.WriteLine("resource availability = ");
			int avRowLength = av.GetLength(0);
			int avColLength = av.GetLength(1);
			for (int i = 0; i < avRowLength; i++)
			{
				Console.Write('\t');
				for (int j = 0; j < avColLength; j++)
				{
					Console.Write(string.Format("{0} ", av[i, j]));
				}
				Console.Write(Environment.NewLine);
			}

			// Parse server locations on nodes.
			al = new double[numServers, numNodes];

			MatchCollection alMatches = Regex.Matches(neatText, @"al\s*=\s*\[\s*(\[.*\]\s*)*\s*];");
			for (int i = 0; i < alMatches[0].Groups[1].Captures.Count; i++) {
				var locations = alMatches[0].Groups[1].Captures[i].Value.Trim().Replace("[", "").Replace("]", "");
				var split = locations.Split(',');
				for (int j = 0; j < split.Length; j++) {
					al[i, j] = Convert.ToDouble(split[j]);
				}
			}
			Console.WriteLine("server locations on nodes = ");
			int alRowLength = al.GetLength(0);
			int alColLength = al.GetLength(1);
			for (int i = 0; i < alRowLength; i++)
			{
				Console.Write('\t');
				for (int j = 0; j < alColLength; j++)
				{
					Console.Write(string.Format("{0} ", al[i, j]));
				}
				Console.Write(Environment.NewLine);
			}
				
			// Parse server locations on nodes.
			MatchCollection linksMatches = Regex.Matches(neatText, @"Edges\s*=\s*{\s*(<.*>,\s*)*};");
			for (int i = 0; i < linksMatches[0].Groups[1].Captures.Count; i++) {
				var links = linksMatches[0].Groups[1].Captures[i].Value.Trim().Replace("<", "").Replace(">", "");
				var split = links.Split(',');
				int n1 = Convert.ToInt32(split[0]);
				int n2 = Convert.ToInt32(split[1]);
				double c = Convert.ToDouble(split[2]);
				double p = Convert.ToDouble(split[3]);
				double l = Convert.ToDouble(split[4]);
				Links.Add(new Tuple<int, int>(n1, n2), new LinkProperties(c, p, l));
			}
			foreach (var k in Links.Keys) {
				Console.WriteLine("link properties <" + k.Item1 + ", " + k.Item2 + "> " + Links[k].ToString());
			}

			// Parse capacity demands for communication between two components.
			MatchCollection VmDemandsMatches = Regex.Matches(neatText, @"VmDemands\s*=\s*{\s*(<.*>,\s*)*};");
			for (int i = 0; i < VmDemandsMatches[0].Groups[1].Captures.Count; i++) {
				var demands = VmDemandsMatches[0].Groups[1].Captures[i].Value.Trim().Replace("<", "").Replace(">", "");
				var split = demands.Split(',');
				int c1 = Convert.ToInt32(split[0]);
				int c2 = Convert.ToInt32(split[1]);
				double throughput = Convert.ToDouble(split[2]);
				VmDemands.Add(new Tuple<int, int>(c1, c2), throughput);
			}
			foreach (var k in VmDemands.Keys) {
				Console.WriteLine("required throughput between components <" + k.Item1 + ", " + k.Item2 + "> " + VmDemands[k].ToString());
			}

			// Parse latency for each service chain.
			lat = new double[numServiceChains];

			counter = 0;
			MatchCollection latMatches = Regex.Matches(neatText, @"lat\s*=\s*\[(.*)\];");
			foreach (var value in latMatches[0].Groups[1].Value.Split(',')) {
				try {
					lat[counter] = Convert.ToDouble(value.Trim());
				}
				catch (Exception ex) {
					Console.WriteLine(ex.Message);
				}
				counter++;
			}
			Console.WriteLine("maximum latency for each service chain = [" + string.Join(", ", lat) + "]");
		}
	}
}

