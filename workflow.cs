using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Amazon;
using Amazon.SimpleWorkflow;
using Amazon.SimpleWorkflow.Model;

namespace matweaver.swf.Workflow
{
    class workflow
    {

        static string domainName = "Fill In Custom Domain Name Here";
        static string id = "Fill In Custom Amazon Api Id Here";
        static string key = "Fill In Custom Amazon Api Key Here";
        
        static string deciderPollingKey = "CUSTOM TASK NAME HERE DECIDER POLLS FROM THIS";

        static AmazonSimpleWorkflowClient swfClient = new AmazonSimpleWorkflowClient(id, key, RegionEndpoint.USWest2);

        public workflow() { }
        public void RegisterMyDomain()
        {
            var request = new ListDomainsRequest();
            request.RegistrationStatus = RegistrationStatus.REGISTERED;

            var results = swfClient.ListDomainsAsync(request).Result;


            if (results.DomainInfos.Infos.FirstOrDefault(x => x.Name == domainName) == null)
            {
                RegisterDomainRequest registerRequest = new RegisterDomainRequest()
                {
                    Name = domainName,
                    Description = "Custom Description Here",
                    WorkflowExecutionRetentionPeriodInDays = "1"
                };
                swfClient.RegisterDomainAsync(registerRequest);
                Console.WriteLine("Domain Created: " + domainName);
            }
        }

        public void RegisterActivity(string name, string taskListName)
        {
            var listActivityRequest = new ListActivityTypesRequest()
            {
                Domain = domainName,
                Name = name,
                RegistrationStatus = RegistrationStatus.REGISTERED
            };
            var results = swfClient.ListActivityTypesAsync(listActivityRequest).Result;
            if (results.ActivityTypeInfos.TypeInfos.FirstOrDefault(x => x.ActivityType.Name == name) == null)
            {
                RegisterActivityTypeRequest request = new RegisterActivityTypeRequest()
                {
                    Name = name,
                    Domain = domainName,
                    Description = "Custom Description Here",
                    Version = "1.0",
                    DefaultTaskList = new TaskList() { Name = taskListName },//Worker poll based on this
                    DefaultTaskScheduleToStartTimeout = "150",
                    DefaultTaskStartToCloseTimeout = "450",
                    DefaultTaskHeartbeatTimeout = "NONE",
                    DefaultTaskScheduleToCloseTimeout = "350"
                };
                swfClient.RegisterActivityTypeAsync(request);
                Console.WriteLine("Created Activity: " + request.Name);
            }
        }
        public void RegisterWorkflow(string name)
        {
            var listWorkflowRequest = new ListWorkflowTypesRequest()
            {
                Name = name,
                Domain = domainName,
                RegistrationStatus = RegistrationStatus.REGISTERED
            };
            var results = swfClient.ListWorkflowTypesAsync(listWorkflowRequest).Result;
            if (results.WorkflowTypeInfos.TypeInfos.FirstOrDefault(x => x.WorkflowType.Name == name) == null)
            {
                RegisterWorkflowTypeRequest request = new RegisterWorkflowTypeRequest()
                {
                    DefaultChildPolicy = ChildPolicy.TERMINATE,
                    DefaultExecutionStartToCloseTimeout = "350",
                    DefaultTaskList = new TaskList()
                    {
                        Name = deciderPollingKey
                    },
                    DefaultTaskStartToCloseTimeout = "150",
                    Domain = domainName,
                    Name = name,
                    Version = "1.0"
                };

                swfClient.RegisterWorkflowTypeAsync(request);

                Console.WriteLine("Registerd Workflow: " + request.Name);
            }
        }
        public void Worker(string tasklistName)
        {
            while (true)
            {
                Console.WriteLine("Starting Worker" + tasklistName + ": Polling for activity...");
                PollForActivityTaskRequest pollForActivityTaskRequest =
                    new PollForActivityTaskRequest()
                    {
                        Domain = domainName,
                        TaskList = new TaskList()
                        {
                            Name = tasklistName
                        }
                    };
                PollForActivityTaskResponse pollForActivityTaskResponse = swfClient.PollForActivityTaskAsync(pollForActivityTaskRequest).Result;
                Console.WriteLine("finished polling pollForActivityTaskResponse");
                RespondActivityTaskCompletedRequest respondActivityTaskCompletedRequest =
                //Ideally this function would actually do something and return real values. But since it's just hello
                //world we're returning nothing of value. (a static string)
                new RespondActivityTaskCompletedRequest()
                {
                    Result = "{\"customReturnValue\":\"CustomReturnResult\"}",
                    TaskToken = pollForActivityTaskResponse.ActivityTask.TaskToken
                };
                if (pollForActivityTaskResponse.ActivityTask.ActivityId == null)
                {
                    Console.WriteLine("Starting Worker" + tasklistName + ": NULL");
                }
                else
                {
                    RespondActivityTaskCompletedResponse respondActivityTaskCompletedResponse =
                        swfClient.RespondActivityTaskCompletedAsync(respondActivityTaskCompletedRequest).Result;
                    Console.WriteLine("Starting Worker" + tasklistName + ": Activity completed" + pollForActivityTaskResponse.ActivityTask.ActivityId);
                }
            }
        }


        public void ScheduleActivity(string name, List<Decision> decisions)
        {
            Decision decision = new Decision()
            {
                DecisionType = DecisionType.ScheduleActivityTask,
                ScheduleActivityTaskDecisionAttributes =  // Uses DefaultTaskList
                new ScheduleActivityTaskDecisionAttributes()
                {
                    ActivityType = new ActivityType()
                    {
                        Name = name,
                        Version = "1.0"
                    },
                    ActivityId = name + "-" + System.Guid.NewGuid().ToString(),
                    Input = "{\"customSentDataValue\":\"CustomSentDataResult\"}"
                }
            };
            Console.WriteLine("Decider: " + decision.ScheduleActivityTaskDecisionAttributes.ActivityId);
            decisions.Add(decision);
        }

        public void Decider()
        {
            int activityCount = 0;
            while (true)
            {
                Console.WriteLine("Decider: Polling for decision task ...");
                PollForDecisionTaskRequest request = new PollForDecisionTaskRequest()
                {
                    Domain = domainName,
                    TaskList = new TaskList() { Name = deciderPollingKey }
                };

                PollForDecisionTaskResponse response = swfClient.PollForDecisionTaskAsync(request).Result;
                if (response.DecisionTask.TaskToken == null)
                {
                    Console.WriteLine("Decider: NULL");
                    continue;
                }

                int completedActivityTaskCount = 0, totalActivityTaskCount = 0;
                foreach (HistoryEvent e in response.DecisionTask.Events)
                {
                    Console.WriteLine("Decider: EventType - " + e.EventType +
                        ", EventId - " + e.EventId);
                    if (e.EventType == "ActivityTaskCompleted")
                        completedActivityTaskCount++;
                    if (e.EventType.Value.StartsWith("Activity"))
                        totalActivityTaskCount++;
                }
                Console.WriteLine(".... completedCount=" + completedActivityTaskCount);

                List<Decision> decisions = new List<Decision>();
                if (totalActivityTaskCount == 0)
                {
                    ScheduleActivity("Activity1", decisions);
                    activityCount = 4;
                }
                else if (completedActivityTaskCount == activityCount)
                {
                    Decision decision = new Decision()
                    {
                        DecisionType = DecisionType.CompleteWorkflowExecution,
                        CompleteWorkflowExecutionDecisionAttributes =
                        new CompleteWorkflowExecutionDecisionAttributes
                        {
                            Result = "{\"Result\":\"WF Complete!\"}"
                        }
                    };
                    decisions.Add(decision);

                    Console.WriteLine("Worflow Complete");
                }
                RespondDecisionTaskCompletedRequest respondDecisionTaskCompletedRequest =
                    new RespondDecisionTaskCompletedRequest()
                    {
                        Decisions = decisions,
                        TaskToken = response.DecisionTask.TaskToken
                    };
                swfClient.RespondDecisionTaskCompletedAsync(respondDecisionTaskCompletedRequest);
            }
        }


        public void StartWorkflow(string name)
        {
            string workflowID = "WorkflowID - " + DateTime.Now.Ticks.ToString();
            swfClient.StartWorkflowExecutionAsync(new StartWorkflowExecutionRequest()
            {
                Input = "{\"customKey\":\"customvalue\"}",

                WorkflowId = workflowID,
                Domain = domainName,
                WorkflowType = new WorkflowType()
                {
                    Name = name,
                    Version = "1.0"
                }
            });
            Console.WriteLine("Workflow Started:" + workflowID);
        }

    }
}
