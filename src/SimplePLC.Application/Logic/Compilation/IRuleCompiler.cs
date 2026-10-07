namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Models;

public interface IRuleCompiler
{
    CompileResult Compile(LogicGraph graph, ProductDefinition product, string? programId = null);
}
