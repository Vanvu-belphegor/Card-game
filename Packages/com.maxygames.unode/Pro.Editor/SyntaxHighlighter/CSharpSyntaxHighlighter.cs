using System;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using MaxyGames.UNode.Editors;
using Microsoft.CodeAnalysis.Text;

namespace MaxyGames.UNode.SyntaxHighlighter {
	public static class CSharpSyntaxHighlighter {
		private static Assembly[] assemblies;
		private static List<MetadataReference> metadataReferences;

		public class Palette {
			public string Keyword;
			public string Type;
			public string Enum;
			public string EnumMember;
			public string Interface;
			public string Struct;
			public string Class;
			public string Method;
			public string Property;
			public string Field;
			public string Parameter;
			public string Local;
			public string String;
			public string Number;
			public string Char;
			public string Boolean;
			public string Null;
			public string Comment;
			public string Preprocessor;
			public string Punctuation;
			public string Region;
			public string Operator;
			public string Namespace;
			public string Attribute;
			public string XmlDocText;

			internal string GetColor(TokenClass cls) {
				switch(cls) {
					case TokenClass.Keyword: return Keyword;
					case TokenClass.Type: return Type;
					case TokenClass.Enum: return Enum;
					case TokenClass.Interface: return Interface;
					case TokenClass.Struct: return Struct;
					case TokenClass.Class: return Class;
					case TokenClass.Method: return Method;
					case TokenClass.Property: return Property;
					case TokenClass.Field: return Field;
					case TokenClass.Parameter: return Parameter;
					case TokenClass.Local: return Local;
					case TokenClass.EnumMember: return EnumMember;
					case TokenClass.String: return String;
					case TokenClass.Number: return Number;
					case TokenClass.Char: return Char;
					case TokenClass.Boolean: return Boolean;
					case TokenClass.Null: return Null;
					case TokenClass.Comment: return Comment;
					case TokenClass.Preprocessor: return Preprocessor;
					case TokenClass.Punctuation: return Punctuation;
					case TokenClass.Operator: return Operator;
					case TokenClass.Namespace: return Namespace;
					case TokenClass.Attribute: return Attribute;
					case TokenClass.Region: return Region;
					case TokenClass.None: return string.Empty;
					default:
						return string.Empty;
				}
			}

			public Palette(
				string keyword,
				string type,
				string @enum,
				string enumMember,
				string @interface,
				string @struct,
				string @class,
				string method,
				string property,
				string field,
				string parameter,
				string local,
				string str,
				string number,
				string ch,
				string boolean,
				string @null,
				string comment,
				string preprocessor,
				string punctuation,
				string op,
				string ns,
				string attribute,
				string region,
				string xmlDocText
			) {
				Keyword = keyword;
				Type = type;
				Enum = @enum;
				EnumMember = enumMember;
				Interface = @interface;
				Struct = @struct;
				Class = @class;
				Method = method;
				Property = property;
				Field = field;
				Parameter = parameter;
				Local = local;
				String = str;
				Number = number;
				Char = ch;
				Boolean = boolean;
				Null = @null;
				Comment = comment;
				Preprocessor = preprocessor;
				Punctuation = punctuation;
				Operator = op;
				Namespace = ns;
				Attribute = attribute;
				Region = region;
				XmlDocText = xmlDocText;
			}

			public static readonly Palette DarkTheme = new Palette(
				keyword: "#569CD6",
				type: "#4EC9B0",
				@enum: "#B8D7A3",
				enumMember: "#B8D7A3",
				@interface: "#B8D7A3",
				@struct: "#4EC9B0",
				@class: "#4EC9B0",
				method: "#DCDCAA",
				property: "#9CDCFE",
				field: "#9CDCFE",
				parameter: "#9CDCFE",
				local: "#9CDCFE",
				str: "#CE9178",
				number: "#B5CEA8",
				ch: "#CE9178",
				boolean: "#569CD6",
				@null: "#569CD6",
				comment: "#6A9955",
				preprocessor: "#808080",
				punctuation: "#D4D4D4",
				op: "#D4D4D4",
				ns: "#B8D7A3",
				attribute: "#C586C0",
				region: "#808080",
				xmlDocText: "#6A9955"
			);

			public static readonly Palette LightTheme = new Palette(
				keyword: "#0000FF",
				type: "#2B91AF",
				@enum: "#2B91AF",
				enumMember: "#2B91AF",
				@interface: "#2B91AF",
				@struct: "#2B91AF",
				@class: "#2B91AF",
				method: "#795E26",
				property: "#001080",
				field: "#001080",
				parameter: "#001080",
				local: "#001080",
				str: "#A31515",
				number: "#098658",
				ch: "#A31515",
				boolean: "#0000FF",
				@null: "#0000FF",
				comment: "#008000",
				preprocessor: "#808080",
				punctuation: "#000000",
				op: "#000000",
				ns: "#2B91AF",
				attribute: "#800080",
				region: "#808080",
				xmlDocText: "#008000"
			);
		}

		private static List<MetadataReference> GetMetadataReferences() {
			if(metadataReferences != null) {
				return metadataReferences;
			}
			List<MetadataReference> references = new List<MetadataReference>();
			if(assemblies == null) {
				assemblies = RoslynUtility.Data.GetAssemblies();
			}
			foreach(var assembly in assemblies) {
				try {
					if(assembly != null && !string.IsNullOrEmpty(assembly.Location)) {
						//Skip AssetStoreTools assembly
						if(assembly.GetName().Name.StartsWith("AssetStoreTools", StringComparison.Ordinal))
							continue;
						references.Add(MetadataReference.CreateFromFile(assembly.Location));
						if(uNodeThreadUtility.IsInMainThread == false) {
							Thread.Sleep(1);
						}
					}
				} catch { continue; }
			}
			metadataReferences = references;
			return references;
		}

		public static string GetRichTextAsync(string sourceCode) {
			List<string> preprocessorSymbols = new List<string>();
			uNodeThreadUtility.Queue(() => {
				foreach(var symbol in UnityEditor.EditorUserBuildSettings.activeScriptCompilationDefines) {
					if(symbol.StartsWith("UNITY_EDITOR", StringComparison.Ordinal))
						continue;
					preprocessorSymbols.Add(symbol);
				}
			});
			uNodeThreadUtility.WaitUntilEmpty();

			var tree = CSharpSyntaxTree.ParseText(sourceCode, new CSharpParseOptions(preprocessorSymbols: preprocessorSymbols));
			var compilation = CSharpCompilation.Create("CSharpParser",
				syntaxTrees: new[] { tree }, references: GetMetadataReferences());
			var model = compilation.GetSemanticModel(tree, true);

			var root = Task.Run(async () => await tree.GetRootAsync()).Result as CompilationUnitSyntax;
			var walker = new ColorizerSyntaxWalker();
			var builder = new StringBuilder();

			Classifier syntaxClassifier = new Classifier(root);
			var palette = ColorPalette.IsDarkTheme ? Palette.DarkTheme : Palette.LightTheme;

			void Write(TokenClass tokenKind, string text) {
				switch(tokenKind) {
					case TokenClass.String:
					case TokenClass.Char:
					case TokenClass.Comment:
					case TokenClass.DisabledText:
					case TokenClass.Region:
						if(!text.Contains("\n")) {
							ColorWrap(builder, Escape(text), palette.GetColor(tokenKind));
							break;
						}
						string left = "<color=" + palette.GetColor(tokenKind) + ">";
						string right = "</color>"; {
							string[] str = text.Split('\n');
							for(int i = 0; i < str.Length; i++) {
								str[i] = left + Escape(str[i]) + right;
							}
							builder.Append(string.Join("\n", str));
						}
						break;
					case TokenClass.None:
					default:
						ColorWrap(builder, Escape(text), palette.GetColor(tokenKind));
						break;
				}
			}

			walker.DoVisit(root, model, Write, (token, isFallback) => {
				if(isFallback == false && model != null) {
					if(token.Kind() is SyntaxKind.IdentifierToken) {
						var cls = syntaxClassifier.GetClassification(token);
						if(cls == TokenClass.Attribute) {
							var color = palette.GetColor(cls);
							ColorWrap(builder, Escape(token.ValueText), color);
							return true;
						}
					}
					if(token.Kind() is not SyntaxKind.IdentifierToken && !(token.Kind() is SyntaxKind.CharacterLiteralToken or SyntaxKind.StringLiteralToken or SyntaxKind.NumericLiteralToken or SyntaxKind.InterpolatedStringText)) {
						var cls = syntaxClassifier.GetClassification(token);
						if(cls != TokenClass.None) {
							var color = palette.GetColor(cls);
							ColorWrap(builder, Escape(token.ValueText), color);
							return true;
						}
						return false;
					}
				}
				if(isFallback || model == null) {
					var cls = syntaxClassifier.GetClassification(token);
					var color = palette.GetColor(cls);
					switch(cls) {
						case TokenClass.String:
						case TokenClass.Char:
						case TokenClass.Number:
							ColorWrap(builder, Escape(token.Text), color);
							break;
						default:
							ColorWrap(builder, Escape(token.ValueText), color);
							break;
					}
					return true;
				}
				return false;
			});
			return builder.ToString();
		}

		public static string GetRichText(string sourceCode) {
			return GetRichText(sourceCode, true);
		}

		public static string GetRichText(string sourceCode, bool useSemantic) {
			SemanticModel model;
			CompilationUnitSyntax root;
			if(useSemantic) {
				List<string> preprocessorSymbols = new List<string>(16);
				foreach(var symbol in UnityEditor.EditorUserBuildSettings.activeScriptCompilationDefines) {
					if(symbol.StartsWith("UNITY_EDITOR", StringComparison.Ordinal))
						continue;
					preprocessorSymbols.Add(symbol);
				}

				var tree = CSharpSyntaxTree.ParseText(sourceCode, new CSharpParseOptions(preprocessorSymbols: preprocessorSymbols));
				var compilation = CSharpCompilation.Create("CSharpParser",
					syntaxTrees: new[] { tree }, references: GetMetadataReferences());
				model = compilation.GetSemanticModel(tree, true);

				root = tree.GetRoot() as CompilationUnitSyntax;
				//root = RoslynUtility.GetSyntaxTree(sourceCode, out model);
			}
			else {
				root = RoslynUtility.GetSyntaxTree(sourceCode);
				model = null;
			}

			var walker = new ColorizerSyntaxWalker();

			Classifier syntaxClassifier = new Classifier(root);
			var palette = ColorPalette.IsDarkTheme ? Palette.DarkTheme : Palette.LightTheme;

			var builder = new StringBuilder();
			void Write(TokenClass tokenKind, string text) {
				switch(tokenKind) {
					case TokenClass.String:
					case TokenClass.Char:
					case TokenClass.Comment:
					case TokenClass.DisabledText:
					case TokenClass.Region:
						if(!text.Contains("\n")) {
							ColorWrap(builder, Escape(text), palette.GetColor(tokenKind));
							break;
						}
						string color = palette.GetColor(tokenKind);
						string left = "<color=" + palette.GetColor(tokenKind) + ">";
						string right = "</color>"; 
						if(string.IsNullOrEmpty(color)) {
							left = string.Empty;
							right = string.Empty;
						} 
						{
							string[] str = text.Split('\n');
							for(int i = 0; i < str.Length; i++) {
								str[i] = left + Escape(str[i]) + right;
							}
							builder.Append(string.Join("\n", str));
						}
						break;
					case TokenClass.None:
					default:
						ColorWrap(builder, Escape(text), palette.GetColor(tokenKind));
						break;
				}
			}

			walker.DoVisit(root, model, Write, (token, isFallback) => {
				if(isFallback == false && model != null) {
					if(token.Kind() is SyntaxKind.IdentifierToken) {
						var cls = syntaxClassifier.GetClassification(token);
						if(cls == TokenClass.Attribute) {
							var color = palette.GetColor(cls);
							ColorWrap(builder, Escape(token.ValueText), color);
							return true;
						}
					}
					if(token.Kind() is not SyntaxKind.IdentifierToken && !(token.Kind() is SyntaxKind.CharacterLiteralToken or SyntaxKind.StringLiteralToken or SyntaxKind.NumericLiteralToken or SyntaxKind.InterpolatedStringText)) {
						var cls = syntaxClassifier.GetClassification(token);
						if(cls != TokenClass.None) {
							var color = palette.GetColor(cls);
							ColorWrap(builder, Escape(token.ValueText), color);
							return true;
						}
						return false;
					}
				}
				if(isFallback || model == null) {
					var cls = syntaxClassifier.GetClassification(token);
					var color = palette.GetColor(cls);
					switch(cls) {
						case TokenClass.String:
						case TokenClass.Char:
						case TokenClass.Number:
							ColorWrap(builder, Escape(token.Text), color);
							break;
						default:
							ColorWrap(builder, Escape(token.ValueText), color);
							break;
					}
					return true;
				}
				return false;
			});
			return builder.ToString();
		}
		private static string Escape(string raw) {
			return raw
				.Replace("&", "\u0026")
				.Replace("<", "\u003C")
				.Replace(">", "\u003E");
		}

		private static void ColorWrap(StringBuilder sb, string text, string hexColor) {
			if(string.IsNullOrEmpty(hexColor)) {
				sb.Append(text);
			}
			else {
				sb.Append("<color=").Append(hexColor).Append(">");
				sb.Append(text);
				sb.Append("</color>");
			}
		}

		private class Classifier {
			Dictionary<TextSpan, TokenClass> kindsBySpan = new Dictionary<TextSpan, TokenClass>(64);
			HashSet<TextSpan> enumMemberSpans = new HashSet<TextSpan>();

			public Classifier(CompilationUnitSyntax syntax) {
				var namespaces = EditorReflectionUtility.GetNamespaces();
				foreach(var node in syntax.DescendantNodes()) {
					switch(node) {
						case ClassDeclarationSyntax cls:
							kindsBySpan[cls.Identifier.Span] = TokenClass.Class;
							break;
						case StructDeclarationSyntax s:
							kindsBySpan[s.Identifier.Span] = TokenClass.Struct;
							break;
						case InterfaceDeclarationSyntax i:
							kindsBySpan[i.Identifier.Span] = TokenClass.Interface;
							break;
						case EnumDeclarationSyntax e:
							kindsBySpan[e.Identifier.Span] = TokenClass.Enum;
							foreach(var member in e.Members)
								enumMemberSpans.Add(member.Identifier.Span);
							break;
						case DelegateDeclarationSyntax d:
							kindsBySpan[d.Identifier.Span] = TokenClass.Method;
							break;
						case MethodDeclarationSyntax m:
							kindsBySpan[m.Identifier.Span] = TokenClass.Method;
							break;
						case PropertyDeclarationSyntax p:
							if(p.Identifier != default)
								kindsBySpan[p.Identifier.Span] = TokenClass.Property;
							break;
						case EventDeclarationSyntax ev:
							if(ev.Identifier != default)
								kindsBySpan[ev.Identifier.Span] = TokenClass.Field;
							break;
						case VariableDeclaratorSyntax v:
							var parent = v.Parent?.Parent;
							if(parent is FieldDeclarationSyntax)
								kindsBySpan[v.Identifier.Span] = TokenClass.Field;
							else if(parent is EventFieldDeclarationSyntax)
								kindsBySpan[v.Identifier.Span] = TokenClass.Field;
							else if(parent is LocalDeclarationStatementSyntax)
								kindsBySpan[v.Identifier.Span] = TokenClass.Local;
							//else if(parent is UsingDeclarationSyntax)
							//	kindsBySpan[v.Identifier.Span] = TokenClass.Local;
							break;
						case ParameterSyntax param:
							kindsBySpan[param.Identifier.Span] = TokenClass.Parameter;
							break;
						case AttributeSyntax attr: {
							if(attr.Name is IdentifierNameSyntax idName)
								kindsBySpan[idName.Identifier.Span] = TokenClass.Attribute;
							if(attr.Name is QualifiedNameSyntax qName && qName.Right is IdentifierNameSyntax idName2)
								kindsBySpan[idName2.Identifier.Span] = TokenClass.Attribute;
							break;
						}
						case NamespaceDeclarationSyntax ns: {
							if(ns.Name is IdentifierNameSyntax id)
								kindsBySpan[id.Identifier.Span] = TokenClass.Namespace;
							break;
						}
						case FileScopedNamespaceDeclarationSyntax fns: {
							if(fns.Name is IdentifierNameSyntax id2)
								kindsBySpan[id2.Identifier.Span] = TokenClass.Namespace;
							break;
						}
						case CatchDeclarationSyntax cds: {
							if(cds.Type is IdentifierNameSyntax idName)
								kindsBySpan[idName.Identifier.Span] = TokenClass.Type;
							if(cds.Type is QualifiedNameSyntax qName && qName.Right is IdentifierNameSyntax idName2)
								kindsBySpan[idName2.Identifier.Span] = TokenClass.Type;
							break;
						}
						case IdentifierNameSyntax ins: {
							if(ins.Identifier.ValueText == "var") {
								kindsBySpan[ins.Identifier.Span] = TokenClass.Keyword;
							}
							//else if(ins.Parent is UsingDirectiveSyntax) {
							//	kindsBySpan[ins.Identifier.Span] = TokenClass.Namespace;
							//}
							else if(ins.Parent is VariableDeclarationSyntax vds) {
								var p2 = vds.Parent;
								if(p2 is LocalDeclarationStatementSyntax) {
									kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
								}
								else if(p2 is FieldDeclarationSyntax) {
									kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
								}
								else if(p2 is EventFieldDeclarationSyntax) {
									kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
								}
							}
							else if(ins.Parent is ParameterSyntax) {
								kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
							}
							else if(ins.Parent is PropertyDeclarationSyntax) {
								kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
							}
							else if(ins.Parent is MethodDeclarationSyntax mds && mds.ReturnType == ins) {
								kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
							}
							else if(ins.Parent is DelegateDeclarationSyntax dds && dds.ReturnType == ins) {
								kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
							}
							else if(ins.Parent is ClassDeclarationSyntax cds && cds.BaseList != null) {
								foreach(var baseType in cds.BaseList.Types) {
									if(baseType.Type == ins) {
										kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
										break;
									}
								}
							}
							else if(ins.Parent is StructDeclarationSyntax sds && sds.BaseList != null) {
								foreach(var baseType in sds.BaseList.Types) {
									if(baseType.Type == ins) {
										kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
										break;
									}
								}
							}
							else if(ins.Parent is InterfaceDeclarationSyntax ids && ids.BaseList != null) {
								foreach(var baseType in ids.BaseList.Types) {
									if(baseType.Type == ins) {
										kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
										break;
									}
								}
							}
							else if(ins.Parent is ObjectCreationExpressionSyntax ocs && ocs.Type == ins) {
								kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
							}
							else if(ins.Parent is CastExpressionSyntax ces && ces.Type == ins) {
								kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
							}
							else if(ins.Parent is ForEachStatementSyntax fss && fss.Type == ins) {
								kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
							}
							else if(ins.Parent is BaseListSyntax bls) {
								foreach(var baseType in bls.Types) {
									if(baseType.Type == ins) {
										kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
										break;
									}
								}
							}
							//else if(ins.Parent is QualifiedNameSyntax qns) {
							//	var left = qns.Left;
							//	if(left == ins) {
							//		UnityEngine.Debug.Log(kindsBySpan.ContainsKey(qns.Span));
							//		if(namespaces.Contains(ins.Identifier.ValueText)) {
							//			kindsBySpan[ins.Identifier.Span] = TokenClass.Namespace;
							//		}
							//		else {
							//			kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
							//		}
							//	}
							//}
							//else if(ins.Parent is MemberAccessExpressionSyntax mas) {
							//	var expr = mas.Expression;
							//	if(expr == ins) {
							//		if(namespaces.Contains(ins.Identifier.ValueText)) {
							//			kindsBySpan[ins.Identifier.Span] = TokenClass.Namespace;
							//		}
							//		else {
							//			kindsBySpan[ins.Identifier.Span] = TokenClass.Type;
							//		}
							//	}
							//}
							break;
						}
					}
				}
			}

			public TokenClass GetClassification(SyntaxToken token) {
				return ClassifyTokenSyntaxOnly(token, kindsBySpan, enumMemberSpans);
			}

			private static TokenClass ClassifyTokenSyntaxOnly(
				SyntaxToken token,
				Dictionary<TextSpan, TokenClass> kindsBySpan,
				HashSet<TextSpan> enumMemberSpans) {
				var kind = token.Kind();

				if(SyntaxFacts.IsKeywordKind(kind)) {
					if(kind == SyntaxKind.TrueKeyword || kind == SyntaxKind.FalseKeyword)
						return TokenClass.Boolean;
					if(kind == SyntaxKind.NullKeyword)
						return TokenClass.Null;
					return TokenClass.Keyword;
				}

				if(kind == SyntaxKind.StringLiteralToken || kind == SyntaxKind.InterpolatedStringTextToken)
					return TokenClass.String;
				if(kind == SyntaxKind.NumericLiteralToken)
					return TokenClass.Number;
				if(kind == SyntaxKind.CharacterLiteralToken)
					return TokenClass.Char;

				if(kind == SyntaxKind.IdentifierToken) {
					var span = token.Span;
					if(enumMemberSpans.Contains(span))
						return TokenClass.EnumMember;
					if(kindsBySpan.TryGetValue(span, out var cls))
						return cls;

					var next = token.GetNextToken(includeZeroWidth: true);
					if(next.Kind() == SyntaxKind.LessThanToken) {
						var temp = next;
						int depth = 0;
						while(temp != default) {
							if(temp.IsKind(SyntaxKind.LessThanToken)) depth++;
							else if(temp.IsKind(SyntaxKind.GreaterThanToken)) { depth--; if(depth == 0) break; }
							temp = temp.GetNextToken(includeZeroWidth: true);
						}
						next = temp.GetNextToken(includeZeroWidth: true);
					}
					if(next.Kind() == SyntaxKind.OpenParenToken)
						return TokenClass.Method;

					return TokenClass.Local;
				}

				if(SyntaxFacts.IsPunctuation(kind)) {
					if(IsOperator(kind)) return TokenClass.Operator;
					return TokenClass.Punctuation;
				}

				if(kind == SyntaxKind.RegionKeyword || kind == SyntaxKind.EndRegionKeyword)
					return TokenClass.Punctuation;

				return TokenClass.None;
			}

			private static bool IsOperator(SyntaxKind kind) {
				switch(kind) {
					case SyntaxKind.PlusToken:
					case SyntaxKind.MinusToken:
					case SyntaxKind.AsteriskToken:
					case SyntaxKind.SlashToken:
					case SyntaxKind.PercentToken:
					case SyntaxKind.AmpersandToken:
					case SyntaxKind.BarToken:
					case SyntaxKind.CaretToken:
					case SyntaxKind.ExclamationToken:
					case SyntaxKind.TildeToken:
					case SyntaxKind.EqualsEqualsToken:
					case SyntaxKind.ExclamationEqualsToken:
					case SyntaxKind.LessThanToken:
					case SyntaxKind.LessThanEqualsToken:
					case SyntaxKind.GreaterThanToken:
					case SyntaxKind.GreaterThanEqualsToken:
					case SyntaxKind.PlusPlusToken:
					case SyntaxKind.MinusMinusToken:
					case SyntaxKind.LessThanLessThanToken:
					case SyntaxKind.GreaterThanGreaterThanToken:
					case SyntaxKind.BarBarToken:
					case SyntaxKind.AmpersandAmpersandToken:
					case SyntaxKind.QuestionToken:
					case SyntaxKind.ColonToken:
					case SyntaxKind.EqualsToken:
					case SyntaxKind.PlusEqualsToken:
					case SyntaxKind.MinusEqualsToken:
					case SyntaxKind.AsteriskEqualsToken:
					case SyntaxKind.SlashEqualsToken:
					case SyntaxKind.PercentEqualsToken:
					case SyntaxKind.AmpersandEqualsToken:
					case SyntaxKind.BarEqualsToken:
					case SyntaxKind.CaretEqualsToken:
					case SyntaxKind.LessThanLessThanEqualsToken:
					case SyntaxKind.GreaterThanGreaterThanEqualsToken:
						return true;
				}
				return false;
			}
		}
	}

	internal enum TokenClass {
		None,
		Keyword,
		Type,
		Enum,
		Interface,
		Struct,
		Class,
		Method,
		Property,
		Field,
		Parameter,
		Local,
		EnumMember,
		String,
		Number,
		Char,
		Boolean,
		Null,
		Comment,
		Preprocessor,
		Punctuation,
		Operator,
		Namespace,
		Attribute,
		DisabledText,
		Region
	}
}