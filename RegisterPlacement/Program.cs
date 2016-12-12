using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RegisterPlacement.DelayGraph;
using RegisterPlacement.LatencyAssignment;

namespace RegisterPlacement
{
    public class Program
    {
        #region Public Methods

        public static void Main(string[] args)
        {
            Arguments parsedArguments;
            if (!TryParseCommandLine(args, out parsedArguments))
            {
                return;
            }

            var algorithms = new List<Tuple<string, ILatencyAssignment>>
            {
                new Tuple<string, ILatencyAssignment>("asap", new LatencyAssignmentAsap()),
                new Tuple<string, ILatencyAssignment>("greedy", new LatencyAssignmentGreedy()),
                // add your own latency assigner here
            };

            List<DataSet> dataSets = new List<DataSet>();
            FindDataSetsRecursively(Path.GetFullPath(parsedArguments.DataSetDirectory), dataSets);

            if (!dataSets.Any())
            {
                Console.WriteLine("Warning: No data sets found. Make sure DelayGraph*.graphml and corresponding DelayGraphOriginalGoals*.xml files exist somewhere in this hierarchy: " + args[0]);
            }

            List<string> algorithmNames = algorithms.Select(kv => kv.Item1).ToList();
            string dotDirectory;
            SetupResultsDirectories(algorithmNames, parsedArguments, out dotDirectory);
            var resultsSummary = InitializeResultsSummary(algorithmNames);
            Stopwatch sw = new Stopwatch();
            foreach (DataSet dataSet in dataSets)
            {
                foreach (var graphAndGoal in dataSet.GraphsAndGoalsFilePaths)
                {
                    DelayGraph.DelayGraph graph = DelayGraphGraphMlSerializer.DeserializeFromGraphMl(graphAndGoal.GraphPath);

                    XDocument doc = XDocument.Load(graphAndGoal.GoalPath);
                    XElement clockRateElement = doc.Root.Element("TargetClockPeriodInPicoSeconds");

                    int originalTargetClockRate = int.Parse(clockRateElement.Value);

                    if (DelayGraphSolution.PruneEdges(graph))
                    {
                        Console.WriteLine("Successfully removed redundant edges in graph");
                    }

                    int minClockPeriod = DelayGraphSolution.MinClockPeriod(graph);
                    if (minClockPeriod > originalTargetClockRate)
                    {
                        originalTargetClockRate = minClockPeriod;
                    }
                    bool failed = false;
                    var scoreCards = new Dictionary<string, LatencyAssignerScoreCard>();
                    foreach (var tuple in algorithms)
                    {
                        string algorithmName = tuple.Item1;
                        var algorithm = tuple.Item2;
                        sw.Restart();
                        HashSet<DelayGraphVertex> registeredTerminals = algorithm.Execute(graph, originalTargetClockRate);
                        double milliseconds = sw.Elapsed.TotalMilliseconds;
                        sw.Stop();

                        var solutionName = Path.GetFileNameWithoutExtension(graphAndGoal.GraphPath) + "." + algorithmName;

                        DelayGraphSolution solution = new DelayGraphSolution(solutionName, graph, registeredTerminals, originalTargetClockRate);

                        // for debugging

                        var dotFileName = solutionName + ".dot";
                        var dotFilePath = Path.Combine(dotDirectory, dotFileName);
                        solution.PrintDotFile(dotFilePath);

                        bool cycle;
                        int period = solution.EstimatePeriod(out cycle);
                        if (cycle)
                        {
                            period = int.MaxValue;
                        }

                        int slack = originalTargetClockRate - period;
                        if (slack < 0)
                        {
                            failed = true;
                            resultsSummary[algorithmName].FailedCount++;
                        }

                        long throughputCost, latencyCost, registerCost;
                        solution.SumCosts(
                            throughputTotalCost: out throughputCost,
                            latencyTotalCost: out latencyCost,
                            registersTotalCost: out registerCost);
                        var scoreCard = new LatencyAssignerScoreCard();
                        scoreCard.RegisterResult(throughputCost, latencyCost, registerCost, period, slack, milliseconds);
                        scoreCards[algorithmName] = scoreCard;

                    }
                    TabulateAndPrintReports(algorithmNames, parsedArguments.ScoreCardDirectory, resultsSummary, graphAndGoal, originalTargetClockRate, failed, scoreCards);
                }
            }

            PrintSummaryReport(algorithmNames, parsedArguments.ScoreCardDirectory, resultsSummary);

            Console.WriteLine("Scorecards written to: " + Path.GetFullPath(parsedArguments.ScoreCardDirectory));
            Debug.WriteLine("Scorecards written to: " + Path.GetFullPath(parsedArguments.ScoreCardDirectory));
        }

        #endregion

        #region Private Methods

        private static string BuildExpectedGoalFileName(string uniquifierFromDelayGraphFileName)
        {
            return "OriginalGoals" + uniquifierFromDelayGraphFileName + ".xml";
        }

        private static void CheckAgainstOthers(List<string> latencyAssigners, Dictionary<string, LatencyAssignerScoreCard> scoreCards, string latencyAssigner, LatencyAssignerScoreCard scoreCard, out bool isBest, out bool isTied)
        {
            isBest = true;
            isTied = false;
            foreach (var next in latencyAssigners)
            {
                if (next != latencyAssigner && ScoreIsBetter(scoreCards[next], scoreCard))
                {
                    if (ScoreIsEqual(scoreCards[next], scoreCard))
                    {
                        isTied = true;
                    }
                    else
                    {
                        isBest = false;
                    }
                    break;
                }
            }
        }

        private static void FindDataSetsRecursively(string root, List<DataSet> dataSets)
        {
            IList<string> graphFilePaths = Directory.EnumerateFiles(root, "DelayGraph*.graphml").ToList();

            if (graphFilePaths.Any())
            {
                List<GraphAndGoalPaths> graphsAndGoalsFilePaths = new List<GraphAndGoalPaths>();
                foreach (string graphFilePath in graphFilePaths)
                {
                    string graphFileName = Path.GetFileName(graphFilePath);
                    string uniquifier = ParseOutUniquifier(graphFileName);
                    string goalFileName = BuildExpectedGoalFileName(uniquifier);
                    IList<string> goalFilePaths = Directory.EnumerateFiles(root, goalFileName).ToList();
                    if (goalFilePaths.Count != 1)
                    {
                        Console.WriteLine("Error: Couldn't find DelayGraphOriginalGoals*.xml file for delay graph: " + graphFilePath);
                        continue;
                    }
                    GraphAndGoalPaths graghAndGoalPath = new GraphAndGoalPaths(graphFilePath, goalFilePaths.First());
                    graphsAndGoalsFilePaths.Add(graghAndGoalPath);
                }
                if (graphsAndGoalsFilePaths.Any())
                {
                    DataSet dataSet = new DataSet(root, graphsAndGoalsFilePaths);
                    dataSets.Add(dataSet);
                }
            }

            IEnumerable<string> subDirs = Directory.EnumerateDirectories(root);
            foreach (string subDir in subDirs)
            {
                FindDataSetsRecursively(subDir, dataSets);
            }
        }

        private static string GetScoreCardPath(string scoreCardDirectoy, string algorithmName)
        {
            string fileName = algorithmName + "_ScoreCard.csv";
            string filePath = Path.Combine(scoreCardDirectoy, fileName);
            return filePath;
        }

        private static IDictionary<string, ResultInformation> InitializeResultsSummary(IList<string> algorithmNames)
        {
            var dictionary = new Dictionary<string, ResultInformation>();
            foreach (var algorithmName in algorithmNames)
            {
                dictionary[algorithmName] = new ResultInformation();
            }
            return dictionary;
        }

        private static string ParseOutUniquifier(string graphFileName)
        {
            string prependage = "DelayGraph";
            int dotLocationIndex = graphFileName.IndexOf('.');
            int midPieceIndex = prependage.Length;
            string middlePiece = graphFileName.Substring(midPieceIndex, dotLocationIndex - midPieceIndex);
            return middlePiece;
        }

        private static void PrintSummaryReport(IEnumerable<string> algorithmNames, string scoreCardDirectoy,
            IDictionary<string, ResultInformation> resultsSummary)
        {
            string summaryPath = Path.Combine(scoreCardDirectoy, "Summary.csv");
            if (File.Exists(summaryPath))
            {
                File.Delete(summaryPath);
            }
            using (StreamWriter stream = File.CreateText(summaryPath))
            {
                stream.WriteLine("Algorithm, TotalThroughputCost, TotalLatencyCost, TotalRegisterCost, TotalPeriodSum, PercentFailingPeriodConstraint, TotalExecutionTime(ms), Best, Best-Or-Tied, Failed"); // column headers
            }
            using (StreamWriter stream = File.AppendText(summaryPath))
            {
                foreach (var algorithmName in algorithmNames)
                {
                    string score = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}",
                        algorithmName,
                        resultsSummary[algorithmName].OverallScoreCard.TotalThroughputCosts,
                        resultsSummary[algorithmName].OverallScoreCard.TotalLatencyCosts,
                        resultsSummary[algorithmName].OverallScoreCard.TotalRegisterCosts,
                        resultsSummary[algorithmName].OverallScoreCard.TotalSumOfAchievedPeriods,
                        resultsSummary[algorithmName].OverallScoreCard.PercentFailing,
                        resultsSummary[algorithmName].OverallScoreCard.TotalExecutionTime,
                        resultsSummary[algorithmName].BestCount,
                        resultsSummary[algorithmName].BestOrTiedCount,
                        resultsSummary[algorithmName].FailedCount);
                    stream.WriteLine(score);
                }
            }
        }

        private static bool ScoreIsBetter(LatencyAssignerScoreCard x, LatencyAssignerScoreCard y)
        {
            if (x.TotalNumberFailingPeriod < y.TotalNumberFailingPeriod ||
                (x.TotalNumberFailingPeriod == y.TotalNumberFailingPeriod &&
                 (x.TotalThroughputCosts < y.TotalThroughputCosts ||
                  (x.TotalThroughputCosts == y.TotalThroughputCosts &&
                   (x.TotalLatencyCosts < y.TotalLatencyCosts ||
                    (x.TotalLatencyCosts == y.TotalLatencyCosts &&
                     (x.TotalRegisterCosts < y.TotalRegisterCosts ||
                      (x.TotalRegisterCosts == y.TotalRegisterCosts &&
                       (x.TotalSumOfAchievedPeriods < y.TotalSumOfAchievedPeriods ||
                        (x.TotalSumOfAchievedPeriods == y.TotalSumOfAchievedPeriods &&
                         x.TotalExecutionTime < y.TotalExecutionTime))))))))))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool ScoreIsEqual(LatencyAssignerScoreCard x, LatencyAssignerScoreCard y)
        {
            if (x.TotalNumberFailingPeriod == y.TotalNumberFailingPeriod &&
                x.TotalThroughputCosts == y.TotalThroughputCosts &&
                x.TotalLatencyCosts == y.TotalLatencyCosts &&
                x.TotalRegisterCosts == y.TotalRegisterCosts &&
                x.TotalSumOfAchievedPeriods == y.TotalSumOfAchievedPeriods)
            {
                return true;
            }
            return false;
        }

        private static void SetupResultsDirectories(IList<string> algorithmNames, Arguments arguments, out string dotDirectory)
        {
            Directory.CreateDirectory(arguments.ScoreCardDirectory);

            foreach (var algorithmName in algorithmNames)
            {
                string filePath = GetScoreCardPath(arguments.ScoreCardDirectory, algorithmName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                using (StreamWriter stream = File.CreateText(filePath))
                {
                    stream.WriteLine(
                        "GraphPath, ThroughputCost, LatencyCost, RegisterCost, Slack, TargetPeriod, ResultingPeriod, ExecutionTime(ms)");
                        // column headers
                }
            }

            dotDirectory = "DotFiles";
            if (!Directory.Exists(dotDirectory))
            {
                Directory.CreateDirectory(dotDirectory);
            }
        }

        private static void TabulateAndPrintReports(List<string> algorithmNames, 
                                                    string scoreCardDirectoy,
                                                    IDictionary<string, ResultInformation> resultsSummary,
                                                    GraphAndGoalPaths graphAndGoal,
                                                    int originalTargetClockRate,
                                                    bool failed,
                                                    Dictionary<string, LatencyAssignerScoreCard> scoreCards)
        {
            foreach (string algorithmName in algorithmNames)
            {
                var scoreCard = scoreCards[algorithmName];
                int slack = originalTargetClockRate - (int)scoreCard.TotalSumOfAchievedPeriods;
                if (failed)
                {
                    // do not sum costs for failed test - no comparison
                    resultsSummary[algorithmName].OverallScoreCard.RegisterResult(0, 0, 0, 0, slack, scoreCard.TotalExecutionTime);
                }
                else
                {
                    // sum costs for passed test
                    resultsSummary[algorithmName].OverallScoreCard.RegisterResult(scoreCard.TotalThroughputCosts,
                                                                      scoreCard.TotalLatencyCosts,
                                                                      scoreCard.TotalRegisterCosts,
                                                                      (int)scoreCard.TotalSumOfAchievedPeriods,
                                                                      slack,
                                                                      scoreCard.TotalExecutionTime);
                }

                bool best;
                bool tied;
                CheckAgainstOthers(algorithmNames, scoreCards, algorithmName, scoreCard, out best, out tied);

                // count how many best and best-or-tied tests for each assigner
                if (best)
                {
                    resultsSummary[algorithmName].BestOrTiedCount++;
                    if (!tied)
                    {
                        resultsSummary[algorithmName].BestCount++;
                    }
                }

                string filePath = GetScoreCardPath(scoreCardDirectoy, algorithmName);
                using (StreamWriter stream = File.AppendText(filePath))
                {
                    string score = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                                    "{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                                                    graphAndGoal.GraphPath,
                                                    scoreCard.TotalThroughputCosts,
                                                    scoreCard.TotalLatencyCosts,
                                                    scoreCard.TotalRegisterCosts,
                                                    slack,
                                                    originalTargetClockRate,
                                                    scoreCard.TotalSumOfAchievedPeriods,
                                                    scoreCard.TotalExecutionTime,
                                                    (best ? (tied ? "*" : "**") : " "));
                    stream.WriteLine(score);
                }
            }
        }

        private static bool TryParseCommandLine(string[] args, out Arguments arguments)
        {
            arguments = new Arguments();
            if (args.Length != 2)
            {
                Console.WriteLine("Error: Expecting two arguments and not " + args.Length);
                Console.WriteLine("Usage: RegisterPlacement.exe <Data Set Directory Root> <Directory For Scorecard>");
                Console.WriteLine(
                    "This program explores subdirectories of <Data Set Directory Root> to find delay graph data sets to try with register placement algorithms.");
                Console.WriteLine("Various metrics are printed into the <Directory For Scorecard>.");
                return false;
            }
            if (!Directory.Exists(args[0]))
            {
                Console.WriteLine("Error: Directory does not exist: " + args[0]);
                return false;
            }
            arguments.ScoreCardDirectory = args[1];
            arguments.DataSetDirectory = args[0];
            return true;
        }

        #endregion

        #region Nested type: Arguments

        /// <summary>
        /// Simple container class for command line arguments after they're parsed to make it easier to keep track of.
        /// </summary>
        private class Arguments
        {
            #region Internal Properties

            /// <summary>
            /// The directory where the user has stored the data sets.
            /// </summary>
            internal string DataSetDirectory { get; set; }

            /// <summary>
            /// The directory where the user wants to save scorecard results from the run.
            /// </summary>
            internal string ScoreCardDirectory { get; set; }

            #endregion
        }

        #endregion

        #region Nested type: DataSet

        internal class DataSet
        {
            #region Constructors

            internal DataSet(string directory, List<GraphAndGoalPaths> graphsAndGoalsPaths)
            {
                Directory = directory;
                GraphsAndGoalsFilePaths = graphsAndGoalsPaths;
            }

            #endregion

            #region Internal Properties

            internal string Directory { get; private set; }
            internal List<GraphAndGoalPaths> GraphsAndGoalsFilePaths { get; }

            #endregion
        }

        #endregion

        #region Nested type: GraphAndGoalPaths

        internal class GraphAndGoalPaths
        {
            #region Constructors

            internal GraphAndGoalPaths(string graphPath, string goalPath)
            {
                GraphPath = graphPath;
                GoalPath = goalPath;
            }

            #endregion

            #region Internal Properties

            internal string GoalPath { get; }
            internal string GraphPath { get; }

            #endregion
        }

        #endregion

        #region Nested type: LatencyAssignerScoreCard

        internal class LatencyAssignerScoreCard
        {
            #region Constructors

            internal LatencyAssignerScoreCard()
            {
                TotalThroughputCosts = 0;
                TotalLatencyCosts = 0;
                TotalRegisterCosts = 0;
                TotalSumOfAchievedPeriods = 0;
                TotalNumberFailingPeriod = 0;
                TotalCases = 0;
                TotalExecutionTime = 0.0;
            }

            #endregion

            #region Internal Properties

            internal double PercentFailing => (double)TotalNumberFailingPeriod / TotalCases;
            internal long TotalCases { get; private set; }
            internal double TotalExecutionTime { get; private set; }
            internal long TotalLatencyCosts { get; private set; }
            internal long TotalNumberFailingPeriod { get; private set; }
            internal long TotalRegisterCosts { get; private set; }
            internal long TotalSumOfAchievedPeriods { get; private set; }

            internal long TotalThroughputCosts { get; private set; }

            #endregion

            #region Internal Methods

            internal void RegisterResult(
                long throughputCost,
                long latencyCost,
                long registerCost,
                int periodAchieved,
                int slack,
                double executionTime)
            {
                TotalThroughputCosts += throughputCost;
                TotalLatencyCosts += latencyCost;
                TotalRegisterCosts += registerCost;
                TotalSumOfAchievedPeriods += periodAchieved;
                TotalExecutionTime += executionTime;
                TotalCases++;
                if (slack < 0)
                {
                    TotalNumberFailingPeriod++;
                }
            }

            #endregion
        }

        #endregion

        #region Nested type: ResultInformation

        private class ResultInformation
        {
            #region Internal Properties

            internal int BestCount { get; set; }
            internal int BestOrTiedCount { get; set; }
            internal int FailedCount { get; set; }
            internal LatencyAssignerScoreCard OverallScoreCard { get; } = new LatencyAssignerScoreCard();

            #endregion
        }

        #endregion
    }
}
