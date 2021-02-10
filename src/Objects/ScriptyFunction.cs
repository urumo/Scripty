using System.Collections.Generic;
using System.Linq;
using Scripty.Expressions;
using Scripty.Interfaces;
using Scripty.Statements;

namespace Scripty.Objects
{
    public class ScriptyFunction : IObject
    {
        public List<Identifier> Parameters { get; set; }
        public BlockStatement Body { get; set; }
        public ScriptyEnvironment Environment { get; set; }

        public string Type() => ObjectType.FunctionObj;

        public string Inspect() =>
            $"fun({string.Join(", ", Parameters.Select(identifier => identifier.Str()).ToList())}){{\n{Body.Str()}\n}}";
    }
}