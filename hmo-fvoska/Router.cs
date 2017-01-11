using System;
using System.Collections.Generic;
using QuickGraph;
using QuickGraph.Algorithms;

namespace hmofvoska
{
	public class Router
	{
		private Instance Instance;
		private State State;

		private AdjacencyGraph<int, Edge<int>> Graph;
		private Dictionary<Edge<int>, double> Costs;

		public void SetUpEdgesAndCosts() {
			// TODO: check edge capacity.
			Graph = new AdjacencyGraph<int, Edge<int>>();
			Costs = new Dictionary<Edge<int>, double>();

			foreach (var link in Instance.Links) {
				// Don't add links that are at full capacity to the graph.
				if (State.IgnoreLinks.ContainsKey(link.Key) && State.IgnoreLinks[link.Key] == true) {
					// Console.WriteLine("Ignoring link <{0},{1}>", link.Key.Item1, link.Key.Item2);
				} else {
					AddEdgeWithCosts(link.Key.Item1, link.Key.Item2, link.Value.Latency);
				}
			}
		}

		private void AddEdgeWithCosts(int source, int target, double cost) {
			var edge = new Edge<int>(source, target);
			Graph.AddVerticesAndEdge(edge);
			Costs.Add(edge, cost);
		}

		private IEnumerable<Edge<int>> ShortestPath(int from, int to) {
			var edgeCost = AlgorithmExtensions.GetIndexer(Costs);
			var tryGetPath = Graph.ShortestPathsDijkstra(edgeCost, from);

			IEnumerable<Edge<int>> path;
			if (tryGetPath(to, out path)) {
				return path;
			} else {
				return null;
			}
		}

		public Router(Instance instance, State state)
		{
			Instance = instance;
			State = state;
		}

		public void Route() {
			// Loop through all pairs of components which communicate in all service chains.
			foreach (var componentPair in Instance.AllNeededRoutes()) {
				// Console.WriteLine("Component {0} is on server {1} on node {2}", componentPair.Key.Item1, State.ComponentLocationServer(componentPair.Key.Item1), State.ComponentLocationNode(componentPair.Key.Item1));
				// Console.WriteLine("Routing between components {0} and {1}", componentPair.Key.Item1, componentPair.Key.Item2);
				int c1l = State.ComponentLocationNode(componentPair.Key.Item1);
				int c2l = State.ComponentLocationNode(componentPair.Key.Item2);
				if (c1l == c2l) {
					// If two components that need to communicate are on same node, it's trivial.
					State.SetRoute(componentPair.Key.Item1, componentPair.Key.Item2, new List<int> { c1l });
					continue;
				}

				// If components are not on same node, find shortest path.
				bool foundPath = false;
				do {
					SetUpEdgesAndCosts();
					var dijkstra = ShortestPath(c1l, c2l);
					var path = new List<int>();
					bool first = true;
					foreach (var d in dijkstra) {
						if (first) {
							path.Add(d.Source);
							first = false;
						}
						path.Add(d.Target);
					}
					foundPath = State.SetRoute(componentPair.Key.Item1, componentPair.Key.Item2, path);
					if (foundPath) {
						// Console.WriteLine("Path between components {0} and {1} found: [{2}]", componentPair.Key.Item1, componentPair.Key.Item2, string.Join(", ", path));
					}
				}
				while (!foundPath);
			}
		}
	}
}
