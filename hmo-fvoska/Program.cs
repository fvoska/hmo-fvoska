using System;
using System.IO;
using System.Text;

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
		}
	}
}
