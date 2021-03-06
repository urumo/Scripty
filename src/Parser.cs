namespace Scripty
{
    using System.Collections.Generic;
    using Delegates;
    using Enums;
    using Expressions;
    using Interfaces;
    using Literals;
    using Statements;

    public class Parser
    {
        public Parser(Lexer lexer)
        {
            SetPrecedences();
            SetPrefixFns();
            SetInfixFns();
            Lexer = lexer;
            Errors = new List<string>();
            NextToken();
            NextToken();
        }

        private Lexer Lexer { get; }
        private Token CurrentToken { get; set; }
        private Token PeekToken { get; set; }
        public List<string> Errors { get; }

        private Dictionary<string, PrefixParseFn> PrefixParseFns { get; set; }
        private Dictionary<string, InfixParseFn> InfixParseFns { get; set; }
        private Dictionary<string, Precedences> Precedences { get; set; }

        private void SetPrecedences() =>
            Precedences = new Dictionary<string, Precedences>
            {
                {Token.Eq, Enums.Precedences.Equals},
                {Token.NotEq, Enums.Precedences.Equals},
                {Token.Lt, Enums.Precedences.Lessgreater},
                {Token.Gt, Enums.Precedences.Lessgreater},
                {Token.Plus, Enums.Precedences.Sum},
                {Token.Minus, Enums.Precedences.Sum},
                {Token.Slash, Enums.Precedences.Product},
                {Token.Asterisk, Enums.Precedences.Product},
                {Token.Lparen, Enums.Precedences.Call},
                {Token.Lbracket, Enums.Precedences.Index}
            };

        private void SetPrefixFns() =>
            PrefixParseFns = new Dictionary<string, PrefixParseFn>
            {
                {Token.Ident, ParseIdentifier},
                {Token.Int, ParseIntegerLiteral},
                {Token.Float, ParseFloatLiteral},
                {Token.Bang, ParsePrefixExpression},
                {Token.Minus, ParsePrefixExpression},
                {Token.True, ParseBoolean},
                {Token.False, ParseBoolean},
                {Token.Lparen, ParseGroupedExpression},
                {Token.If, ParseIfExpression},
                {Token.Function, ParseFunctionLiteral},
                {Token.String, ParseStringLiteral},
                {Token.Lbracket, ParseArrayLiteral},
                {Token.Lbrace, ParseHashLiteral}
            };

        private IExpression ParseFloatLiteral()
        {
            var lit = new FloatLiteral {Token = CurrentToken};
            var success = double.TryParse(CurrentToken.Literal, out var value);
            if (!success)
            {
                Errors.Add($"could not parse {CurrentToken.Literal} into float64");
                return null;
            }

            lit.Value = value;
            return lit;
        }

        private IExpression ParseHashLiteral()
        {
            var hash = new HashLiteral {Token = CurrentToken, Pairs = new Dictionary<IExpression, IExpression>()};

            while (!PeekTokenIs(Token.Rbrace))
            {
                NextToken();
                var key = ParseExpression(Enums.Precedences.Lowest);

                if (!ExpectPeek(Token.Colon)) return null;

                NextToken();

                var value = ParseExpression(Enums.Precedences.Lowest);

                hash.Pairs.Add(key, value);

                if (!PeekTokenIs(Token.Rbrace) && !ExpectPeek(Token.Comma)) return null;
            }

            return !ExpectPeek(Token.Rbrace) ? null : hash;
        }

        private void SetInfixFns() =>
            InfixParseFns = new Dictionary<string, InfixParseFn>
            {
                {Token.Plus, ParseInfixExpression},
                {Token.Minus, ParseInfixExpression},
                {Token.Slash, ParseInfixExpression},
                {Token.Asterisk, ParseInfixExpression},
                {Token.Eq, ParseInfixExpression},
                {Token.NotEq, ParseInfixExpression},
                {Token.Lt, ParseInfixExpression},
                {Token.Gt, ParseInfixExpression},
                {Token.Lparen, ParseCallExpression},
                {Token.Lbracket, ParseIndexExpression}
            };

        private IExpression ParseArrayLiteral()
        {
            var array = new ArrayLiteral {Token = CurrentToken, Elements = ParseExpressionList(Token.Rbracket)};
            return array;
        }

        private List<IExpression> ParseExpressionList(string end)
        {
            var list = new List<IExpression>();

            if (PeekTokenIs(end))
            {
                NextToken();
                return list;
            }

            NextToken();
            list.Add(ParseExpression(Enums.Precedences.Lowest));

            while (PeekTokenIs(Token.Comma))
            {
                NextToken();
                NextToken();
                list.Add(ParseExpression(Enums.Precedences.Lowest));
            }

            return !ExpectPeek(end) ? null : list;
        }

        private IExpression ParseStringLiteral() =>
            new StringLiteral {Token = CurrentToken, Value = CurrentToken.Literal};

        private IExpression ParseFunctionLiteral()
        {
            var lit = new FunctionLiteral {Token = CurrentToken};
            if (!ExpectPeek(Token.Lparen)) return null;
            lit.Parameters = ParseFunctionParameters();
            if (!ExpectPeek(Token.Lbrace)) return null;
            lit.Body = ParseBlockStatement();
            return lit;
        }

        private List<Identifier> ParseFunctionParameters()
        {
            var identifiers = new List<Identifier>();

            if (PeekTokenIs(Token.Rparen))
            {
                NextToken();
                return identifiers;
            }

            NextToken();

            var ident = new Identifier {Token = CurrentToken, Value = CurrentToken.Literal};
            identifiers.Add(ident);

            while (PeekTokenIs(Token.Comma))
            {
                NextToken();
                NextToken();
                ident = new Identifier {Token = CurrentToken, Value = CurrentToken.Literal};
                identifiers.Add(ident);
            }

            return !ExpectPeek(Token.Rparen) ? null : identifiers;
        }

        private IExpression ParseIfExpression()
        {
            var expression = new IfExpression {Token = CurrentToken};
            if (!ExpectPeek(Token.Lparen)) return null;
            NextToken();
            expression.Condition = ParseExpression(Enums.Precedences.Lowest);
            if (!ExpectPeek(Token.Rparen)) return null;
            if (!ExpectPeek(Token.Lbrace)) return null;
            expression.Consequence = ParseBlockStatement();

            if (!PeekTokenIs(Token.Else)) return expression;
            NextToken();
            if (!ExpectPeek(Token.Lbrace)) return null;

            expression.Alternative = ParseBlockStatement();

            return expression;
        }

        private BlockStatement ParseBlockStatement()
        {
            var block = new BlockStatement {Token = CurrentToken, Statements = new List<IStatement>()};
            NextToken();
            while (!CurTokenIs(Token.Rbrace) && !CurTokenIs(Token.Eof))
            {
                var stmt = ParseStatement();
                if (!(stmt is null)) block.Statements.Add(stmt);
                NextToken();
            }

            return block;
        }

        private IExpression ParseGroupedExpression()
        {
            NextToken();
            var exp = ParseExpression(Enums.Precedences.Lowest);

            return !ExpectPeek(Token.Rparen) ? null : exp;
        }

        private IExpression ParseBoolean() =>
            new BooleanLiteral
                {Token = CurrentToken, Value = CurTokenIs(Token.True)};


        private IExpression ParseIndexExpression(IExpression v)
        {
            var exp = new IndexExpression {Token = CurrentToken, Left = v};
            NextToken();

            exp.Index = ParseExpression(Enums.Precedences.Lowest);

            return !ExpectPeek(Token.Rbracket) ? null : exp;
        }

        private IExpression ParseCallExpression(IExpression v) =>
            new CallExpression
                {Token = CurrentToken, Function = v, Arguments = ParseExpressionList(Token.Rparen)};

        private IExpression ParsePrefixExpression()
        {
            var expression = new PrefixExpression
            {
                Token = CurrentToken,
                Operator = CurrentToken.Literal
            };
            NextToken();
            expression.Right = ParseExpression(Enums.Precedences.Prefix);
            return expression;
        }

        private IExpression ParseIntegerLiteral()
        {
            var lit = new IntegerLiteral {Token = CurrentToken};
            var success = long.TryParse(CurrentToken.Literal, out var value);
            if (!success)
            {
                Errors.Add($"could not parse {CurrentToken.Literal} into int64");
                return null;
            }

            lit.Value = value;
            return lit;
        }

        private IExpression ParseIdentifier() =>
            new Identifier
                {Token = CurrentToken, Value = CurrentToken.Literal};


        private void NoPrefixParseFnError(string t) => Errors.Add($"no prefix parse function found for {t}");

        public Code ParseCode()
        {
            var program = new Code {Statements = new List<IStatement>()};
            while (CurrentToken.Type != Token.Eof)
            {
                var stmt = ParseStatement();
                if (!(stmt is null)) program.Statements.Add(stmt);

                NextToken();
            }

            return program;
        }

        private IStatement ParseStatement() =>
            CurrentToken.Type switch
            {
                Token.Let => ParseLetStatement(),
                Token.Return => ParseReturnStatement(),
                _ => ParseExpressionStatement()
            };

        private ExpressionStatement ParseExpressionStatement()
        {
            var stmt = new ExpressionStatement
            {
                Token = CurrentToken,
                Expression = ParseExpression(Enums.Precedences.Lowest)
            };
            if (PeekTokenIs(Token.Semicolon)) NextToken();
            return stmt;
        }

        private IExpression ParseExpression(Precedences precedence)
        {
            var valueExists = PrefixParseFns.TryGetValue(CurrentToken.Type, out var prefix);
            if (!valueExists)
            {
                NoPrefixParseFnError(CurrentToken.Type);
                return null;
            }

            var leftExp = prefix();
            while (!PeekTokenIs(Token.Semicolon) && precedence < PeekPrecedence())
            {
                var infixExists = InfixParseFns.TryGetValue(PeekToken.Type, out var infix);
                if (!infixExists) return leftExp;
                NextToken();
                leftExp = infix(leftExp);
            }

            return leftExp;
        }

        private Precedences PeekPrecedence() =>
            Precedences.TryGetValue(PeekToken.Type, out var precedence)
                ? precedence
                : Enums.Precedences.Lowest;

        private Precedences CurrentPrecedence() =>
            Precedences.TryGetValue(CurrentToken.Type, out var precedence)
                ? precedence
                : Enums.Precedences.Lowest;

        private IExpression ParseInfixExpression(IExpression left)
        {
            var expression = new InfixExpression
                {Token = CurrentToken, Operator = CurrentToken.Literal, Left = left};

            var precedence = CurrentPrecedence();
            NextToken();
            expression.Right = ParseExpression(precedence);

            return expression;
        }

        private ReturnStatement ParseReturnStatement()
        {
            var stmt = new ReturnStatement {Token = CurrentToken};
            NextToken();
            stmt.ReturnValue = ParseExpression(Enums.Precedences.Lowest);

            if (PeekTokenIs(Token.Semicolon)) NextToken();

            return stmt;
        }

        private LetStatement ParseLetStatement()
        {
            var stmt = new LetStatement {Token = CurrentToken};
            if (!ExpectPeek(Token.Ident)) return null;


            stmt.Name = new Identifier {Token = CurrentToken, Value = CurrentToken.Literal};
            if (!ExpectPeek(Token.Assign)) return null;

            NextToken();
            stmt.Value = ParseExpression(Enums.Precedences.Lowest);

            if (PeekTokenIs(Token.Semicolon)) NextToken();

            return stmt;
        }

        private bool CurTokenIs(string t) => CurrentToken.Type == t;

        private bool ExpectPeek(string t)
        {
            if (PeekTokenIs(t))
            {
                NextToken();
                return true;
            }

            PeekError(t);
            return false;
        }

        private bool PeekTokenIs(string t) => PeekToken.Type == t;

        private void NextToken()
        {
            CurrentToken = PeekToken;
            PeekToken = Lexer.NextToken();
        }

        private void PeekError(string t) =>
            Errors.Add($"expected next token to be '{t}', got '{PeekToken.Type}' instead");
    }
}