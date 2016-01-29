// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

//
// Parse Condition predicate strings from binary rewriter model files. These strings evaluate to true or false
// and have similar semantics to C# conditional compilation (they utilize a different syntax to avoid using
// '&' inside XML strings). The terminals of this mini-grammar are C# conditional constants (fed to the
// rewriter using the /define switch with a semi-colon separated list of constants, the same list that is fed
// to C# compilation).
//
// The intent is to allow portions of the model file to be conditionally included based on the same build
// feature switches (such as FEATURE_COMINTEROP) used elsewhere.
//
// An example of usage inside a model file would be:
//  <Type Status="ImplRoot" Name="System.__ComObject" Condition="FEATURE_COMINTEROP and FEATURE_CORESYSTEM" />
//
// The informal syntax of the expressions allowed is:
//  expr    ::= <subexpr> ['and' | 'or' <subexpr>...]
//  subexpr ::= <symbol> | '(' <expr> ')' | 'not' <subexpr>
//
// Where <symbol> is a case-insensitive constant name (e.g. 'FEATURE_CORECLR').
//
// General usage is to create an instance of the ConditionParser class, providing a list of the constants that
// are 'defined' (have the value 'true'). Then the Parse method can be called multiple times with a string
// containing the condition to be evaluated. Any parsing errors are signalled by throwing a
// ConditionParserError exception.
//

// Indicates a parsing error. The exception message will contain a brief description of the problem and the
// full text of the condition being evaluated.
internal class ConditionParserError : Exception
{
    public ConditionParserError(ConditionParser parser, string message)
        : base(String.Format("{0}: '{1}'", message, parser.ToString()))
    {
    }
}

// Class encapsulating the parsing and evaluation of a condition string.
internal class ConditionParser
{
    public ConditionParser(IEnumerable<string> symbols)
    {
        // Store all the defined constants in a hash table. The values don't matter to us, just use int and
        // set them all to 1.
        _symbols = new Dictionary<string, int>();
        foreach (string symbol in symbols)
            _symbols.Add(symbol.ToUpper(), 1);
    }

    // Parse the given condition and evaluate it against the set of defined constants provided at construction
    // time.
    public bool Parse(string condition)
    {
        // Remember the input string.
        _currString = condition;

        // Convert the string into a stream of higher level tokens.
        Tokenize();

        // Parse the token stream and evaluate the result.
        return ParseExpr();
    }

    public override string ToString()
    {
        return _currString;
    }

    // Parse a top-level expression:
    //  expr    ::= <subexpr> ['and' | 'or' <subexpr>...]
    private bool ParseExpr()
    {
        // We always parse and evaluate at least one sub-expression. Do that now and then accumulate the
        // result as we parse further expressions into 'leftExpr'.
        bool leftExpr = ParseSubExpr();
        bool rightExpr;

        while (true)
        {
            Token token = NextToken();
            switch (token.Type)
            {
                case TokenType.EndOfString:
                case TokenType.CloseParen:
                    // This expression terminates at the end of the token stream or a closing parenthesis (because
                    // we were recursively evaluating a bracketed sub-expression).
                    return leftExpr;

                case TokenType.Or:
                    // Handle 'Or' expression.
                    rightExpr = ParseSubExpr();
                    leftExpr = leftExpr || rightExpr;
                    break;

                case TokenType.And:
                    // Handle 'And' expression.
                    rightExpr = ParseSubExpr();
                    leftExpr = leftExpr && rightExpr;
                    break;

                default:
                    throw new ConditionParserError(this, String.Format("Unexpected token: '{0}'", token));
            }
        }
    }

    // Parse lower-level subexpressions:
    //  subexpr ::= <symbol> | '(' <expr> ')' | 'not' <subexpr>
    private bool ParseSubExpr()
    {
        Token token = NextToken();
        switch (token.Type)
        {
            case TokenType.Symbol:
                // A symbol represents a constant that is true if defined (part of the set given to us at
                // construction time) or false otherwise.
                return _symbols.ContainsKey(token.Symbol);

            case TokenType.OpenParen:
                // Handle a bracketed sub-expression.
                bool expr = ParseExpr();
                return expr;

            case TokenType.Not:
                // Handle 'Not' expression.
                return !ParseSubExpr();

            default:
                throw new ConditionParserError(this, String.Format("Unexpected token: '{0}'", token));
        }
    }

    // Get next token from the stream and advance the position in the stream.
    private Token NextToken()
    {
        // Once we've reached the end of the tokens return the last token (EndOfString) for all further
        // invocations.
        if (_currToken >= _tokens.Count)
            return _tokens[_tokens.Count - 1];

        return _tokens[_currToken++];
    }

    // Convert the input string into a stream of tokens (i.e. lexing).
    private void Tokenize()
    {
        _tokens = new List<Token>();
        _currToken = 0;

        int currIndex = 0;

        while (currIndex < _currString.Length)
        {
            // Get the next character.
            char nextCh = _currString[currIndex++];

            switch (nextCh)
            {
                case ' ':
                case '\t':
                    // Skip whitespace.
                    break;

                case '(':
                    _tokens.Add(new Token(TokenType.OpenParen));
                    break;

                case ')':
                    _tokens.Add(new Token(TokenType.CloseParen));
                    break;

                default:
                    // Assume anything else is a symbol name or reserved keyword. Slurp all the following
                    // characters that fit into the symbol name domain ('a' - 'z', 'A' - 'Z', '0' - '9' and '_')
                    // into a string builder.
                    StringBuilder symbolBuilder = new StringBuilder();
                    while ((nextCh >= 'a' && nextCh <= 'z') ||
                           (nextCh >= 'A' && nextCh <= 'Z') ||
                           (nextCh >= '0' && nextCh <= '9') ||
                           (nextCh == '_'))
                    {
                        symbolBuilder.Append(Char.ToUpper(nextCh));
                        if (currIndex == _currString.Length)
                            break;

                        nextCh = _currString[currIndex++];
                    }

                    // If we didn't collect any characters for a symbol name then it means we had an illegal
                    // character in the input.
                    string symbol = symbolBuilder.ToString();
                    if (symbol.Length == 0)
                        throw new ConditionParserError(this, String.Format("Illegal character: '{0}'", nextCh));

                    // We walked one character too far (except in the case where we found the end of the string)
                    // so back up one character for the next loop iteration.
                    if (currIndex < _currString.Length)
                        currIndex--;

                    // Check for keywords ('and', 'or' or 'not') as opposed to symbol names.
                    if (symbol.ToLower() == "and")
                        _tokens.Add(new Token(TokenType.And));
                    else if (symbol.ToLower() == "or")
                        _tokens.Add(new Token(TokenType.Or));
                    else if (symbol.ToLower() == "not")
                        _tokens.Add(new Token(TokenType.Not));
                    else
                        _tokens.Add(new Token(symbol));

                    break;
            }
        }

        _tokens.Add(new Token(TokenType.EndOfString));
    }

    // Types of token we expect in the input string.
    private enum TokenType
    {
        EndOfString,    // Pseudo token appended to the end of the string to make parsing easier.
        Symbol,         // String name (e.g. "FEATURE_CORECLR").
        And,            // "and"
        Or,             // "or"
        Not,            // "not"
        OpenParen,      // "("
        CloseParen      // ")"
    }

    // A higher level representation of a consecutive group of characters in the input string. Consists of a
    // type (see above) and in the case of a Symbol token a symbol name as well.
    private class Token
    {
        public Token(TokenType type)
        {
            Type = type;
            Symbol = null;
        }

        public Token(string symbol)
        {
            Type = TokenType.Symbol;
            Symbol = symbol;
        }

        public override string ToString()
        {
            if (Type == TokenType.Symbol)
                return String.Format("[Symbol:{0}]", Symbol);
            return String.Format("[{0}]", Type.ToString());
        }

        public TokenType Type { get; set; }
        public string Symbol { get; set; }
    }

    private Dictionary<string, int> _symbols;      // Set of constants which are considered 'defined'
    private string _currString;   // Input string from a model.xml Condition attribute
    private List<Token> _tokens;       // List of tokens lexed from the string above by Tokenize
    private int _currToken;    // Index of the token currently being parsed
}
