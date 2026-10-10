using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MaxyGames.UNode.SyntaxHighlighter {
	internal class ColorizerSyntaxWalker : CSharpSyntaxWalker {
		private SemanticModel model;
		private Action<TokenClass, string> writeDelegate;
		private Func<SyntaxToken, bool, bool> tokenHandler;
		public ColorizerSyntaxWalker() : base(SyntaxWalkerDepth.Token) {

		}

		public void DoVisit(SyntaxNode token, SemanticModel model, Action<TokenClass, string> writeDelegate, Func<SyntaxToken, bool, bool> tokenHandler) {
			this.model = model;
			this.writeDelegate = writeDelegate;
			this.tokenHandler = tokenHandler;
			Visit(token);
		}

		public override void VisitToken(SyntaxToken token) {
			base.VisitLeadingTrivia(token);
			var isProcessed = false;
			if(tokenHandler != null && tokenHandler(token, false)) {
				isProcessed = true;
				base.VisitTrailingTrivia(token);
				return;
			}
			if(token.IsKeyword()) {
				writeDelegate(TokenClass.Keyword, token.ValueText);
				isProcessed = true;
			} else {
				switch(token.Kind()) {
					case SyntaxKind.StringLiteralToken:
					case SyntaxKind.InterpolatedStringTextToken:
						writeDelegate(TokenClass.String, token.Text);
						isProcessed = true;
						break;
					case SyntaxKind.CharacterLiteralToken:
						writeDelegate(TokenClass.Char, token.Text);
						isProcessed = true;
						break;
					case SyntaxKind.NumericLiteralToken:
						writeDelegate(TokenClass.Number, token.Text);
						isProcessed = true;
						break;
					case SyntaxKind.IdentifierToken:
						if(model == null) {
							HandleSpecialCaseIdentifiers(token);
							base.VisitTrailingTrivia(token);
							return;
						}
						if(token.Parent is SimpleNameSyntax) {
							var name = (SimpleNameSyntax)token.Parent;
							var symbolInfo = model.GetSymbolInfo(name);
							if(symbolInfo.Symbol != null && symbolInfo.Symbol.Kind != SymbolKind.ErrorType) {
								switch(symbolInfo.Symbol.Kind) {
									case SymbolKind.Event:
										writeDelegate(TokenClass.Field, token.ValueText);
										isProcessed = true;
										break;
									case SymbolKind.Method:
										writeDelegate(TokenClass.Method, token.ValueText);
										isProcessed = true;
										break;
									case SymbolKind.NamedType:
										if(symbolInfo.Symbol is ITypeSymbol typeSymbol) {
											if(typeSymbol.TypeKind == TypeKind.Class) {
												writeDelegate(TokenClass.Class, token.ValueText);
											}
											else if(typeSymbol.TypeKind == TypeKind.Enum) {
												writeDelegate(TokenClass.Enum, token.ValueText);
											}
											else if(typeSymbol.TypeKind == TypeKind.Interface) {
												writeDelegate(TokenClass.Interface, token.ValueText);
											}
											else if(typeSymbol.TypeKind == TypeKind.Struct) {
												writeDelegate(TokenClass.Struct, token.ValueText);
											}
											else {
												writeDelegate(TokenClass.Type, token.ValueText);
											}
											isProcessed = true;
											break;
										}
										writeDelegate(TokenClass.Type, token.ValueText);
										isProcessed = true;
										break;
									case SymbolKind.Parameter:
										writeDelegate(TokenClass.Parameter, token.ValueText);
										isProcessed = true;
										break;
									case SymbolKind.Field:
										writeDelegate(TokenClass.Field, token.ValueText);
										isProcessed = true;
										break;
									case SymbolKind.Local:
										writeDelegate(TokenClass.Local, token.ValueText);
										isProcessed = true;
										break;
									case SymbolKind.Property:
										writeDelegate(TokenClass.Property, token.ValueText);
										isProcessed = true;
										break;
									case SymbolKind.Namespace:
										writeDelegate(TokenClass.Namespace, token.ValueText);
										isProcessed = true;
										break;
									default:
										break;
								}
							}
						} else if(token.Parent is TypeDeclarationSyntax) {
							var name = (TypeDeclarationSyntax)token.Parent;
							var symbol = model.GetDeclaredSymbol(name);
							if(symbol != null && symbol.Kind != SymbolKind.ErrorType) {
								switch(symbol.Kind) {
									case SymbolKind.NamedType:
										if(symbol is ITypeSymbol typeSymbol) {
											if(typeSymbol.TypeKind == TypeKind.Class) {
												writeDelegate(TokenClass.Class, token.ValueText);
											}
											else if(typeSymbol.TypeKind == TypeKind.Enum) {
												writeDelegate(TokenClass.Enum, token.ValueText);
											}
											else if(typeSymbol.TypeKind == TypeKind.Interface) {
												writeDelegate(TokenClass.Interface, token.ValueText);
											}
											else if(typeSymbol.TypeKind == TypeKind.Struct) {
												writeDelegate(TokenClass.Struct, token.ValueText);
											}
											else {
												writeDelegate(TokenClass.Type, token.ValueText);
											}
											isProcessed = true;
											break;
										}
										writeDelegate(TokenClass.Type, token.ValueText);
										isProcessed = true;
										break;
								}
							}
						}
						break;
				}
			}
			if(!isProcessed) {
				if(tokenHandler != null && tokenHandler(token, true)) {
					isProcessed = true;
				}
			}
			if(!isProcessed)
				HandleSpecialCaseIdentifiers(token);
			base.VisitTrailingTrivia(token);
		}

		private void HandleSpecialCaseIdentifiers(SyntaxToken token) {
			switch(token.Kind()) {
				case SyntaxKind.IdentifierToken:
					try {
						if ((token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.Parameter)
						  || (token.Parent.Kind() == SyntaxKind.EnumDeclaration)
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.Attribute)
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.CatchDeclaration)
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.ObjectCreationExpression)
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.ForEachStatement && !(token.GetNextToken().Kind() == SyntaxKind.CloseParenToken))
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Parent.Kind() == SyntaxKind.CaseSwitchLabel && !(token.GetPreviousToken().Kind() == SyntaxKind.DotToken))
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.MethodDeclaration)
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.CastExpression)
						  //e.g. "private static readonly HashSet patternHashSet = new HashSet();" the first HashSet in this case
						  || (token.Parent.Kind() == SyntaxKind.GenericName && token.Parent.Parent.Kind() == SyntaxKind.VariableDeclaration)
						  //e.g. "private static readonly HashSet patternHashSet = new HashSet();" the second HashSet in this case
						  || (token.Parent.Kind() == SyntaxKind.GenericName && token.Parent.Parent.Kind() == SyntaxKind.ObjectCreationExpression)
						  //e.g. "public sealed class BuilderRouteHandler : IRouteHandler" IRouteHandler in this case
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.BaseList)
						  //e.g. "Type baseBuilderType = typeof(BaseBuilder);" BaseBuilder in this case
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Parent.Parent.Kind() == SyntaxKind.TypeOfExpression)
						  // e.g. "private DbProviderFactory dbProviderFactory;" OR "DbConnection connection = dbProviderFactory.CreateConnection();"
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.VariableDeclaration)
						  // e.g. "DbTypes = new Dictionary();" DbType in this case
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.TypeArgumentList)
						  // e.g. "DbTypes.Add("int", DbType.Int32);" DbType in this case
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.SimpleMemberAccessExpression && token.Parent.Parent.Parent.Kind() == SyntaxKind.Argument && !(token.GetPreviousToken().Kind() == SyntaxKind.DotToken || Char.IsLower(token.ValueText[0])))
						  // e.g. "schemaCommand.CommandType = CommandType.Text;" CommandType in this case
						  || (token.Parent.Kind() == SyntaxKind.IdentifierName && token.Parent.Parent.Kind() == SyntaxKind.SimpleMemberAccessExpression && !(token.GetPreviousToken().Kind() == SyntaxKind.DotToken || Char.IsLower(token.ValueText[0])))
						  ) {
							writeDelegate(TokenClass.Type, token.ToString());
						} else {
							if (token.ValueText == "HashSet") {

							}
							writeDelegate(TokenClass.None, token.ToString());
						}
					} catch {
						goto default;
					}
					break;
				default:
					writeDelegate(TokenClass.None, token.ToString());
					break;
			}
		}

		public override void VisitTrivia(SyntaxTrivia trivia) {
			switch(trivia.Kind()) {
				case SyntaxKind.MultiLineCommentTrivia:
				case SyntaxKind.SingleLineCommentTrivia:
					writeDelegate(TokenClass.Comment, trivia.ToString());
					break;
				case SyntaxKind.DisabledTextTrivia:
					writeDelegate(TokenClass.DisabledText, trivia.ToString());
					break;
				case SyntaxKind.DocumentationCommentExteriorTrivia:
				case SyntaxKind.EndOfDocumentationCommentToken:
				case SyntaxKind.MultiLineDocumentationCommentTrivia:
				case SyntaxKind.SingleLineDocumentationCommentTrivia:
					writeDelegate(TokenClass.Comment, trivia.ToFullString());
					break;
				case SyntaxKind.RegionDirectiveTrivia:
				case SyntaxKind.EndRegionDirectiveTrivia:
					writeDelegate(TokenClass.Region, trivia.ToString().AddLineInEnd());
					break;
				case SyntaxKind.IfDirectiveTrivia:
				case SyntaxKind.EndIfDirectiveTrivia:
				case SyntaxKind.ElseDirectiveTrivia:
				case SyntaxKind.ElifDirectiveTrivia:
					writeDelegate(TokenClass.None, trivia.ToFullString());
					break;
				case SyntaxKind.DefineDirectiveTrivia:
				case SyntaxKind.PragmaWarningDirectiveTrivia:
				case SyntaxKind.PragmaChecksumDirectiveTrivia:
					writeDelegate(TokenClass.None, trivia.ToFullString());
					break;
				default:
					writeDelegate(TokenClass.None, trivia.ToString());
					break;
			}
			base.VisitTrivia(trivia);
		}
	}
}