namespace SimplePLC.Application.Logic.Runtime;

public interface IRuntimeEngine
{
    Func<DateTime>? TimeProvider { get; set; }
    RuntimeSnapshot Scan(SimplePLC.Application.Logic.Compilation.CompiledProgram program, long tickMs);
    RuntimeSnapshot Scan(IReadOnlyList<SimplePLC.Domain.Models.Rule> rules, long tickMs);
    void Reset();
    long ScanCount { get; }
}
