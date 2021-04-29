# parallel-CALPUFF
A parallel algorithm for CALPUFF dispersion model

This code implements the algorithm in the manuscript "A Parallel Computing Algorithm for the Emergency-oriented Atmospheric Dispersion Model CALPUFF".
Authors:Dongou Yang, Mei Li, Hui Liu. Email of corresponding author: mli@pku.edu.cn.

The code is based on Netcoreapp2.1, RabbitMQ.Client5.1.0, and Windows Internet Information Service(IIS).

The CALPUFF model is open-source and can be downloaded on http://src.com/.

Usage:
1. Extract files.
2. Install Netcoreapp, RabbitMQ Client, and Windows IIS.
3. Build a RabbitMQ cluster for the client and the servers.
4. Run RabbitMQ service and Windows IIS.
5. Run the command "dotnet run" under the folder "CalpuffWorkerG" to start a server.
6. Run the command "dotnet run" under the folder "NewTaskG" to start a client and send the computation request.
