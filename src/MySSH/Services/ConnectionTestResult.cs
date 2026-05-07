using MySSH.Models;

namespace MySSH.Services;

public sealed record ConnectionTestResult(
    bool Success,
    string Message,
    MachineSnapshot Snapshot);
