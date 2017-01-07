using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace hmofvoska
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			string instanceFile = "instanca.txt";
			if (args.Length > 0) {
				instanceFile = args[0];
			}

			Instance instance = new Instance(instanceFile);
			Console.WriteLine(instance);

			InitialVms init = new InitialVms(instance);

			State initialSolution = init.InitialPlacement();

			Console.WriteLine(initialSolution);

			//state.SaveToFile("/home/fiouch/res1.txt");
		}
	}
}
