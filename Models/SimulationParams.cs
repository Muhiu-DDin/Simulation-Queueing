using queueanalysis.Services;

namespace queueanalysis.Models;

public class SimulationParams
{
    public string ArrivalProcess { get; set; } = string.Empty;
    public string ServiceProcess { get; set; } = string.Empty;
    public DistributionParams ArrivalParams { get; set; } = new();
    public DistributionParams ServiceParams { get; set; } = new();
    public int NumServers { get; set; }
    public int NumCustomers { get; set; } = 20;
}