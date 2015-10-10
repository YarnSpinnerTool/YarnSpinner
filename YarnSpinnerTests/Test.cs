using NUnit.Framework;
using System;

namespace YarnSpinnerTests
{
	[TestFixture ()]
	public class Test
	{



		[SetUp()]
		public void Init()
		{
			var newWorkingDir = 
				System.IO.Path.Combine (Environment.CurrentDirectory, "../../Tests/");
			Environment.CurrentDirectory = newWorkingDir;
		}

		[Test ()]
		public void TestCase ()
		{

			var d = new Yarn.Dialogue()

		}
	}

}

