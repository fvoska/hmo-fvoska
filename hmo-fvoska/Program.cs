using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace hmofvoska {
	class MainClass {
		public static double GetRandomNumber(double minimum, double maximum) {
			var rnd = new Random();
			return rnd.NextDouble() * (maximum - minimum) + minimum;
		}

		public static void Main(string[] args) {
			var t1 = DateTime.Now;
			// First argument is path to instance file.
			string instanceFile = "instanca.txt";
			if (args.Length > 0) {
				instanceFile = args[0];
			}

			double minTemp = 1e-8; // TRY WITH: 1e-8 Limits algorithm running time.
			int maxIterations = 10000; // Not really important, but this is another way to limit how long algorithm runs.
			double initTemp = 5; // TRY WITH: 5
			double temp = initTemp;
			double alpha = 0.9; // TRY WITH: 0.99
			int tabooListLength = 10; // TRY WITH: 10
			bool sortServers = false; // TRY WITH: false

			// Parse instance file.
			var instance = new Instance(instanceFile);
			// Console.WriteLine(instance);
			var t2 = DateTime.Now;
			Console.WriteLine("\t\t\t\t\t\t\t\tParsing took {0}s", (t2 - t1).TotalSeconds);

			// tabooListLength = (int)Math.Sqrt(instance.numVms);
			// Console.WriteLine(tabooListLength);

			// Initial component placement on servers.
			var init = new InitialVms(instance);
			State initSolution = init.InitialPlacement(sortServers);

			// Set up intial routes.
			var router = new Router(instance, initSolution);
			router.Route();

			Console.WriteLine("== INITIAL SOLUTION");
			List<string> validityMessage;
			double initFitness = initSolution.CalculateFitness();
			Console.WriteLine("\tFitness: " + initFitness);
			var t3 = DateTime.Now;
			Console.WriteLine("\t\t\t\t\t\t\t\tCalculating initial solution took {0}s", (t3 - t2).TotalSeconds);
			initSolution.SaveToFile("res-initial.txt");
			initSolution.SaveToFile("res-" + initFitness + ".txt");

			double min = initFitness;
			double lastFitness = initFitness;
			State bestSolution = initSolution;
			State currentSolution = initSolution;
			var taboo = new LimitedQueue<int>(tabooListLength);
			bool marked1m = false;
			bool marked5m = false;
			bool marked60m = false;
			int totalDooms = 0;
			//bool stuck = false;
			for (int iteration = 1; iteration <= maxIterations; iteration++) {
				Console.WriteLine("== ITERATION {0}/{1}; TEMP = {2}", iteration, maxIterations, temp);

				// Generate neighbours.
				var neighbours = currentSolution.GenerateNeighbours();
				neighbours.Shuffle();

				// Choose one neighbour based on probability (simulated annealing).
				bool chosen = false;
				int neighbourIndex = 0;
				int doomsdayCounter = 0; // Counts how many neighbours are invalid solutions.
				do {
					var neighbour = neighbours[neighbourIndex];
					neighbourIndex = (neighbourIndex + 1) % neighbours.Count;

					if (taboo.Contains(neighbour.SwappedComponent)) continue;

					router = new Router(instance, neighbour);
					bool foundRoute = false;
					try {
						foundRoute = router.Route();
					} catch (Exception ex) {
						// Error finding route - skipping neighbour.
						Console.WriteLine(ex.Message);
						Console.WriteLine("!!! Error finding route - skipping neighbour !!!");
						//stuck = true; break;
						continue;
					}
					if (!foundRoute) {
						doomsdayCounter++;
						if (doomsdayCounter == neighbours.Count) {
							// In case none of the neighbours have valid routes, go back to initial solution.
							neighbourIndex = 0;
							doomsdayCounter = 0;
							totalDooms++;
							currentSolution = initSolution;
							neighbours = currentSolution.GenerateNeighbours();
							temp = initTemp / Math.Pow(alpha, totalDooms);
							Console.WriteLine("!!! None of the neighbours have valid routes, going back to initial solution !!!");
							continue;
						}
						// Console.WriteLine(neighbourIndex);
						continue;
					} 
					doomsdayCounter = 0;
					var validSolution = neighbour.IsValid(out validityMessage);
					if (validSolution) {
						double nf = neighbour.CalculateFitness();
						if (nf < currentSolution.Fitness) {
							neighbour.Probability = 1;
						} else {
							double delta = Math.Abs(nf - lastFitness);
							neighbour.Probability = Math.Exp(-delta / temp);
						}
						var random = GetRandomNumber(0, 1);
						// Console.WriteLine(random + " = random ? probability = " + neighbour.Probability);
						if (random < neighbour.Probability) {
							currentSolution = neighbour;
							if (currentSolution.Fitness < bestSolution.Fitness) {
								bestSolution = currentSolution;
								bestSolution.SaveToFile("res-" + nf + ".txt");
							}
							chosen = true;
							taboo.Enqueue(currentSolution.SwappedComponent);
							Console.WriteLine("\tChose neighbour with fitness {0}", neighbour.Fitness);
							break;
						}
					}
				}
				while (!chosen);
				// if (stuck) break;

				// Reduce temperature for next itteration.
				temp *= alpha;
				if (temp < minTemp) {
					Console.WriteLine("\n\t\t\t\t\t\t\t\tReached minimum temperature {0} / {1}", temp, minTemp);
					break;
				}

				var t4 = DateTime.Now;
				if (!marked1m && (t4 - t1).TotalMinutes >= 1) {
					bestSolution.SaveToFile("res-1m-voska-" + t1.ToString("yyyyMMddHHmmssffff") + "-" + bestSolution.Fitness +".txt");
					Console.WriteLine("\t\t\t\t\t\t\t\tSaving 1m solution.");
					marked1m = true;
				}
				if (!marked5m && (t4 - t1).TotalMinutes >= 5) {
					bestSolution.SaveToFile("res-5m-voska-" + t1.ToString("yyyyMMddHHmmssffff") + "-" + bestSolution.Fitness + ".txt");
					Console.WriteLine("\t\t\t\t\t\t\t\tSaving 5m solution.");
					marked5m = true;
				}
				if (!marked60m && (t4 - t1).TotalMinutes >= 60) {
					bestSolution.SaveToFile("res-1h-voska-" + t1.ToString("yyyyMMddHHmmssffff") + "-" + bestSolution.Fitness + ".txt");
					Console.WriteLine("\t\t\t\t\t\t\t\tSaving 1h solution.");
					marked60m = true;
				}
				Console.WriteLine("\t\t\t\t\t\t\t\tTotal time: {0}s", (t4 - t1).TotalSeconds);
				Console.WriteLine("\t\t\t\t\t\t\t\tBest so far:" + bestSolution.Fitness);
			}

			Console.WriteLine("== BEST SOLUTION");
			Console.WriteLine(bestSolution);
			Console.WriteLine("\tFitness: " + bestSolution.CalculateFitness());
			Console.WriteLine("== FINISHED");
			Console.ReadLine();
		}
	}
}
