# DelayGraph

This Delay Graph Package contains following components:
1.	Formulation of the Register Assignment Problem
2.	Specification of the Delay Graph data structures
3.	2300+ test cases extracted from realistic applications
4.	Testbench that runs multiple implementations and compares results
5.	Lesson plans for programming exercises

The motivations for creating and distributing this package are:
1.	Advance the state of the art in High Level Synthesis
2.	Provide a rich and realistic set of example graphs and testbench
3.	Educate theory and implementation of advanced graph algorithms

What is new and novel:
1.	The register assignment problem formation is novel, lending itself to new and interesting solutions
2.	The delay graph is cyclic, encouraging learning and development of cyclic graph algorithms
3.	The delay graph can also be treated as directed acyclic graph, enabling acyclic graph algorithms
4.	Large number of huge example graphs, and a testbench that runs different algorithms over this data set, allows easy compare of user algorithms
5.	Lesson plans to introduce programming graph algorithms for large graphs.

Material:
1.	C# Source code of Delay Graph, two Register Assignment algorithms, and Testbench that runs these algorithms over test set
2.	Test set of 2300+ Delay Graph and Clock Period Constraints (both in GraphML xml format).
3.	Documentation of the Delay Graph Data Structures, the Register Assignment Problem, and Suggested lesson plans

# Running for the first time
Extract the contents of DataSets.zip into the root directory. 
After building you can use this command to run for the first time from the root directory
RegisterPlacement\bin\Debug\RegisterPlacement.exe DataSets Scorecards

Note: The extracted DataSets directory should not be submitted to github as it is too large. Any 
additional data sets must be added to DataSets.zip

## Detailed Documentation

See the included RegisterAssignmentProblem.docx for further Documentation
