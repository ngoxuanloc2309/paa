namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;

public interface IRuleCompiler
{
    CompileResult Compile(LogicGraph graph, ProductDefinition product, string? programId = null);

    CompileResult Compile(
        LogicGraph graph,
        ProductDefinition product,
        string? programId,
        IReadOnlyList<ModbusPortConfigDto>? modbusPorts,
        IReadOnlyList<ModbusSlaveMapEntryDto>? slaveMapConfigs);
}
