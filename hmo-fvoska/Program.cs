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

			State state = new State(instance);
			state.PutVmsOnServer(1, 7);
			state.PutVmsOnServer(2, 3);
			state.PutVmsOnServer(44, 1);
			state.PutVmsOnServer(7, 1);
			state.PutVmsOnServer(9, 1);
			state.PutVmsOnServer(5, 2);
			state.PutVmsOnServer(11, 2);
			state.SetRoute(7, 9, new List<int> { 1, 4, 2 });
			state.SetRoute(5, 11, new List<int> { 7, 6 });
			Console.WriteLine(state);
			Console.WriteLine(state.CalculateFitness());
			state.SaveToFile("/home/fiouch/res1.txt");
		}
	}
}
