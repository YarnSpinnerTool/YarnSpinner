using NUnit.Framework;
using System;

namespace YarnSpinnerTests
{
	[TestFixture ()]
	public class Test
	{

		Yarn.MemoryVariableStore storage = new Yarn.MemoryVariableStore();

		[SetUp()]
		public void Init()
		{
			var testFolder = Environment.GetEnvironmentVariable ("TEST_DIR");

			var newWorkingDir = 
				System.IO.Path.Combine (Environment.CurrentDirectory, testFolder);
			Environment.CurrentDirectory = newWorkingDir;
		}

		[Test ()]
		public void TestCase ()
		{

			var d = new Yarn.Dialogue (storage);

		}
	}

}

