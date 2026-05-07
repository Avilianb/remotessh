namespace MySSH.Models;

public sealed class MachineSnapshot
{
    public string Os { get; set; } = "";

    public string CpuCores { get; set; } = "";

    public string MemoryTotal { get; set; } = "";

    public string DiskTotal { get; set; } = "";
}
