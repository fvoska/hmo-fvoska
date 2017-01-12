using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace hmofvoska {
	class MainClass {
		public static void Main(string[] args) {
			// First argument is path to instance file.
			string instanceFile = "instanca.txt";
			if (args.Length > 0) {
				instanceFile = args[0];
			}

			// Parse instance file.
			var instance = new Instance(instanceFile);
			Console.WriteLine(instance);

			// Initial component placement on servers.
			var init = new InitialVms(instance);
			State solution = init.InitialPlacement();

			// Set up intial routes.
			var router = new Router(instance, solution);
			router.Route();

			Console.WriteLine(solution);
			List<string> validityMessage;
			Console.WriteLine("\nSolution valid: " + solution.IsValid(out validityMessage));
			Console.WriteLine("Fitness: " + solution.CalculateFitness());

			solution.SaveToFile("res.txt");

			Console.ReadLine();
		}
	}
}
