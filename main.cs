using System;
using System.Threading.Tasks;
using matweaver.swf.Workflow;

namespace matweaver.swf {
	class swf
	{
		static string workflowName = "custom workflow name";
		public static void Main()
		{
			Console.WriteLine("Welcome to the Jungle!");
			workflow myWorkflow = new workflow();

			//Register all the activities, domains and workflows
			//(can also be done using GUI)
			myWorkflow.RegisterMyDomain();
			myWorkflow.RegisterActivity("Activity1A", "Activity1");
			myWorkflow.RegisterWorkflow(workflowName);

			//start the workflow
			Task.Run(() => myWorkflow.StartWorkflow(workflowName));
			Task.Run(() => myWorkflow.StartWorkflow(workflowName));
			//start the decider
			Task.Run(() => myWorkflow.Decider());
			//start the workers
			Task.Run(() => myWorkflow.Worker("Activity1"));
			Task.Run(() => myWorkflow.Worker("Activity1"));
			Console.Read();
		}
	}
}
