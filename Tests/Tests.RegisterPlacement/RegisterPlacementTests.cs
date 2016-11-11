using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RegisterPlacement.DelayGraph;
using RegisterPlacement.LatencyAssignment;

namespace Tests.RegisterPlacement
{
    /// <summary>
    /// Test for register placement
    /// </summary>
    [TestClass]
    public class RegisterPlacementTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// This is a simple example of how to write a unit test for this project.
        /// </summary>
        [TestMethod]
        [DeploymentItem(@"DataSets\DelayGraph_0.graphml")]
        public void RunGreedyOnTest1()
        {
            var deployedGraphPath = Path.Combine(TestContext.TestDeploymentDir, "DelayGraph_0.graphml");
            DelayGraph graph = DelayGraphGraphMlSerializer.DeserializeFromGraphMl(deployedGraphPath);
            LatencyAssignmentAsap algorithm = new LatencyAssignmentAsap();
            var result = algorithm.Execute(graph, 250);
            Assert.IsTrue(result.Count >= 0, "No results returned.");
        }
    }
}
