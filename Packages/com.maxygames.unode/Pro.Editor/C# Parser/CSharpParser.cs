using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEngine;
using MaxyGames.UNode.Nodes;
using System.IO;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Syntax = Microsoft.CodeAnalysis.SyntaxTree;

namespace MaxyGames.UNode.Editors {
	/// <summary>
	/// A Class for parsing C# into uNode using Microsoft.CodeAnalysis.
	/// </summary>
	public sealed class CSharpParser {
		#region Classes
		/// <summary>
		/// Class for save parsed data used for parsing C#
		/// </summary>
		public sealed class RootData {
			public object owner;
			public IScriptGraphType scriptGraphType => owner as IScriptGraphType;
			public IGraph graph => owner as IGraph;
			public Graph graphData => graph?.GraphData;
			public SemanticModel model;
			public MemberDeclarationSyntax syntax;
			public List<KeyValuePair<BaseFieldDeclarationSyntax, Variable>> variables = new List<KeyValuePair<BaseFieldDeclarationSyntax, Variable>>();
			public List<KeyValuePair<PropertyDeclarationSyntax, Property>> properties = new List<KeyValuePair<PropertyDeclarationSyntax, Property>>();
			public List<KeyValuePair<MethodDeclarationSyntax, Function>> methods = new List<KeyValuePair<MethodDeclarationSyntax, Function>>();
			public List<KeyValuePair<ConstructorDeclarationSyntax, Constructor>> constructors = new List<KeyValuePair<ConstructorDeclarationSyntax, Constructor>>();

			public RootData(MemberDeclarationSyntax syntax, SemanticModel model, object owner) {
				this.syntax = syntax;
				this.model = model;
				this.owner = owner;
				EditorReflectionUtility.GetListOfType<SyntaxHandles>();
			}
		}

		/// <summary>
		/// Class to store the data of parsed syntaxs.
		/// </summary>
		public class Data {
			public string name;
			public List<RootData> roots;
			public Dictionary<StatementSyntax, Node> parsedStatements;
			public Dictionary<ISymbol, Symbol> symbolMap;
			public HashSet<UPort> autoProxyPorts;

			public struct Symbol {
				public object owner;
				public Func<ParserSetting, MemberData> createReference;
				public Type type;
			}

			public IEnumerable<Syntax> references;

			public Data() {
				roots = new List<RootData>();
				parsedStatements = new Dictionary<StatementSyntax, Node>();
				symbolMap = new Dictionary<ISymbol, Symbol>();
				autoProxyPorts = new HashSet<UPort>();
			}
		}
		#endregion

		#region Variables
		public static bool option_UseBlockSystem;
		public static HashSet<string> usingNamespaces = new HashSet<string>();

		private static List<SyntaxHandles> m_syntaxHandlerList;
		/// <summary>
		/// The list of SyntaxHandles used to handle the Syntax
		/// </summary>
		public static List<SyntaxHandles> syntaxHandlerList {
			get {
				if(m_syntaxHandlerList == null) {
					m_syntaxHandlerList = EditorReflectionUtility.GetListOfType<SyntaxHandles>();
					m_syntaxHandlerList.Sort((x, y) => CompareUtility.Compare(x.order, y.order));
				}
				return m_syntaxHandlerList;
			}
		}
		/// <summary>
		/// The allowed type to create a value node.
		/// </summary>
		public static Type[] allowedValueTargetTypes = new Type[] {
			typeof(string),
			typeof(bool),
			typeof(byte),
			typeof(sbyte),
			typeof(char),
			typeof(short),
			typeof(ushort),
			typeof(uint),
			typeof(int),
			typeof(long),
			typeof(ulong),
			typeof(float),
			typeof(double),
			typeof(decimal),
			typeof(AnimationCurve),
			typeof(Rect),
			typeof(Color),
			typeof(Color32),
			typeof(Bounds),
			typeof(Gradient),
			typeof(Vector2),
			typeof(Vector2Int),
			typeof(Vector3),
			typeof(Vector3Int),
			typeof(Vector4),
			typeof(Quaternion),
			typeof(Type),
		};
		/// <summary>
		/// The parser data
		/// </summary>
		public static Data parserData;
		#endregion

		/// <summary>
		/// Start parsing a c# script.
		/// </summary>
		/// <param name="script"></param>
		/// <param name="objectName"></param>
		/// <returns></returns>
		public static ScriptGraph StartParse(string script, string objectName, bool reset = true, IEnumerable<Syntax> references = null) {
			if(script == null) {
				throw new NullReferenceException("Can't parse, Scripts is null");
			}
			if(objectName == null) {
				throw new Exception("Script name is null");
			}
			ScriptGraph scriptGraph = ScriptableObject.CreateInstance<ScriptGraph>();
			scriptGraph.name = objectName;
			try {
				StartParse(script, scriptGraph, reset, references);
			}
			catch {
				UnityEngine.Object.DestroyImmediate(scriptGraph);
				throw;
			}
			return scriptGraph;
		}

		/// <summary>
		/// Start parsing a c# script.
		/// </summary>
		/// <param name="script"></param>
		/// <param name="scriptGraph"></param>
		public static void StartParse(string script, ScriptGraph scriptGraph, bool reset = true, IEnumerable<Syntax> references = null) {
			if(scriptGraph == null) {
				throw new Exception("gameObject is null");
			}
			if(reset) {
				usingNamespaces = new HashSet<string>();
			}
			SemanticModel model;
			var root = RoslynUtility.GetSyntaxTree(script, out model, references);
			if(model == null)
				throw new System.Exception("Unable to parse the script");
			ParseSyntax(root, model, scriptGraph, references);
		}

		private static void ParseNestedDeclaration(MemberDeclarationSyntax memberSyntax, SemanticModel model, GameObject gameObject) {
			//TODO: add nested type supports
			//parserData = new Data() {
			//	name = gameObject.name,
			//};
			//Action onFinishAction = ParseMember(memberSyntax, model, gameObject);
			//foreach(var root in parserData.roots) {
			//	foreach(var pair in root.properties) {
			//		if(!pair.Value.AutoProperty) {
			//			if(pair.Value.CanGetValue()) {
			//				if(pair.Key.AccessorList != null) {
			//					var accessor = pair.Key.AccessorList.ChildNodes().First(item => item.Kind() == SyntaxKind.GetAccessorDeclaration) as AccessorDeclarationSyntax;
			//					if(accessor.Body != null) {
			//						var nodes = ParseStatement(accessor.Body, model, root.root, pair.Value.getRoot);
			//						if(nodes != null && nodes.Count > 0) {
			//							var node = nodes[0];
			//							pair.Value.getRoot.startNode = node;
			//						}
			//					} else if(accessor.ExpressionBody != null) {
			//						var body = accessor.ExpressionBody;
			//						var member = ParseExpression(body.Expression, model, root.root, pair.Value.getRoot);
			//						if(pair.Value.ReturnType() == typeof(void)) {
			//							pair.Value.getRoot.startNode = member.GetFlowNode();
			//						} else {
			//							var node = CreateComponent<NodeReturn>("return", pair.Value.getRoot.transform, pair.Value.owner);
			//							node.returnValue = member;
			//							pair.Value.getRoot.startNode = node;
			//						}
			//					}
			//				} else if(pair.Key.ExpressionBody != null) {
			//					var body = pair.Key.ExpressionBody;
			//					var member = ParseExpression(body.Expression, model, root.root, pair.Value.getRoot);
			//					if(pair.Value.ReturnType() == typeof(void)) {
			//						pair.Value.getRoot.startNode = member.GetFlowNode();
			//					} else {
			//						var node = CreateComponent<NodeReturn>("return", pair.Value.getRoot.transform, pair.Value.owner);
			//						node.returnValue = member;
			//						pair.Value.getRoot.startNode = node;
			//					}
			//				}
			//			}
			//			if(pair.Value.CanSetValue() && pair.Key.AccessorList != null) {
			//				var accessor = pair.Key.AccessorList.ChildNodes().First(item => item.Kind() == SyntaxKind.SetAccessorDeclaration) as AccessorDeclarationSyntax;
			//				if(accessor.Body != null) {
			//					var nodes = ParseStatement(accessor.Body, model, root.root, pair.Value.setRoot);
			//					if(nodes != null && nodes.Count > 0) {
			//						var node = nodes[0];
			//						pair.Value.setRoot.startNode = node;
			//					}
			//				} else if(accessor.ExpressionBody != null) {
			//					var body = accessor.ExpressionBody;
			//					var member = ParseExpression(body.Expression, model, root.root, pair.Value.setRoot);
			//					pair.Value.setRoot.startNode = member.GetFlowNode();
			//				}
			//			}
			//		}
			//	}
			//	foreach(var pair in root.constructors) {
			//		if(pair.Key.Body != null) {
			//			var nodes = ParseStatement(pair.Key.Body, model, root.root, pair.Value);
			//			if(nodes != null && nodes.Count > 0) {
			//				var node = nodes[0];
			//				pair.Value.startNode = node;
			//			}
			//		} else if(pair.Key.ExpressionBody != null) {
			//			var body = pair.Key.ExpressionBody;
			//			var member = ParseExpression(body.Expression, model, root.root, pair.Value);
			//			pair.Value.startNode = member.GetFlowNode();
			//		}
			//	}
			//	foreach(var pair in root.methods) {
			//		if(pair.Key.Body != null) {
			//			var nodes = ParseStatement(pair.Key.Body, model, root.root, pair.Value);
			//			if(nodes != null && nodes.Count > 0) {
			//				var node = nodes[0];
			//				pair.Value.startNode = node;
			//			}
			//		} else if(pair.Key.ExpressionBody != null) {
			//			var body = pair.Key.ExpressionBody;
			//			var member = ParseExpression(body.Expression, model, root.root, pair.Value);
			//			if(pair.Value.ReturnType() == typeof(void)) {
			//				pair.Value.startNode = member.GetFlowNode();
			//			} else {
			//				var node = CreateComponent<NodeReturn>("return", pair.Value.transform, pair.Value.owner);
			//				node.returnValue = member;
			//				pair.Value.startNode = node;
			//			}
			//		}
			//	}
			//	root.root.Refresh();
			//	foreach(var func in root.root.Functions) {
			//		if(func.startNode != null) {
			//			NodeEditorUtility.FitNodePositionsWithGroup(func.startNode, (connections, rect) => {
			//				NodeEditorUtility.MoveNodes(connections, new Vector2(-(rect.width / 2), -(rect.height / 2)));
			//			});
			//		}
			//	}
			//	foreach(var ctor in root.root.Constuctors) {
			//		if(ctor.startNode != null) {
			//			NodeEditorUtility.FitNodePositionsWithGroup(ctor.startNode, (connections, rect) => {
			//				NodeEditorUtility.MoveNodes(connections, new Vector2(-(rect.width / 2), -(rect.height / 2)));
			//			});
			//		}
			//	}
			//	foreach(var prop in root.root.Properties) {
			//		if(!prop.AutoProperty) {
			//			if(prop.setRoot != null && prop.setRoot.startNode != null) {
			//				NodeEditorUtility.FitNodePositionsWithGroup(prop.setRoot.startNode, (connections, rect) => {
			//					NodeEditorUtility.MoveNodes(connections, new Vector2(-(rect.width / 2), -(rect.height / 2)));
			//				});
			//			}
			//			if(prop.getRoot != null && prop.getRoot.startNode != null) {
			//				NodeEditorUtility.FitNodePositionsWithGroup(prop.getRoot.startNode, (connections, rect) => {
			//					NodeEditorUtility.MoveNodes(connections, new Vector2(-(rect.width / 2), -(rect.height / 2)));
			//				});
			//			}
			//		}
			//	}
			//}
			//var data = gameObject.GetComponent<uNodeData>();
			//if(data == null) {
			//	data = gameObject.AddComponent<uNodeData>();
			//}
			//data.generatorSettings.usingNamespace = usingNamespaces.ToArray();
			//if(onFinishAction != null) {//Start handle nested types.
			//	onFinishAction();
			//}
		}

		/// <summary>
		/// Begin parsing syntax tree
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <param name="scriptGraph"></param>
		public static void ParseSyntax(CompilationUnitSyntax syntax, SemanticModel model, ScriptGraph scriptGraph, IEnumerable<Syntax> references = null) {
			GraphUtility.SaveAllGraph();//Make sure to save all graphs before parsing.
			parserData = new Data() {
				name = scriptGraph.name,
				references = references,
			};
			//Parsing using namespace
			List<string> usings = new List<string>();
			foreach(var u in syntax.Usings) {
				if(u.Alias == null) {
					var symbol = GetSymbol(u.Name, model);
					if(symbol != null) {
						string n = symbol.ToDisplayString();
						usings.Add(n);
						if(!usingNamespaces.Contains(n)) {
							usingNamespaces.Add(n);
						}
					}
					else if(!u.Name.ToString().Contains(" ")) {
						string n = u.Name.ToString();
						usings.Add(n);
						if(!usingNamespaces.Contains(n)) {
							usingNamespaces.Add(n);
						}
					}
					else {
						Debug.Log($"Couldn't parse using directive syntax: {u}");
					}
				}
			}
			scriptGraph.UsingNamespaces = usings;
			Action onFinishAction = null;
			foreach(var member in syntax.Members) {
				onFinishAction += ParseMember(member, model, scriptGraph);
			}
			foreach(var root in parserData.roots) {
				foreach(var pair in root.properties) {
					if(!pair.Value.AutoProperty) {
						if(pair.Value.CanGetValue()) {
							if(pair.Key.AccessorList != null) {
								var accessor = pair.Key.AccessorList.ChildNodes().First(item => item.Kind() == SyntaxKind.GetAccessorDeclaration) as AccessorDeclarationSyntax;
								if(accessor.Body != null) {
									var nodes = ParseStatement(accessor.Body, model, pair.Value.getRoot);
									if(nodes != null && nodes.Count > 0) {
										var node = nodes[0];
										pair.Value.getRoot.Entry.EnsureRegistered();
										pair.Value.getRoot.Entry.exit.ConnectTo(node.nodeObject.primaryFlowInput);
									}
								}
								else if(accessor.ExpressionBody != null) {
									var body = accessor.ExpressionBody;
									var member = ParseExpression(body.Expression, model, pair.Value.getRoot);
									if(pair.Value.ReturnType() == typeof(void)) {
										pair.Value.getRoot.Entry.exit.ConnectTo(member.Get(null) as UPort);
									}
									else {
										var node = CreateNode<NodeReturn>("return", pair.Value.getRoot);
										pair.Value.getRoot.Entry.exit.ConnectTo(node.enter);
										node.Register();
										node.value.AssignToDefault(member);
									}
								}
							}
							else if(pair.Key.ExpressionBody != null) {
								var body = pair.Key.ExpressionBody;
								var member = ParseExpression(body.Expression, model, pair.Value.getRoot);
								if(pair.Value.ReturnType() == typeof(void)) {
									pair.Value.getRoot.Entry.exit.ConnectTo(member.Get(null) as UPort);
								}
								else {
									var node = CreateNode<NodeReturn>("return", pair.Value.getRoot);
									pair.Value.getRoot.Entry.exit.ConnectTo(node.enter);
									node.Register();
									node.value.AssignToDefault(member);
								}
							}
						}
						if(pair.Value.CanSetValue() && pair.Key.AccessorList != null) {
							var accessor = pair.Key.AccessorList.ChildNodes().First(item => item.Kind() == SyntaxKind.SetAccessorDeclaration) as AccessorDeclarationSyntax;
							if(accessor.Body != null) {
								var nodes = ParseStatement(accessor.Body, model, pair.Value.setRoot);
								if(nodes != null && nodes.Count > 0) {
									var node = nodes[0];
									pair.Value.setRoot.Entry.EnsureRegistered();
									pair.Value.setRoot.Entry.exit.ConnectTo(node.nodeObject.primaryFlowInput);
								}
							}
							else if(accessor.ExpressionBody != null) {
								var body = accessor.ExpressionBody;
								var member = ParseExpression(body.Expression, model, pair.Value.setRoot);
								var port = member.Get(null) as UPort;
								if(port != null) {
									if(port is FlowInput) {
										pair.Value.setRoot.Entry.exit.ConnectTo(port);
									}
									else if(port is ValueOutput valueOutput) {
										NodeObject flowNode;
										if(valueOutput.hasValidConnections) {
											flowNode = valueOutput.GetConnectedPorts().First().node;
										}
										else {
											flowNode = valueOutput.node;
										}
										if(flowNode.primaryFlowInput != null) {
											port = flowNode.primaryFlowInput;
										}
										else if(flowNode.FlowInputs.Count > 0) {
											port = flowNode.FlowInputs.First();
										}
										else {
											throw null;
										}
										pair.Value.setRoot.Entry.exit.ConnectTo(port);
									}
									else {
										throw null;
									}

								}
							}
						}
					}
				}
				foreach(var pair in root.constructors) {
					if(pair.Key.Body != null) {
						var nodes = ParseStatement(pair.Key.Body, model, pair.Value);
						if(nodes != null && nodes.Count > 0) {
							var node = nodes[0];
							pair.Value.Entry.EnsureRegistered();
							if(node.nodeObject.primaryFlowInput != null) {
								pair.Value.Entry.exit.ConnectTo(node.nodeObject.primaryFlowInput);
							}
							else if(node.nodeObject.FlowInputs.Count > 0) {
								var port = node.nodeObject.FlowInputs.First();
								pair.Value.Entry.exit.ConnectTo(port);
							}
							//else {
							//	Debug.LogWarning("Unable to find entry node from constructor: " + pair.Value.name);
							//}
						}
					}
					else if(pair.Key.ExpressionBody != null) {
						var body = pair.Key.ExpressionBody;
						var member = ParseExpression(body.Expression, model, pair.Value);
						var port = member.Get(null) as UPort;
						if(port != null) {
							pair.Value.Entry.exit.ConnectTo(port);
						}
					}
				}
				foreach(var pair in root.methods) {
					if(pair.Key.Body != null) {
						var nodes = ParseStatement(pair.Key.Body, model, pair.Value);
						if(nodes != null && nodes.Count > 0) {
							var node = nodes[0];
							pair.Value.Entry.EnsureRegistered();
							if(node.nodeObject.primaryFlowInput != null) {
								pair.Value.Entry.exit.ConnectTo(node.nodeObject.primaryFlowInput);
							}
							else if(node.nodeObject.FlowInputs.Count > 0) {
								var port = node.nodeObject.FlowInputs.First();
								pair.Value.Entry.exit.ConnectTo(port);
							}
							//else {
							//	Debug.LogWarning("Unable to find entry node from method: " + pair.Value.name);
							//}
						}
					}
					else if(pair.Key.ExpressionBody != null) {
						var body = pair.Key.ExpressionBody;
						var member = ParseExpression(body.Expression, model, pair.Value);
						if(pair.Value.ReturnType() == typeof(void)) {
							var port = member.Get(null) as UPort;
							if(port is FlowInput) {
								pair.Value.Entry.exit.ConnectTo(port);
							}
							else if(port is ValueOutput valueOutput) {
								NodeObject flowNode;
								if(valueOutput.hasValidConnections) {
									flowNode = valueOutput.GetConnectedPorts().First().node;
								}
								else {
									flowNode = valueOutput.node;
								}
								if(flowNode.primaryFlowInput != null) {
									port = flowNode.primaryFlowInput;
								}
								else if(flowNode.FlowInputs.Count > 0) {
									port = flowNode.FlowInputs.First();
								}
								else {
									throw null;
								}
								pair.Value.Entry.exit.ConnectTo(port);
							}
							else {
								throw null;
							}
						}
						else {
							var node = CreateNode<NodeReturn>("return", pair.Value);
							pair.Value.Entry.exit.ConnectTo(node.enter);
							node.Register();
							node.value.AssignToDefault(member);
						}
					}
				}
			}

			if(onFinishAction != null) {//Start handle nested types.
				onFinishAction();
			}

			//Convert inline value to nodes
			{
				foreach(var type in scriptGraph.TypeList) {
					if(type is IGraph graph) {
						var nodes = graph.GraphData.GetObjectsInChildren<NodeObject>(true, true).ToArray();
						foreach(var node in nodes) {
							foreach(var port in node.ValueInputs) {
								if(port.UseDefaultValue && port.isAssigned) {
									if(!IsSupportInline(port.DefaultValue) || !port.DefaultValue.targetType.HasFlags(
											MemberData.TargetType.Values |
											MemberData.TargetType.Self |
											MemberData.TargetType.uNodeVariable |
											MemberData.TargetType.uNodeLocalVariable |
											MemberData.TargetType.uNodeProperty |
											MemberData.TargetType.Null |
											MemberData.TargetType.Type)) {
										if(port.type.IsCastableTo(typeof(Delegate))) {
											var delegateType = port.type;
											if(delegateType != typeof(Delegate) && port.DefaultValue.targetType.HasFlags(MemberData.TargetType.Method | MemberData.TargetType.uNodeFunction)) {
												//In case it is dirrectly referencing function ex: SomeMethod(MyFunction)
												port.AssignToDefault(ToValueNode(new(port.DefaultValue), null, new ParserSetting(model, node.parent), true));
												//var lambda = CreateNode<Nodes.NodeLambda>("lambda", node.parent);
												//lambda.delegateType = delegateType;
												//lambda.Register();
												//var targetNode = CreateMultipurposeNode("node", node.parent, new MemberData(port.DefaultValue));
												//targetNode.useOutputParameters = false;
												//if(lambda.input != null) {
												//	lambda.input.ConnectTo(targetNode.output);
												//}
												//else {
												//	lambda.body.ConnectTo(targetNode.enter);
												//}
												//for(int i = 0; i < lambda.parameters.Count; i++) {
												//	lambda.parameters[i].port.ConnectToAsProxy(targetNode.parameters[i].input);
												//}
												//port.AssignToDefault(lambda.output);
												continue;
											}
										}
										if(!IsSupportInline(port.DefaultValue)) {
											port.AssignToDefault(ToValueNode(new(port.DefaultValue), null, new ParserSetting(model, node.parent)));
										}
									}
								}
							}
						}
						if(option_UseBlockSystem) {
							var supportedBlockNodes = nodes.Where(n => n.FlowInputs.Count == 1 && n.FlowOutputs.Count == 1).ToHashSet();
							HashSet<NodeObject> exceptions = new HashSet<NodeObject>();
							void RecursiveFindStack(NodeObject current, List<NodeObject> stacked) {
								if(current.FlowInputs.Count != 1 || current.FlowOutputs.Count != 1)
									return;
								if(exceptions.Add(current)) {
									stacked.Add(current);
									if(current.primaryFlowOutput.isAssigned) {
										RecursiveFindStack(current.primaryFlowOutput.GetTargetNode(), stacked);
									}
								}
							}

							foreach(var node in supportedBlockNodes) {
								var stacks = new List<NodeObject>();
								RecursiveFindStack(node, stacks);
								if(stacks.Count > 1) {
									var stackedNode = CreateNode<NodeAction>("action", stacks[0].parent);
									var first = stacks.First();
									var last = stacks.Last();
									if(first.primaryFlowInput.isConnected) {
										stackedNode.enter.ConnectTo(first.primaryFlowInput.connections[0].output);
									}
									if(last.primaryFlowOutput.isAssigned) {
										stackedNode.exit.ConnectTo(last.primaryFlowOutput.GetTargetFlow());
									}
									foreach(var stack in stacks) {
										stack.SetParent(stackedNode.data.container);
									}
								}
							}
						}
					}
				}
			}


			//Auto proxy
			{
				foreach(var port in parserData.autoProxyPorts) {
					foreach(var con in port.ValidConnections) {
						con.isProxy = true;
					}
				}
			}

			//Auto convert node for more compact graph
			{
				foreach(var type in scriptGraph.TypeList) {
					if(type is IGraph graph) {
						var nodes = graph.GraphData.GetObjectsInChildren<NodeObject>(true, true).ToArray();
						foreach(var node in nodes) {
							if(node == null) continue;
							try {
								if(node.node is MultipurposeNode mNode && mNode.instance != null && mNode.output != null && node.ValueInputs.Count == 1 && node.ValueOutputs.Count == 1 && (mNode.enter == null || !mNode.enter.hasValidConnections)) {
									var members = mNode.target.GetMembers(false);
									if(members != null && members.Length == 1) {
										var member = members[0];
										if(member.DeclaringType == typeof(Component) || member.DeclaringType == typeof(GameObject)) {
											if(member is MethodInfo method) {
												if(member.Name == nameof(Component.GetComponent)) {
													if(method.IsGenericMethod && method.GetParameters().Length == 0) {
														var gType = method.GetGenericArguments();
														var port = mNode.output.GetConnectedPorts().First();
														if(port.type == gType[0]) {
															var n = CreateNode<NodeValueConverter>("convert", node.parent);
															n.type = gType[0];
															n.output.ConnectTo(port);
															if(mNode.instance.UseDefaultValue) {
																n.input.AssignToDefault(ToValueNode(new(mNode.instance.DefaultValue), null, new(model, node.parent)));
															}
															else {
																n.input.ConnectTo(mNode.instance.GetTargetPort());
															}
														}
														else {
															var n = CreateNode<NodeConvert>("convert", node.parent);
															n.type = gType[0];
															n.output.ConnectTo(port);
															if(mNode.instance.UseDefaultValue) {
																n.target.AssignToDefault(ToValueNode(new(mNode.instance.DefaultValue), null, new(model, node.parent)));
															}
															else {
																n.target.ConnectTo(mNode.instance.GetTargetPort());
															}
														}
														node.Destroy();
														continue;
													}
												}
											}
											else if(mNode.output.connections.Count == 1) {
												if(member is PropertyInfo property) {
													if(member.Name == nameof(Component.transform)) {
														var n = CreateNode<NodeValueConverter>("convert", node.parent);
														n.type = typeof(Transform);
														n.output.ConnectTo(mNode.output.GetConnectedPorts().First());
														if(mNode.instance.UseDefaultValue) {
															n.input.AssignToDefault(ToValueNode(new(mNode.instance.DefaultValue), null, new(model, node.parent)));
														}
														else {
															n.input.ConnectTo(mNode.instance.GetTargetPort());
														}
														node.Destroy();
														continue;
													}
												}
											}
										}
									}
								}
								if(node.node is NodeConvert nodeConvert) {
									if(nodeConvert.output.hasValidConnections && nodeConvert.output.connections.Count == 1) {
										var conn = nodeConvert.output.connections[0];
										if(conn.input.type != typeof(object)) {
											var n = CreateNode<NodeValueConverter>("convert", node.parent);
											n.type = nodeConvert.type;
											n.output.ConnectTo(nodeConvert.output.GetConnectedPorts().First());
											if(nodeConvert.target.UseDefaultValue) {
												n.input.AssignToDefault(ToValueNode(new(nodeConvert.target.DefaultValue), null, new(model, node.parent)));
											}
											else {
												n.input.ConnectTo(nodeConvert.target.GetTargetPort());
											}
											node.Destroy();
											continue;
										}
									}
								}
							}
							catch(Exception ex) {
								Debug.LogException(ex);
							}
						}
					}
				}
			}

			{//Auto group variables & functions
				foreach(var type in scriptGraph.TypeList) {
					if(type is IGraph graph) {
						var vContainer = graph.GraphData.variableContainer;
						if(vContainer.childCount >= 10) {
							int publicCount = vContainer.GetObjectsInChildren<Variable>().Count(v => v.modifier.isPublic);
							int nonPublicCount = vContainer.GetObjectsInChildren<Variable>().Count(v => v.modifier.isPublic == false);
							if(publicCount >= 5) {
								var list = vContainer.GetObjectsInChildren<Variable>().Where(v => v.modifier.isPublic).ToArray();
								var group = new UGroupElement() { name = "Public" };
								vContainer.InsertChild(0, group);
								foreach(var v in list) {
									v.SetParent(group);
								}
							}
							if(nonPublicCount >= 5) {
								var list = vContainer.GetObjectsInChildren<Variable>().Where(v => v.modifier.isPublic == false).ToArray();
								var group = new UGroupElement() { name = "Private" };
								vContainer.AddChild(group);
								foreach(var v in list) {
									v.SetParent(group);
								}
							}
						}
						var fContainer = graph.GraphData.functionContainer;
						if(fContainer.childCount >= 10) {
							int publicCount = fContainer.GetObjectsInChildren<Function>().Count(v => v.modifier.isPublic);
							int nonPublicCount = fContainer.GetObjectsInChildren<Function>().Count(v => v.modifier.isPublic == false);
							if(publicCount >= 5) {
								var list = fContainer.GetObjectsInChildren<Function>().Where(v => v.modifier.isPublic).ToArray();
								var group = new UGroupElement() { name = "Public" };
								fContainer.InsertChild(0, group);
								foreach(var v in list) {
									v.SetParent(group);
								}
							}
							if(nonPublicCount >= 5) {
								var list = fContainer.GetObjectsInChildren<Function>().Where(v => v.modifier.isPublic == false).ToArray();
								var group = new UGroupElement() { name = "Private" };
								fContainer.AddChild(group);
								foreach(var v in list) {
									v.SetParent(group);
								}
							}
						}
					}
				}
			}

			//Place Fit Nodes
			{

				//Vector2[] GetSquarePositions(int count, float spacing) {
				//	// Auto rows & cols based on sqrt
				//	int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
				//	int rows = Mathf.CeilToInt((float)count / cols);

				//	Vector2[] positions = new Vector2[count];
				//	int index = 0;

				//	for(int r = 0; r < rows; r++) {
				//		for(int c = 0; c < cols; c++) {
				//			if(index >= count)
				//				break;

				//			// Start from top-left (0,0), go right (x+) and down (y-)
				//			float x = c * spacing;
				//			float y = -r * spacing;
				//			positions[index++] = new Vector2(x, y);
				//		}
				//	}

				//	return positions;
				//}
				//var pool = StaticListPool<NodeObject>.Allocate();
				//foreach(var type in scriptGraph.TypeList) {
				//	if(type is IGraph graph) {
				//		pool.Clear();
				//		pool.AddRange(graph.GraphData.GetObjectsInChildren<NodeObject>(true, true));
				//		var positions = GetSquarePositions(pool.Count, 250);

				//		for(int i = 0; i < pool.Count; i++) {
				//			pool[i].SetPosition(positions[i]);
				//		}
				//	}
				//}
				//StaticListPool<NodeObject>.Free(pool);

				foreach(var type in scriptGraph.TypeList) {
					if(type is IGraph graph) {
						foreach(var nodeObject in graph.GraphData.GetObjectsInChildren<NodeObject>(true, true)) {
							if(nodeObject.ValueInputs.Any(p => p.hasValidConnections) ||
								nodeObject.FlowOutputs.Any(p => p.hasValidConnections)) {
								if(nodeObject.FlowInputs.Any(p => p.hasValidConnections) == false &&
								nodeObject.ValueOutputs.Any(p => p.hasValidConnections) == false) {
									NodeEditorUtility.PlaceFit.PlaceFitNodes(nodeObject);
								}
							}
						}
					}
				}

				foreach(var type in scriptGraph.TypeList) {
					if(type is IGraph graph) {
						foreach(var container in graph.GraphData.GetObjectsInChildren<NodeContainerWithEntry>(true, true)) {
							var entry = container.Entry;
							NodeEditorUtility.PlaceFit.PlaceFitNodes(entry);
						}
					}
				}
			}
		}

		#region Parse Type
		/// <summary>
		/// Function to parse type from syntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public static SerializedType ParseType(SyntaxNode syntax, SemanticModel model) {
			if(syntax is BaseTypeDeclarationSyntax) {
				return ParseType(model.GetDeclaredSymbol(syntax as BaseTypeDeclarationSyntax));
			}
			else if(syntax is AttributeSyntax) {
				return ParseType(model.GetTypeInfo(syntax).Type);
			}
			else if(syntax is TypeOfExpressionSyntax) {
				return ParseType(model.GetTypeInfo((syntax as TypeOfExpressionSyntax).Type).Type);
			}
			else {
				var symbol = model.GetSymbolInfo(syntax).Symbol;
				if(syntax is ITypeSymbol) {
					return ParseType(symbol as ITypeSymbol);
				}
				else if(syntax is IFieldSymbol) {
					return ParseType((symbol as IFieldSymbol).Type);
				}
				else if(syntax is IPropertySymbol) {
					return ParseType((symbol as IPropertySymbol).Type);
				}
				else if(syntax is IMethodSymbol) {
					return ParseType((symbol as IMethodSymbol).ReturnType);
				}
				else if(syntax is IParameterSymbol) {
					return ParseType(symbol as IParameterSymbol);
				}
				else {
					if(syntax is LiteralExpressionSyntax literal) {
						var token = literal.Token;
						if(token.Value == null) {
							return typeof(object);
						}
						else {
							return token.Value.GetType();
						}
					}
					else if(syntax is InitializerExpressionSyntax) {
						var kind = syntax.Kind();
						if(kind == SyntaxKind.ComplexElementInitializerExpression) {
							var parent = syntax.FirstAncestorOrSelf<BaseObjectCreationExpressionSyntax>();
							if(parent != null) {
								var parentSymbol = GetTypeSymbol(parent, model);
								if(parentSymbol != null) {
									var parentType = ParseType(parentSymbol);
									if(parentType != null) {
										var elementType = parentType.type.ElementType();
										if(elementType != null) {
											return elementType;
										}
									}
								}
							}
						}
					}
					return ParseType(model.GetTypeInfo(syntax).Type);
				}
			}
		}

		/// <summary>
		/// Function to parse TypeSymbol to MemberData
		/// </summary>
		/// <param name="typeSymbol"></param>
		/// <returns></returns>
		public static SerializedType ParseType(ITypeSymbol typeSymbol, bool isByRef = false) {
			if(typeSymbol == null) {
				throw new ArgumentNullException("typeSymbol");
			}
			var owner = GetSymbolOwner(typeSymbol);
			if(owner != null) {
				if(typeSymbol.TypeKind == TypeKind.TypeParameter) {
					//TODO: add support for generic
					throw new NotSupportedException("Generic Type is not supported");
					//return new MemberData() {
					//	name = typeSymbol.Name,
					//	targetType = MemberData.TargetType.uNodeGenericParameter,
					//	startType = owner.GetType(),
					//	type = typeof(object),
					//	instance = owner,
					//};
				}
			}
			if(typeSymbol.Kind == SymbolKind.ArrayType) {
				var arraySymbol = typeSymbol as IArrayTypeSymbol;
				if(arraySymbol.Rank > 1) {
					throw new Exception("two or more dimensional array aren't supported" + $"\nOn Script: {parserData.name}");
				}
				var elementType = ParseType(arraySymbol.ElementType);
				if(elementType.typeKind == SerializedTypeKind.GenericParameter) {
					var result = SerializerUtility.Duplicate(elementType);
					//result.name += "[]";
					return result;
				}
				Type t = elementType.type.MakeArrayType();
				if(isByRef) {
					t = t.MakeByRefType();
				}
				return t;
			}
			if(typeSymbol.SpecialType != SpecialType.None) {
				switch(typeSymbol.SpecialType) {
					case SpecialType.System_Boolean:
						return !isByRef ? typeof(bool) : typeof(bool).MakeByRefType();
					case SpecialType.System_Byte:
						return !isByRef ? typeof(byte) : typeof(byte).MakeByRefType();
					case SpecialType.System_Char:
						return !isByRef ? typeof(char) : typeof(char).MakeByRefType();
					case SpecialType.System_Decimal:
						return !isByRef ? typeof(decimal) : typeof(decimal).MakeByRefType();
					case SpecialType.System_Double:
						return !isByRef ? typeof(double) : typeof(double).MakeByRefType();
					case SpecialType.System_Int16:
						return !isByRef ? typeof(short) : typeof(short).MakeByRefType();
					case SpecialType.System_Int32:
						return !isByRef ? typeof(int) : typeof(int).MakeByRefType();
					case SpecialType.System_Int64:
						return !isByRef ? typeof(long) : typeof(long).MakeByRefType();
					case SpecialType.System_Object:
						return !isByRef ? typeof(object) : typeof(object).MakeByRefType();
					case SpecialType.System_SByte:
						return !isByRef ? typeof(sbyte) : typeof(sbyte).MakeByRefType();
					case SpecialType.System_Single:
						return !isByRef ? typeof(float) : typeof(float).MakeByRefType();
					case SpecialType.System_String:
						return !isByRef ? typeof(string) : typeof(string).MakeByRefType();
					case SpecialType.System_UInt16:
						return !isByRef ? typeof(ushort) : typeof(ushort).MakeByRefType();
					case SpecialType.System_UInt32:
						return !isByRef ? typeof(uint) : typeof(uint).MakeByRefType();
					case SpecialType.System_UInt64:
						return !isByRef ? typeof(ulong) : typeof(ulong).MakeByRefType();
					case SpecialType.System_Void:
						return !isByRef ? typeof(void) : typeof(void).MakeByRefType();
					case SpecialType.System_ValueType:
						return !isByRef ? typeof(ValueType) : typeof(ValueType).MakeByRefType();
					case SpecialType.System_Collections_Generic_ICollection_T:
						return !isByRef ? typeof(ICollection<>) : typeof(ICollection<>).MakeByRefType();
					case SpecialType.System_Collections_Generic_IEnumerable_T:
						return !isByRef ? typeof(IEnumerable<>) : typeof(IEnumerable<>).MakeByRefType();
					case SpecialType.System_Collections_Generic_IEnumerator_T:
						return !isByRef ? typeof(IEnumerator<>) : typeof(IEnumerator<>).MakeByRefType();
					case SpecialType.System_Collections_Generic_IList_T:
						return !isByRef ? typeof(IList<>) : typeof(IList<>).MakeByRefType();
					case SpecialType.System_Collections_Generic_IReadOnlyCollection_T:
						return !isByRef ? typeof(IReadOnlyCollection<>) : typeof(IReadOnlyCollection<>).MakeByRefType();
					case SpecialType.System_Collections_Generic_IReadOnlyList_T:
						return !isByRef ? typeof(IReadOnlyList<>) : typeof(IReadOnlyList<>).MakeByRefType();
					case SpecialType.System_Collections_IEnumerable:
						return !isByRef ? typeof(IEnumerable) : typeof(IEnumerable).MakeByRefType();
					case SpecialType.System_Collections_IEnumerator:
						return !isByRef ? typeof(IEnumerator) : typeof(IEnumerator).MakeByRefType();
					case SpecialType.System_DateTime:
						return !isByRef ? typeof(DateTime) : typeof(DateTime).MakeByRefType();
					case SpecialType.System_Delegate:
						return !isByRef ? typeof(Delegate) : typeof(Delegate).MakeByRefType();
					case SpecialType.System_Enum:
						return !isByRef ? typeof(Enum) : typeof(Enum).MakeByRefType();
					case SpecialType.System_IAsyncResult:
						return !isByRef ? typeof(IAsyncResult) : typeof(IAsyncResult).MakeByRefType();
					case SpecialType.System_IDisposable:
						return !isByRef ? typeof(IDisposable) : typeof(IDisposable).MakeByRefType();
					case SpecialType.System_IntPtr:
						return !isByRef ? typeof(IntPtr) : typeof(IntPtr).MakeByRefType();
					case SpecialType.System_MulticastDelegate:
						return !isByRef ? typeof(MulticastDelegate) : typeof(MulticastDelegate).MakeByRefType();
					case SpecialType.System_Nullable_T:
						return !isByRef ? typeof(Nullable<>) : typeof(Nullable<>).MakeByRefType();
					case SpecialType.System_RuntimeArgumentHandle:
						return !isByRef ? typeof(RuntimeArgumentHandle) : typeof(RuntimeArgumentHandle).MakeByRefType();
					case SpecialType.System_RuntimeFieldHandle:
						return !isByRef ? typeof(RuntimeFieldHandle) : typeof(RuntimeFieldHandle).MakeByRefType();
					case SpecialType.System_RuntimeMethodHandle:
						return !isByRef ? typeof(RuntimeMethodHandle) : typeof(RuntimeMethodHandle).MakeByRefType();
					case SpecialType.System_RuntimeTypeHandle:
						return !isByRef ? typeof(RuntimeTypeHandle) : typeof(RuntimeTypeHandle).MakeByRefType();
				}
			}
			if(typeSymbol is INamedTypeSymbol) {
				INamedTypeSymbol namedTypeSymbol = typeSymbol as INamedTypeSymbol;
				if(namedTypeSymbol.IsGenericType) {
					Type genericType = namedTypeSymbol.ConstructUnboundGenericType().ToDisplayString(RoslynUtility.genericDisplayFormat).Add("`" + namedTypeSymbol.TypeArguments.Length).ToType(false);
					if(genericType == null) {//Retry to parse type using different method.
						if(namedTypeSymbol != namedTypeSymbol.OriginalDefinition) {
							var baseType = ParseType(namedTypeSymbol.OriginalDefinition);
							if(baseType != null && baseType.type != null) {
								genericType = baseType.type.MakeGenericType(namedTypeSymbol.TypeArguments.Select(item => ParseType(item).type).ToArray());
								if(isByRef) {
									genericType = genericType.MakeByRefType();
								}
								return genericType;
							}
							else {
								baseType = RoslynUtility.GetTypeFromTypeName(namedTypeSymbol.ToString());
								if(baseType != null) {
									if(isByRef) {
										baseType = baseType.type.MakeByRefType();
									}
									return baseType;
								}
								throw new System.Exception("Failed to deserialize type: " + namedTypeSymbol.ToString() + $"\nOn Script: {parserData.name}");
							}
						}
						else {
							return SerializedType.None;
						}
					}
					List<SerializedType> types = new List<SerializedType>();
					foreach(var arg in namedTypeSymbol.TypeArguments) {
						types.Add(ParseType(arg));
					}
					if(types.Any(item => item.typeKind == SerializedTypeKind.GenericParameter)) {
						//TODO: add support for generic parameter
						throw new NotImplementedException();
						//var member = new MemberData() {
						//	instance = types[0].instance,
						//	targetType = MemberData.TargetType.uNodeGenericParameter,
						//};
						//var typeDatas = MemberDataUtility.MakeTypeDatas(types);
						//member.Items = new MemberData.ItemData[]{
						//	new MemberData.ItemData() {
						//		genericArguments = new TypeData[] { new TypeData() {
						//				name = genericType.FullName,
						//				parameters = typeDatas.ToArray()
						//			}
						//		}
						//	}
						//};
						//return member;
					}
					var gTypes = types.Select(item => item.type);
					if(gTypes.All(item => item != null)) {
						Type t = genericType.MakeGenericType(gTypes.ToArray());
						if(isByRef) {
							t = t.MakeByRefType();
						}
						return t;
					}
					return !isByRef ? genericType : genericType.MakeByRefType();
				}
			}
			bool flag = typeSymbol.Locations.Any(item => item.IsInSource);
			if(flag) {
				var comp = GetSymbolReferenceValue(typeSymbol, new ParserSetting());
				if(comp != null) {
					return comp.startType;
				}
			}
			//Check if type is nested type
			if(typeSymbol.ContainingType != null) {
				return (ParseType(typeSymbol.ContainingType).type.FullName + "+" + typeSymbol.Name + (isByRef ? "&" : "")).ToType();
			}
			Type type = TypeSerializer.Deserialize(typeSymbol.ToString() + (isByRef ? "&" : ""), false);
			//If type not found, try to find it manually.
			if(type == null) {
				if(usingNamespaces != null) {
					string nm = typeSymbol.ToString() + (isByRef ? "&" : "");
					foreach(var n in usingNamespaces) {
						type = TypeSerializer.Deserialize(n + "." + nm, false);
						if(type != null)
							break;
					}
					if(type == null) {
						foreach(var n in usingNamespaces) {
							type = TypeSerializer.Deserialize(n + "." + nm + "Attribute", false);
							if(type != null)
								break;
						}
					}
				}
				if(type == null) {
					var types = EditorReflectionUtility.GetRuntimeTypes();
					if(types != null) {
						var fullName = typeSymbol.ToString();
						foreach(var rt in types) {
							if(rt == null) continue;
							if(rt.FullName == fullName) {
								return rt;
							}
						}
					}
					throw new Exception("The type name : " + typeSymbol.ToString() + (isByRef ? "&" : "") + " not found" + $"\nOn Script: {parserData.name}");
				}
			}
			return type;
		}

		/// <summary>
		/// Function to parse IParameterSymbol to MemberData
		/// </summary>
		/// <param name="parameter"></param>
		/// <returns></returns>
		public static SerializedType ParseType(IParameterSymbol parameter) {
			//if(parameter.Type.TypeKind == TypeKind.TypeParameter) {//Generic type

			//}
			return ParseType(parameter.Type, !(parameter.RefKind == Microsoft.CodeAnalysis.RefKind.None || parameter.RefKind == Microsoft.CodeAnalysis.RefKind.In));
		}

		/// <summary>
		/// Parse TypeDeclarationSyntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="root"></param>
		/// <param name="model"></param>
		public static void ParseTypeDeclarationSyntax(TypeDeclarationSyntax syntax, ClassScript root, SemanticModel model) {
			if(root == null)
				throw new Exception();
			if(root is IClassModifier classModifier) {
				foreach(var token in syntax.Modifiers) {
					switch(token.Kind()) {
						case SyntaxKind.PublicKeyword:
							classModifier.GetModifier().Public = true;
							break;
						case SyntaxKind.PrivateKeyword:
							classModifier.GetModifier().Private = true;
							break;
						case SyntaxKind.ProtectedKeyword:
							classModifier.GetModifier().Protected = true;
							break;
						case SyntaxKind.InternalKeyword:
							classModifier.GetModifier().Internal = true;
							break;
						case SyntaxKind.AbstractKeyword:
							classModifier.GetModifier().Abstract = true;
							break;
						case SyntaxKind.SealedKeyword:
							classModifier.GetModifier().Sealed = true;
							break;
						case SyntaxKind.StaticKeyword:
							classModifier.GetModifier().Static = true;
							break;
					}
				}
			}
			var attributes = ParseAttribute(syntax.AttributeLists, model, root.GraphData);
			root.GraphData.attributes = attributes;
		}
		#endregion

		/// <summary>
		/// A Function to parse Attribute
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <returns></returns>
		public static List<AttributeData> ParseAttribute(SyntaxList<AttributeListSyntax> syntax, SemanticModel model, UGraphElement root) {
			List<AttributeData> attributeList = new List<AttributeData>();
			foreach(var attributes in syntax) {
				foreach(var attribute in attributes.Attributes) {
					var attData = new AttributeData();
					var typeSymbol = GetTypeSymbol(attribute, model);
					var type = ParseType(typeSymbol).type;
					attData.attributeType = type;
					attributeList.Add(attData);
					if(attribute.ArgumentList != null) {
						List<object> args = new List<object>();
						var symbol = GetSymbol(attribute, model) as IMethodSymbol;
						List<ParameterValueData> namedParameters = new List<ParameterValueData>();
						foreach(var arg in attribute.ArgumentList.Arguments) {
							MemberData val;
							if(TryParseExpression(arg.Expression, model, root, out val)) {
								if(arg.NameEquals != null) {
									string paramName = GetSymbol(arg.NameEquals.Name, model).Name;
									namedParameters.Add(new ParameterValueData(paramName, ParseType(arg.NameEquals.Name, model), val.Get(null)));
								}
								else {
									args.Add(val.Get(null));
									//var argType = model.GetTypeInfo(arg.Expression).Type;
									//typeArgs.Add(ParseType(argType));
								}
							}
							else {
								Debug.LogError($"Can't parse attribute arguments\nOn Script: {parserData.name}");
								args = null;
								break;
							}
						}
						if(args != null) {
							var typeArgs = symbol.Parameters.Select(p => ParseType(p).type).ToArray();
							if(symbol.Parameters.Any(p => p.IsParams)) {
								//Handle params keyword
								if(args.Count > typeArgs.Length || args.Count > 0 && args[args.Count - 1] != null && args[args.Count - 1] is not Array) {
									var lastValue = args.TakeLast((args.Count - typeArgs.Length) + 1).ToArray();
									for(int i = 0; i < lastValue.Length; i++) {
										args.RemoveAt(args.Count - 1);
									}
									args.Add(lastValue);
								}
							}
							var ctor = type.GetConstructor(typeArgs.ToArray());
							if(ctor != null) {
								var cvd = new ConstructorValueData(ctor);
								for(int i = 0; i < args.Count; i++) {
									cvd.parameters[i].value = args[i];
								}
								cvd.initializer = namedParameters.ToArray();
								attData.constructor = cvd;
							}
							else {
								Debug.LogError($"Couldn't find matching constructor in attribute: {type.FullName}\nType Args: {string.Join(", ", typeArgs.Select(t => t.FullName))}\nOn Script: {parserData.name}");
							}
						}
						else {
							attData.constructor = new ConstructorValueData(type);
						}
					}
					else {
						attData.constructor = new ConstructorValueData(type);
					}
				}
			}
			return attributeList;
		}

		/// <summary>
		/// Get native type for the graph
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public static Type GetGraphType(SyntaxNode syntax, SemanticModel model) {
			var typeDeclaration = syntax.FirstAncestorOrSelf<TypeDeclarationSyntax>();
			var symbol = model.GetDeclaredSymbol(typeDeclaration);
			return ParseType(symbol);
		}

		/// <summary>
		/// Create a new node
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="name"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		public static T CreateNode<T>(string name, UGraphElement parent) where T : Node, new() {
			var result = new T();
			var node = new NodeObject(result) { name = name };
			parent.AddChild(node);
			node.EnsureRegistered();
			return result;
		}

		public static T CreateElement<T>(UGraphElement parent) where T : UGraphElement, new() {
			return CreateElement<T>("element", parent);
		}

		public static T CreateElement<T>(string name, UGraphElement parent) where T : UGraphElement, new() {
			return parent.AddChild<T>(new T() { name = name });
		}

		public static MemberData CreateFromPort(UPort port) {
			if(port == null) {
				throw null;
			}
			return MemberData.CreateFromValue(new UPortRef(port));
		}

		/// <summary>
		/// A Function to creating MultipurposeNode
		/// </summary>
		/// <param name="name"></param>
		/// <param name="parent"></param>
		/// <param name="target"></param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public static MultipurposeNode CreateMultipurposeNode(string name, UGraphElement parent, MemberData target, params MemberData[] parameters) {
			return CreateMultipurposeNode(name, parent, target, parameters as IList<MemberData>);
		}

		/// <summary>
		/// A Function to creating MultipurposeNode
		/// </summary>
		public static MultipurposeNode CreateMultipurposeNode(string name, UGraphElement parent, MemberData target, IList<MemberData> parameters) {
			if(target.targetType == MemberData.TargetType.NodePort) {
				throw new InvalidOperationException();
			}
			var node = CreateNode<MultipurposeNode>(name ?? "MultipurposeNode", parent);
			node.target = target;
			node.useOutputParameters = parameters == null || parameters.Any(p => !p.isAssigned);
			node.Register();

			var memberInvoke = node.member;
			if(parameters != null && memberInvoke.parameters != null && memberInvoke.parameters.Count >= parameters.Count) {
				for(int i = 0; i < parameters.Count; i++) {
					var param = parameters[i];
					var targetParam = memberInvoke.parameters[i];
					if(targetParam.input != null) {
						targetParam.input.ConnectTo(param);
					}
				}
			}
			if(node.instance != null && target.instance != null) {
				if(target.instance is MemberData) {
					node.instance.ConnectTo(target.instance as MemberData);
				}
				else {
					node.instance.AssignToDefault(target.instance);
				}
			}
			return node;
		}

		/// <summary>
		/// A Function to get MemberData from ExpressionSyntax.
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		public static MemberData ParseExpression(ExpressionSyntax syntax, SemanticModel model, UGraphElement parent) {
			MemberData value;
			if(TryParseExpression(syntax, model, parent, out value)) {
				return value;
			}
			else {
				throw new Exception("Couldn't handle expression:" + syntax + $"\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax)}");
			}
		}


		/// <summary>
		/// A Function to get MemberData from ExpressionSyntax.
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="setting"></param>
		/// <returns></returns>
		public static MemberData ParseExpression(ExpressionSyntax syntax, ParserSetting setting) {
			MemberData value;
			if(TryParseExpression(syntax, setting, out value)) {
				return value;
			}
			else {
				throw new Exception("Couldn't handle expression:" + syntax + $"\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax)}");
			}
		}

		/// <summary>
		/// A Function to get MemberData from PatternSyntax.
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="setting"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public static MemberData ParsePattern(PatternSyntax syntax, ParserSetting setting) {
			if(syntax is TypePatternSyntax) {
				var ex = syntax as TypePatternSyntax;
				return ParseExpression(ex.Type, setting);
			}
			throw new Exception("Couldn't handle pattern expression:" + syntax + $"\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax)}");
		}

		/// <summary>
		/// A Function to get MemberData from PatternSyntax.
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="expressionValue"></param>
		/// <param name="setting"></param>
		/// <returns></returns>
		/// <exception cref="Exception"></exception>
		public static MemberData ParsePattern(PatternSyntax syntax, MemberData expressionValue, ParserSetting setting) {
			if(syntax is TypePatternSyntax) {
				return ParsePattern(syntax, setting);
			}
			else if(syntax is BinaryPatternSyntax) {
				var patternExpression = syntax as BinaryPatternSyntax;
				if(patternExpression.Kind() == SyntaxKind.OrPattern) {
					var or = CreateNode<MultiORNode>("OR", setting.parent);

					var leftType = ParsePattern(patternExpression.Left, setting);
					var rightType = ParsePattern(patternExpression.Right, setting);
					if(leftType.targetType == MemberData.TargetType.Type && rightType.targetType == MemberData.TargetType.Type) {
						var left = CreateNode<ISNode>("IS", setting.parent);
						left.type = leftType.startType;
						left.target.AssignToDefault(expressionValue);
						left.Register();

						var right = CreateNode<ISNode>("IS", setting.parent);
						right.type = rightType.startType;
						right.target.AssignToDefault(expressionValue);
						right.Register();

						or.inputs[0].port.ConnectTo(left.output);
						or.inputs[1].port.ConnectTo(right.output);

						return CreateFromPort(or.output);
					}

				}
			}
			throw new Exception("Couldn't handle pattern expression:" + syntax + $"\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax)}");
		}

		/// <summary>
		/// A Function to get MemberData from ExpressionSyntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		/// <param name="recursive"></param>
		/// <returns></returns>
		public static bool TryParseExpression(ExpressionSyntax syntax, SemanticModel model, UGraphElement parent, out MemberData value, bool recursive = true) {
			return TryParseExpression(syntax, new ParserSetting(model, parent), out value, recursive);
		}

		/// <summary>
		/// A Function to get MemberData from ExpressionSyntax.
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="setting"></param>
		/// <param name="value"></param>
		/// <param name="recursive"></param>
		/// <returns></returns>
		public static bool TryParseExpression(ExpressionSyntax syntax, ParserSetting setting, out MemberData value, bool recursive = true) {
			try {
				foreach(var handler in syntaxHandlerList) {
					if(handler.ExpressionHandler(syntax, new ParserData(setting), out value)) {
						return true;
					}
				}
			}
			catch {
				if(syntax == null) {
					throw new System.ArgumentNullException("syntax");
				}
				else {
					Debug.LogError($"Error on parsing syntax : {syntax.ToString()} type: {syntax.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax)}");
				}
				throw;
			}
			if(syntax is NameSyntax) {
				var ex = syntax as NameSyntax;
				var symbol = GetSymbol(ex, setting.model);
				if(symbol == null) {
					if(string.IsNullOrEmpty(ex.ToFullString())) {
						value = null;
						return false;
					}
					throw new Exception("Couldn't found symbol for syntax:" + ex + "\nMaybe the modifier is private, protected, or internal?\nPlease change the modifier to public and try parse again." + $"\nOn Script: {parserData.name}");
				}
				if(IsInSource(symbol.Locations)) {
					var symbolValue = GetSymbolReferenceValue(symbol, setting);
					if(symbolValue != null) {
						value = MemberData.Clone(symbolValue);
						return true;
					}
					if(symbol is IMethodSymbol) {
						var MSymbol = symbol as IMethodSymbol;
						if(setting.CanCreateNode) {
							var node = CreateNode<NodeAnonymousFunction>("AnonymousFunction", setting.parent);
							node.returnType = ParseType(MSymbol.ReturnType);
							List<MemberData> parameters = new List<MemberData>();
							if(MSymbol.Parameters.Length > 0) {
								for(int i = 0; i < MSymbol.Parameters.Length; i++) {
									var paramData = new NodeAnonymousFunction.ParameterData() {
										name = MSymbol.Parameters[i].Name,
										type = CSharpParser.ParseType(MSymbol.Parameters[i]),
									};
									var pData = MemberData.CreateFromValue(new UPortRef(paramData.id, PortKind.ValueOutput, node));
									parameters.Add(pData);
									//Register parameter used for identity parameter.
									CSharpParser.RegisterSymbol(MSymbol.Parameters[i], node, _ => pData);
								}
							}
							var body = CreateMultipurposeNode(MSymbol.Name, setting.parent, GetMethodData(MSymbol, setting), parameters.ToArray());
							if(node.returnType.type != typeof(void)) {
								if(body.nodeObject.CanGetValue()) {
									var n = CSharpParser.CreateNode<NodeReturn>("return", setting.parent);
									node.body.ConnectTo(n.enter);
									n.Register();
									n.value.ConnectTo(body.output);
								}
								else if(body.IsFlowNode()) {
									node.body.ConnectTo(body.enter);
								}
							}
							else {
								if(body != null) {
									if(body.IsFlowNode()) {
										node.body.ConnectTo(body.enter);
									}
									else if(body.nodeObject.CanGetValue()) {
										var n = CSharpParser.CreateNode<NodeReturn>("return", setting.parent);
										node.body.ConnectTo(n.enter);
										n.Register();
										n.value.ConnectTo(body.output);
									}
								}
							}
							value = CreateFromPort(node.output);
							return true;
						}
					}
					else if(symbol is ITypeParameterSymbol && GetSymbolOwner(symbol) != null) {
						value = MemberData.CreateFromType(ParseType(syntax, setting.model));
						return true;
					}
					else if(symbol is ITypeSymbol) {
						value = MemberData.CreateFromType(ParseType(syntax, setting.model));
						return true;
					}
					else if(symbol is IEventSymbol) {
						var data = setting.root.GetVariable(symbol.Name);
						value = MemberData.CreateFromValue(data);
						//var member = new MemberData {
						//	name = symbol.Name,
						//	instance = setting.root,
						//	targetType = MemberData.TargetType.uNodeVariable,
						//	type = ParseType((symbol as IEventSymbol).Type).startType,
						//	isStatic = false
						//};
						//member.startType = member.type;
						//value = member;
						return true;
					}
					else if(symbol is IFieldSymbol) {
						var type = ParseType((symbol as IFieldSymbol).Type);
						var containingType = ParseType(symbol.ContainingType);
						if(type != null) {
							if(type.type.IsEnum) {
								value = MemberData.CreateFromValue(Enum.Parse(type, symbol.Name));
							}
							else if(!symbol.IsStatic) {
								value = new MemberData {
									Items = new[] { new MemberData.ItemData(containingType.type.Name), new MemberData.ItemData(symbol.Name) },
									instance = MemberData.This(setting.root),
									targetType = MemberData.TargetType.Field,
									type = type,
									startType = containingType,
									isStatic = false
								};
							}
							else {
								value = new MemberData {
									Items = new[] { new MemberData.ItemData(containingType.type.Name), new MemberData.ItemData(symbol.Name) },
									instance = null,
									targetType = MemberData.TargetType.Field,
									type = type,
									startType = containingType,
									isStatic = true
								};
							}
							return true;
						}
					}
					else if(symbol is IParameterSymbol) {
						var type = ParseType((symbol as IParameterSymbol).Type);
						var symbolReference = GetSymbolReferenceValue(symbol, setting);
						Debug.Log(symbolReference);
						if(symbolReference != null) {
							value = symbolReference;
							return true;
						}
					}
					if(recursive) {
						var member = GetMemberFromExpression(syntax, setting);
						if(member != null) {
							value = member;
							return true;
						}
					}
				}
				else {
					if(symbol is ITypeSymbol) {
						value = MemberData.CreateFromType(ParseType(symbol as ITypeSymbol));
						return true;
					}
					else if(symbol is ITypeParameterSymbol) {
						value = MemberData.CreateFromType(ParseType(syntax, setting.model));
						return true;
					}
					else if(symbol is IFieldSymbol) {
						var type = ParseType((symbol as IFieldSymbol).Type);
						if(type != null) {
							//if(type.type.IsEnum) {
							//	value = MemberData.CreateFromValue(Enum.Parse(type, symbol.Name));
							//}
							//else 
							var containingType = ParseType(symbol.ContainingType);
							if(!symbol.IsStatic) {
								value = new MemberData {
									Items = new[] { new MemberData.ItemData(containingType.type.Name), new MemberData.ItemData(symbol.Name) },
									instance = MemberData.This(setting.root),
									targetType = MemberData.TargetType.Field,
									type = type,
									startType = containingType,
									isStatic = false
								};
							}
							else {
								value = new MemberData {
									Items = new[] { new MemberData.ItemData(containingType.type.Name), new MemberData.ItemData(symbol.Name) },
									instance = null,
									targetType = MemberData.TargetType.Field,
									type = type,
									startType = containingType,
									isStatic = true
								};
							}
							return true;
						}
					}
					else if(symbol is IPropertySymbol) {
						var targetType = ParseType((symbol as IPropertySymbol).Type);
						var containingType = ParseType(symbol.ContainingType);
						if(!symbol.IsStatic) {
							value = new MemberData {
								Items = new[] { new MemberData.ItemData(containingType.type.Name), new MemberData.ItemData(symbol.Name) },
								instance = MemberData.This(setting.root),
								targetType = MemberData.TargetType.Property,
								type = targetType,
								startType = containingType,
								isStatic = false
							};
						}
						else {
							value = new MemberData {
								Items = new[] { new MemberData.ItemData(containingType.type.Name), new MemberData.ItemData(symbol.Name) },
								instance = null,
								targetType = MemberData.TargetType.Property,
								type = targetType,
								startType = containingType,
								isStatic = true
							};
						}
						return true;
					}
					else if(symbol is IMethodSymbol) {
						var MSymbol = symbol as IMethodSymbol;
						MemberData member = GetMethodData(MSymbol, setting);
						if(member != null) {
							value = member;
							return true;
						}
					}
					else if(symbol is IEventSymbol) {
						var targetType = ParseType((symbol as IEventSymbol).Type);
						var containingType = ParseType(symbol.ContainingType);
						if(!symbol.IsStatic) {
							value = new MemberData {
								Items = new[] { new MemberData.ItemData(containingType.type.Name), new MemberData.ItemData(symbol.Name) },
								instance = MemberData.This(setting.root),
								targetType = MemberData.TargetType.Event,
								type = targetType,
								startType = containingType,
								isStatic = false
							};
						}
						else {
							value = new MemberData {
								Items = new[] { new MemberData.ItemData(containingType.type.Name), new MemberData.ItemData(symbol.Name) },
								instance = null,
								targetType = MemberData.TargetType.Event,
								type = targetType,
								startType = containingType,
								isStatic = true
							};
						}
						return true;
					}
					else if(symbol.Kind == SymbolKind.Discard) {
						value = MemberData.None;
						return true;
					}
				}
			}
			else if(syntax is PredefinedTypeSyntax) {
				value = new MemberData(ParseType(syntax, setting.model), MemberData.TargetType.Type);
				return true;
			}
			else if(recursive) {
				var member = GetMemberFromExpression(syntax, setting);
				if(member != null) {
					value = member;
					return true;
				}
			}
			value = null;
			return false;
		}

		/// <summary>
		/// Get the MemberData from MethodSymbol
		/// </summary>
		/// <param name="symbol"></param>
		/// <param name="setting"></param>
		/// <returns></returns>
		public static MemberData GetMethodData(IMethodSymbol symbol, ParserSetting setting) {
			MemberData member = null;
			string name = symbol.ContainingType.Name.Add(".") + symbol.Name;
			bool isSource = IsInSource(symbol.Locations);
			if(isSource) {
				member = GetSymbolReferenceValue(symbol, setting);
			}
			else if(!symbol.IsStatic) {
				member = new MemberData(name, ParseType(symbol.ContainingType), MemberData.TargetType.Method, ParseType(symbol.ReturnType));
				member.instance = MemberData.This(setting.root);
			}
			else {
				member = new MemberData(name, ParseType(symbol.ContainingType), MemberData.TargetType.Method, ParseType(symbol.ReturnType));
			}
			if(member != null) {
				if(symbol.IsStatic) {
					member.isStatic = true;
				}
				MemberData.ItemData iData = new MemberData.ItemData(symbol.Name);
				if(symbol.IsGenericMethod) {
					TypeData[] param = new TypeData[symbol.TypeArguments.Length];
					for(int i = 0; i < symbol.TypeArguments.Length; i++) {
						param[i] = MemberDataUtility.GetTypeData(ParseType(symbol.TypeArguments[i]));
					}
					iData.genericArguments = param;
				}
				iData.parameters = symbol.Parameters.Select(item => GetTypeData(item, !isSource, iData.genericArguments != null ? iData.genericArguments.Select(it => it.name).ToArray() : null)).ToArray();
				member.Items[member.Items.Length - 1] = iData;
			}
			return member;
		}

		/// <summary>
		/// A Function to parse ArgumentListSyntax
		/// </summary>
		/// <param name="listSyntax"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool TryParseArgument(ArgumentListSyntax listSyntax, SemanticModel model, UGraphElement parent, out List<MemberData> value) {
			List<MemberData> list = new List<MemberData>();
			foreach(var param in listSyntax.Arguments) {
				MemberData member;
				if(!TryParseArgument(param, model, parent, out member)) {
					value = list;
					return false;
				}
				list.Add(member);
			}
			value = list;
			return true;
		}

		/// <summary>
		/// A Function to parse ArgumentSyntax.
		/// </summary>
		/// <param name="argument"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="parent"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool TryParseArgument(ArgumentSyntax argument, SemanticModel model, UGraphElement parent, out MemberData value) {
			var expression = argument.Expression;
			if(TryParseExpression(expression, model, parent, out value)) {
				return true;
			}
			value = null;
			return false;
		}

		/// <summary>
		/// Function to define node for StatementSyntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="node"></param>
		public static void DefineStatementNode(StatementSyntax syntax, Node node) {
			parserData.parsedStatements[syntax] = node;
		}

		public static bool CanInlineValue(object value) {
			if(value is Delegate) return false;
			if(value is MemberData member) {
				return member.IsTargetingValue || member.targetType == MemberData.TargetType.Constructor;
			}
			else if(value is ParsedElementInitializer initializer) {
				foreach(var element in initializer.value) {
					if(element.type.type != null && CanInlineValue(element.value) == false)
						return false;
				}
			}
			return true;
		}

		public static object GetInlineValue(object value) {
			if(value is Delegate) return null;
			if(value is MemberData member) {
				return member.Get(null);
			}
			else if(value is ParsedElementInitializer initializer) {
				var result = new ParsedElementValue();
				result.values = new object[initializer.value.Count];
				for(int i = 0; i < result.values.Length; i++) {
					result.values[i] = Operator.Convert(initializer.value[i].value.Get(null), initializer.value[i].type);
				}
				return result;
			}
			return value;
		}

		/// <summary>
		/// Check if the location is in source
		/// </summary>
		/// <param name="location"></param>
		/// <returns></returns>
		public static bool IsInSource(IEnumerable<Location> location) {
			return location.Any(item => item.IsInSource && item.Kind == LocationKind.SourceFile);
		}

		/// <summary>
		/// Check if the symbol is in source
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="symbol"></param>
		/// <param name="setting"></param>
		/// <returns></returns>
		public static bool IsInSource<T>(ISymbol symbol, ParserSetting setting) where T : SyntaxNode {
			bool isInSource = false;
			if(IsInSource(symbol.Locations)) {
				foreach(var r in symbol.DeclaringSyntaxReferences) {
					var syntax = r.GetSyntax();
					if(syntax is T) {
						var s = GetSymbol(syntax, setting.model);
						if(s != null && GetSymbolOwner(s) == setting.root) {
							isInSource = true;
							break;
						}
					}
				}
			}
			return isInSource;
		}

		/// <summary>
		/// Check if the location is in source
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public static bool IsInSource(SyntaxNode syntax, SemanticModel model) {
			if(syntax is BaseTypeDeclarationSyntax) {
				return IsInSource(model.GetDeclaredSymbol(syntax).Locations);
			}
			else if(syntax is ExpressionSyntax || syntax is StatementSyntax) {
				return IsInSource(model.GetSymbolInfo(syntax).Symbol.Locations);
			}
			throw new Exception();
		}

		/// <summary>
		/// Get the Source Component from location (WIP)
		/// </summary>
		/// <param name="location"></param>
		/// <returns></returns>
		public static Component GetSourceComponentFromLocation(IEnumerable<Location> location) {
			foreach(var loc in location) {
				if(loc.IsInSource) {
					foreach(var root in parserData.roots) {

					}
				}
			}
			return null;
		}

		/// <summary>
		/// Get TypeData from ParameterSymbol
		/// </summary>
		/// <param name="parameter"></param>
		/// <param name="genericName"></param>
		/// <returns></returns>
		public static TypeData GetTypeData(IParameterSymbol parameter, params string[] genericName) {
			string name = null;
			if(parameter.Type.TypeKind == TypeKind.TypeParameter) {
				name = "#" + MemberDataUtility.GetGenericIndex(parameter.Name, genericName);
			}
			else {
				return MemberDataUtility.GetTypeData(ParseType(parameter), genericName);
			}
			return new TypeData(name);
		}

		/// <summary>
		/// Get TypeData from ParameterSymbol
		/// </summary>
		/// <param name="parameter"></param>
		/// <param name="genericName"></param>
		/// <returns></returns>
		public static TypeData GetTypeData(IParameterSymbol parameter, bool withRefOrOut, params string[] genericName) {
			string name = null;
			if(parameter.Type.TypeKind == TypeKind.TypeParameter) {
				name = "#" + MemberDataUtility.GetGenericIndex(parameter.Name, genericName);
			}
			else {
				var type = ParseType(parameter).type;
				if(type.IsByRef && !withRefOrOut) {
					type = type.GetElementType();
				}
				return MemberDataUtility.GetTypeData(type, genericName);
			}
			return new TypeData(name);
		}

		/// <summary>
		/// Get TypeData from TypeSymbol 
		/// </summary>
		/// <param name="parameter"></param>
		/// <param name="genericName"></param>
		/// <returns></returns>
		public static TypeData GetTypeData(ITypeSymbol parameter, params string[] genericName) {
			return MemberDataUtility.GetTypeData(ParseType(parameter), genericName);
		}

		/// <summary>
		/// Function to get member from ExpressionSyntax
		/// </summary>
		/// <param name="expression"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		public static MemberData GetMemberFromExpression(ExpressionSyntax expression, SemanticModel model, UGraphElement parent) {
			return GetMemberFromExpression(expression, new ParserSetting(model, parent));
		}


		/// <summary>
		/// Function to get member from ExpressionSyntax
		/// </summary>
		/// <param name="expression"></param>
		/// <param name="setting"></param>
		/// <returns></returns>
		public static MemberData GetMemberFromExpression(ExpressionSyntax expression, ParserSetting setting) {
			List<MemberData> parameterValues;
			var result = GetMemberFromExpression(expression, setting, out parameterValues);
			if(setting.CanCreateNode && parameterValues != null && parameterValues.Count > 0) {
				result = ToValueNode(result, parameterValues, setting);
			}
			else if(result.instance is MemberData) {
				var m = result.instance as MemberData;
				if(m.targetType == MemberData.TargetType.NodePort) {//Auto convert MemberData to node on targeting output pin.
					result = ToValueNode(result, parameterValues, setting);
				}
			}
			return result;
		}

		public static T FindNode<T>(SyntaxNode node, bool recursive = true) where T : SyntaxNode {
			if(node is T) {
				return node as T;
			}
			var nodes = node.ChildNodes();
			if(nodes != null) {
				foreach(var n in nodes) {
					if(n is T) {
						return n as T;
					}
					else if(recursive) {
						var result = FindNode<T>(n);
						if(result != null) {
							return result;
						}
					}
				}
			}
			return null;
		}


		/// <summary>
		/// Function to get member from ExpressionSyntax
		/// </summary>
		/// <param name="expression"></param>
		/// <param name="setting"></param>
		/// <param name="parameterValues"></param>
		/// <returns></returns>
		public static MemberData GetMemberFromExpression(ExpressionSyntax expression, ParserSetting setting, out List<MemberData> parameterValues) {
			return GetMemberFromExpression(expression, setting, out parameterValues, out _);
		}

		/// <summary>
		/// Function to get member from ExpressionSyntax
		/// </summary>
		/// <param name="expression"></param>
		/// <param name="setting"></param>
		/// <param name="parameterValues"></param>
		/// <param name="initializers"></param>
		/// <returns></returns>
		public static MemberData GetMemberFromExpression(ExpressionSyntax expression, ParserSetting setting, out List<MemberData> parameterValues, out List<ParameterValueData> initializers) {
			var members = GetMembersDataFromExpression(expression, setting, out parameterValues, out initializers);
			if(members != null) {
				if(members.Count > 1 && !members[0].isStatic) {
					MemberData instance = null;
					while(members.Count > 1) {
						if(members[0].targetType == MemberData.TargetType.NodePort) {
							var port = members[0].Get<ValueOutput>(null);
							instance = CreateFromPort(port);
						}
						else {
							var node = CreateMultipurposeNode("node", setting.parent, members[0]);
							if(node.parameters != null && node.parameters.Count <= parameterValues?.Count) {
								for(int i = 0; i < node.parameters.Count; i++) {
									var param = parameterValues[i];
									var targetParam = node.parameters[i];
									targetParam.input.ConnectTo(param);
								}
								for(int i = 0; i < node.parameters.Count; i++) {
									parameterValues.RemoveAt(0);
								}
							}
							if(node.instance != null) {
								if(instance != null) {
									node.instance.ConnectTo(instance);
								}
								else if(node.instance.isAssigned == false) {
									if(members[0].instance is MemberData) {
										node.instance.ConnectTo(members[0].instance as MemberData);
									}
									else if(members[0].instance != null) {
										node.instance.AssignToDefault(members[0].instance);
									}
									else {
										node.instance.AssignToDefault(MemberData.This(setting.root));
									}
								}
							}
							if(node.output != null) {
								instance = CreateFromPort(node.output);
							}
						}
						members.RemoveAt(0);
					}
					if(instance != null) {
						members[0].instance = instance;
					}
					if(members.Count == 1) {
						var member = members[0];
						if(member.targetType == MemberData.TargetType.Method) {
							var memberInfos = member.GetMembers();
							if(memberInfos.Length == 0) {
								var info = member.startType.GetMember(member.startName, MemberData.flags);
								if(info.Length == 1) {
									member = MemberData.CreateFromMember(info[0]);

									if(instance != null) {
										member.instance = instance;
									}
								}
								else {
									throw null;
								}
							}
						}
						return member;
					}
				}
				if(members.Count > 1) {
					var data = new MemberData();
					List<MemberData.ItemData> items = new List<MemberData.ItemData>();
					bool flag = false;
					for(int i = 0; i < members.Count; i++) {
						var member = members[i];
						if(member == null) {
							if(i == 0)
								continue;
							throw new Exception();
						}
						if(i == 0) {
							if(member.targetType == MemberData.TargetType.NodePort) {
								data.instance = member;
								data.startType = member.startType;
								continue;
							}
							else if((member.targetType == MemberData.TargetType.Constructor)) {
								var ex = FindNode<BaseObjectCreationExpressionSyntax>(expression);
								if(ex != null) {
									MemberData m;
									if(TryParseExpression(ex, setting.model, setting.parent, out m)) {
										data.instance = m;
										data.startType = member.startType;
										data.isStatic = false;
										continue;
									}
								}
							}
							else if(member.targetType == MemberData.TargetType.Values) {
								data.instance = member;
								data.startType = member.startType;
								data.isStatic = false;
								continue;
							}
							else if(member.targetType == MemberData.TargetType.Type && members.Count > 1) {
								if(members[1].startType.IsCastableTo(typeof(MemberInfo)) &&
									members[1].targetType.HasFlags(MemberData.TargetType.Field | MemberData.TargetType.Property | MemberData.TargetType.Method)) {
									//For handling typeof(MyType).Name etc...
									data.instance = MemberData.CreateFromValue(new UPortRef(CreateMultipurposeNode("typeof()", setting.parent, members[0], null).output));
									members.RemoveAt(0);
									i--;
									continue;
								}
								else if(members.Count == 2) {
									Type type = member.startType;
									if(type != typeof(Enum) && type.IsCastableTo(typeof(Enum))) {
										if(members[1].targetType == MemberData.TargetType.Values) {
											return members[1];
										}
										return new MemberData(Enum.Parse(type, members[1].Items[members[1].Items.Length - 1].GetActualName()));
									}
								}
							}
							else if(member.targetType == MemberData.TargetType.Self && members.Count > 1) {
								//Handling base.MyMethod
								data.instance = member;
								data.isStatic = false;
								members.RemoveAt(i);
								i--;
								continue;
							}
							data.isStatic = member.isStatic;
						}
						if(!flag) {
							if(member.targetType != MemberData.TargetType.Type) {
								if(i + 1 >= members.Count) {//Last members
									if(member.targetType == MemberData.TargetType.Field && member.type.IsEnum) {//For enum values.
										if(Enum.GetNames(member.type).Contains(member.Items.Last().GetActualName())) {//To ensure the name is valid
											return new MemberData(Enum.Parse(member.type, member.Items.Last().GetActualName()));
										}
									}
								}
								if(i > 0) {
									if(members[0].IsTargetingUNode) {
										if(member.targetType == MemberData.TargetType.uNodeVariable || member.targetType == MemberData.TargetType.uNodeProperty) {
											var memberDatas = GetGraphType(expression, setting.model).GetMember(member.startName);
											if(memberDatas.Length > 0) {
												member = MemberData.CreateFromMember(memberDatas[0]);
											}
										}
									}
									else if(i > 2 && i + 1 == members.Count) {
										//For handling: SomeVariable.GetComponent<MyClass>().MyVariable
										if(members[i].IsTargetingUNode) {
											if(member.targetType == MemberData.TargetType.uNodeVariable) {
												member.targetType = MemberData.TargetType.Field;
											}
											else if(member.targetType == MemberData.TargetType.uNodeProperty) {
												member.targetType = MemberData.TargetType.Property;
											}
											else if(member.targetType == MemberData.TargetType.uNodeFunction) {
												member.targetType = MemberData.TargetType.Method;
											}
										}
									}
								}
								if(i + 1 < members.Count) {
									data.isStatic = member.isStatic || data.isStatic && data.targetType != MemberData.TargetType.Type;
								}
								data.targetType = member.targetType;
								if(!data.StartSerializedType.isFilled)
									data.startType = member.startType;
								if(data.instance == null && member.instance != null) {
									data.instance = member.instance;
								}
								if(member.IsTargetingUNode && member.targetType != MemberData.TargetType.NodePort) {
									flag = true;
								}
							}
							else {
								//For Type
								data.startType = member.startType;
								data.isStatic = member.isStatic;
							}
						}
						if(member.isDeepTarget) {
							items.AddRange(member.Items);
						}
						else {
							if(items.Count == 0) {
								items.AddRange(member.Items);
							}
							else {
								items.Add(member.Items.Last());
							}
						}
						data.type = member.type;
					}
					data.Items = items.ToArray();
					if(data.isStatic) {
						data.instance = null;
					}
					return data;
				}
				else {
					if(members.Count == 0)
						throw null;
					return members[0];
				}
			}
			return null;
		}

		/// <summary>
		/// Register port for auto proxy all connection to this port
		/// </summary>
		/// <param name="port"></param>
		public static void RegisterAutoProxyConnectionForPort(UPort port) {
			if(port == null)
				throw new ArgumentNullException(nameof(port));
			parserData.autoProxyPorts.Add(port);
		}

		/// <summary>
		/// Register owned symbol
		/// </summary>
		/// <param name="symbol"></param>
		/// <param name="owner"></param>
		/// <param name="createReference"></param>
		public static void RegisterSymbol(ISymbol symbol, object owner, Func<ParserSetting, MemberData> createReference = null, Type type = null) {
			if(symbol == null) {
				throw new ArgumentNullException("symbol");
			}
			if(!parserData.symbolMap.ContainsKey(symbol)) {
				parserData.symbolMap.Add(symbol, new Data.Symbol() {
					owner = owner,
					type = type,
					createReference = createReference,
				});
			}
		}

		/// <summary>
		/// Get the owner component of a symbol, if any.
		/// </summary>
		/// <param name="symbol"></param>
		/// <returns></returns>
		public static object GetSymbolOwner(ISymbol symbol) {
			if(symbol == null)
				return null;
			if(parserData.symbolMap.ContainsKey(symbol)) {
				return parserData.symbolMap[symbol].owner;
			}
			if(symbol is IMethodSymbol) {
				IMethodSymbol methodSymbol = symbol as IMethodSymbol;
				if(methodSymbol.IsGenericMethod && methodSymbol.OriginalDefinition != methodSymbol) {
					return GetSymbolOwner(methodSymbol.OriginalDefinition);
				}
			}
			else if(symbol is IParameterSymbol) {
				return GetSymbolOwner(symbol.ContainingSymbol);
			}
			return null;
		}

		/// <summary>
		/// Get the user data value of a symbol, if any.
		/// </summary>
		/// <param name="symbol"></param>
		/// <returns></returns>
		public static MemberData GetSymbolReferenceValue(ISymbol symbol, ParserSetting setting) {
			if(parserData.symbolMap.TryGetValue(symbol, out var data)) {
				if(data.createReference != null) {
					return data.createReference(setting);
				}
			}
			return null;
		}

		/// <summary>
		/// Get the type of a symbol, if any.
		/// </summary>
		/// <param name="symbol"></param>
		/// <returns></returns>
		public static Type GetSymbolType(ISymbol symbol) {
			if(parserData.symbolMap.ContainsKey(symbol)) {
				return parserData.symbolMap[symbol].type;
			}
			return null;
		}

		/// <summary>
		/// Get ISymbol from SyntaxNode
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public static ISymbol GetSymbol(SyntaxNode syntax, SemanticModel model) {
			if(syntax is MemberDeclarationSyntax ||
				syntax is ArgumentSyntax ||
				syntax is VariableDeclarationSyntax ||
				syntax is VariableDeclaratorSyntax ||
				syntax is AccessorDeclarationSyntax ||
				syntax is ForEachStatementSyntax ||
				syntax is TypeParameterSyntax ||
				syntax is CatchDeclarationSyntax ||
				syntax is UsingDirectiveSyntax ||
				syntax is VariableDesignationSyntax) {
				try {
					return model.GetDeclaredSymbol(syntax) ?? model.GetSymbolInfo(syntax).Symbol;
				}
				catch(Exception ex) {
					throw new Exception($"Errors getting symbol on syntax: {syntax}\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax as CSharpSyntaxNode)}", ex);
				}
			}
			else {
				var symbol = model.GetSymbolInfo(syntax);
				if(symbol.Symbol != null) {
					return symbol.Symbol;
				}
				if(symbol.CandidateSymbols != null && symbol.CandidateSymbols.Length > 0) {
					return symbol.CandidateSymbols[0];
				}
				return null;
			}
		}

		/// <summary>
		/// Get Symbol from SyntaxNode
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public static T GetSymbol<T>(SyntaxNode syntax, SemanticModel model) where T : ISymbol {
			if(syntax is MemberDeclarationSyntax || syntax is ArgumentSyntax || syntax is VariableDeclarationSyntax) {
				return (T)model.GetDeclaredSymbol(syntax);
			}
			else {
				return (T)model.GetSymbolInfo(syntax).Symbol;
			}
		}

		/// <summary>
		/// Get ITypeSymbol from SyntaxNode
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public static ITypeSymbol GetTypeSymbol(SyntaxNode syntax, SemanticModel model) {
			if(syntax is MemberDeclarationSyntax || syntax is ArgumentSyntax || syntax is VariableDeclarationSyntax) {
				var symbol = model.GetDeclaredSymbol(syntax);
				if(symbol is ITypeSymbol) {
					return syntax as ITypeSymbol;
				}
				else if(syntax is IFieldSymbol) {
					return (syntax as IFieldSymbol).Type;
				}
				else if(syntax is IPropertySymbol) {
					return (syntax as IPropertySymbol).Type;
				}
				else if(syntax is IMethodSymbol) {
					return (syntax as IMethodSymbol).ReturnType;
				}
				else if(syntax is ILocalSymbol) {
					return (syntax as ILocalSymbol).Type;
				}
				else if(syntax is IEventSymbol) {
					return (syntax as IEventSymbol).Type;
				}
				throw new Exception("Can't get ITypeSymbol from syntax:" + syntax + $"\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax as CSharpSyntaxNode)}");
			}
			var typeInfo = model.GetTypeInfo(syntax);
			return typeInfo.Type ?? typeInfo.ConvertedType;
		}

		/// <summary>
		/// Function to parse MemberInfo into MemberData
		/// </summary>
		/// <param name="members"></param>
		/// <returns></returns>
		public static MemberData ParseMemberInfo(params MemberInfo[] members) {
			return new MemberData(members);
		}

		/// <summary>
		/// Convert MemberData to value node.
		/// </summary>
		/// <param name="member"></param>
		/// <param name="parameters"></param>
		/// <param name="setting"></param>
		/// <param name="toDelegate"></param>
		/// <returns></returns>
		/// <exception cref="NullReferenceException"></exception>
		public static MemberData ToValueNode(MemberData member, IList<MemberData> parameters, ParserSetting setting, bool toDelegate = false) {
			if(member.targetType == MemberData.TargetType.NodePort) {
				return member;
			}
			if(toDelegate && (member.targetType == MemberData.TargetType.uNodeFunction || member.targetType == MemberData.TargetType.Method)) {
				var node = CreateNode<NodeDelegateFunction>("", setting.parent);
				node.member.target = member;
				node.Register();
				return CreateFromPort(node.output);
			}
			else {
				var node = CreateMultipurposeNode("node", setting.parent, member, parameters);
				if(node.output == null) {
					throw new NullReferenceException("Failed to create value node from value: " + SerializerUtility.ObjectToJSON(member));
				}
				return CreateFromPort(node.output);
			}
		}

		public static MemberData ToSupportedValue(MemberData member, ParserSetting setting) {
			if(!IsSupportInline(member)) {
				return ToValueNode(member, null, setting);
			}
			return member;
		}

		public static bool IsSupportInline(MemberData member) {
			if(member == null || member.targetType == MemberData.TargetType.NodePort)
				return true;
			if(member.IsTargetingValue == false && member.isStatic == false && member.IsTargetingGraph == false) {
				return false;
			}
			if(member.targetType == MemberData.TargetType.uNodeFunction) {
				var funcRef = member.startItem.GetReferenceValue() as Function;
				if(funcRef == null || funcRef.parameters.Count > 0) {
					return false;
				}
			}
			if(member.isDeepTarget || member.IsTargetingReflection) {
				var members = member.GetMembers(false);
				if(members != null) {
					for(int i = 0; i < members.Length; i++) {
						var method = members[i] as MethodBase;
						if(method != null) {
							//For constructors and functions
							if(method.IsGenericMethod || method.GetParameters().Length > 0) {
								return false;
							}
						}
					}
					return true;
				}
				return false;
			}
			return true;
		}

		#region ParseStatement
		/// <summary>
		/// Function to parse StatementSyntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		public static List<Node> ParseStatement(StatementSyntax syntax, SemanticModel model, UGraphElement parent) {
			return ParseStatement(syntax, model, parent, out _);
		}

		/// <summary>
		/// Function to parse StatementSyntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="parent"></param>
		/// <param name="parserResult"></param>
		/// <returns></returns>
		public static List<Node> ParseStatement(StatementSyntax syntax, SemanticModel model, UGraphElement parent, out ParserResult parserResult) {
			if(syntax == null)
				throw new ArgumentNullException("Syntax is null");
			if(model == null)
				throw new ArgumentNullException("Model is null");
			List<Node> nodes = new List<Node>();
			ParserResult lastResult = null;
			foreach(var handler in syntaxHandlerList) {
				ParserResult result;
				if(handler.StatementHandler(syntax, new ParserData() {
					model = model,
					parent = parent,
					previousResult = lastResult
				}, out result)) {
					if(lastResult != null && lastResult.node != null && lastResult.next != null &&
						(result == null || lastResult.node != result.node)) {
						lastResult.next(result.node);
						lastResult.next = null;
					}
					if(result != null && result.node != null && !(syntax is BlockSyntax)) {
						string summary = GetSummary(syntax);
						if(!string.IsNullOrEmpty(summary)) {
							result.node.nodeObject.comment = summary;
						}
					}
					lastResult = result;
					if(lastResult != null && lastResult.node != null) {
						nodes.Add(lastResult.node);
					}
					parserData.parsedStatements[syntax] = nodes != null && nodes.Count > 0 ? nodes[0] : null;
					parserResult = lastResult;
					return nodes;
				}
			}
			throw new Exception("Couldn't handle statement:" + syntax + $"\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}");
		}

		/// <summary>
		/// Function to parse StatementSyntax
		/// </summary>
		/// <param name="syntaxs"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="parent"></param>
		/// <returns></returns>
		public static List<Node> ParseStatement(IEnumerable<StatementSyntax> syntaxs, SemanticModel model, UGraphElement parent) {
			return ParseStatement(syntaxs, model, parent, out _);
		}

		/// <summary>
		/// Function to parse StatementSyntax
		/// </summary>
		/// <param name="syntaxs"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="parent"></param>
		/// <param name="parserResult"></param>
		/// <returns></returns>
		public static List<Node> ParseStatement(IEnumerable<StatementSyntax> syntaxs, SemanticModel model, UGraphElement parent, out List<ParserResult> parserResult) {
			if(syntaxs == null || model == null)
				throw new ArgumentNullException();
			List<Node> nodes = new List<Node>();
			List<ParserResult> results = new List<ParserResult>();
			ParserResult lastResult = null;
			foreach(var syntax in syntaxs) {
				bool flag = false;
				foreach(var handler in syntaxHandlerList) {
					ParserResult result;
					if(handler.StatementHandler(syntax, new ParserData() {
						model = model,
						parent = parent,
						previousResult = lastResult
					}, out result)) {
						if(result == null) {
							flag = true;
							break;
						}
						if(lastResult != null && lastResult.node != null && lastResult.next != null &&
							(result == null || lastResult.node != result.node)) {
							lastResult.next(result.node);
							lastResult.next = null;
						}
						if(result != null && result.node != null && !(syntax is BlockSyntax)) {
							string summary = GetSummary(syntax);
							if(!string.IsNullOrEmpty(summary)) {
								result.node.nodeObject.comment = summary;
							}
						}
						lastResult = result;
						results.Add(result);
						if(lastResult != null && lastResult.node != null) {
							nodes.Add(lastResult.node);
						}
						parserData.parsedStatements[syntax] = result != null && result.node != null ? result.node : null;
						flag = true;
						break;
					}
				}
				if(!flag) {
					throw new Exception("Couldn't handle statement:" + syntax + "\n" + syntax.GetType() + $"\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax)}");
				}
			}
			parserResult = results;
			return nodes;
		}

		/// <summary>
		/// Get the summary from xml
		/// </summary>
		/// <param name="xml"></param>
		/// <returns></returns>
		public static string GetSummary(string xml) {
			if(!string.IsNullOrEmpty(xml) && !xml.Trim().StartsWith("<!", StringComparison.Ordinal)) {
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.LoadXml(xml);
				var member = xmlDocument["member"];
				if(member != null) {
					var summary = member["summary"].InnerText;
					var str = summary.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
					string result = null;
					foreach(var s in str) {
						result += s.Trim().AddFirst("\n", !string.IsNullOrEmpty(result));
					}
					return result;
				}
			}
			return null;
		}

		/// <summary>
		/// Get the summary from syntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <returns></returns>
		public static string GetSummary(StatementSyntax syntax) {
			if(syntax != null) {
				var trivias = syntax.GetLeadingTrivia();
				System.Text.StringBuilder builder = new System.Text.StringBuilder();
				bool isFirst = true;
				for(int i = trivias.Count - 1; i >= 0; i--) {
					var trivia = trivias[i];
					if(trivia.Kind() == SyntaxKind.SingleLineCommentTrivia) {
						builder.Append(trivia.ToString().Remove(0, 2).Trim().AddFirst("\n", !isFirst));
						isFirst = false;
					}
					else if(trivia.Kind() == SyntaxKind.MultiLineCommentTrivia) {
						builder.Append(trivia.ToString().RemoveLast(2).Remove(0, 2).Trim().AddFirst("\n", !isFirst));
						isFirst = false;
					}
				}
				return builder.ToString();
			}
			return null;
		}

		/// <summary>
		/// Get the summary from syntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <returns></returns>
		public static string GetSummary(MemberDeclarationSyntax syntax, SemanticModel model) {
			var symbol = model.GetDeclaredSymbol(syntax);
			if(symbol != null && !string.IsNullOrEmpty(symbol.GetDocumentationCommentXml())) {
				return GetSummary(symbol.GetDocumentationCommentXml());
			}
			return null;
		}

		public static void ParseVariableDeclaration(DeclarationPatternSyntax syntax, SemanticModel model, UGraphElement parent, out Variable variable, out VariableDesignationSyntax variableSyntax) {
			if(syntax != null && model != null && parent != null) {
				var typeSymbol = model.GetSymbolInfo(syntax.Type).Symbol as ITypeSymbol;
				if(syntax.Designation is SingleVariableDesignationSyntax designationSyntax) {
					try {
						var varData = CreateElement<Variable>(designationSyntax.Identifier.ValueText, parent);
						varData.type = ParseType(typeSymbol);
						varData.resetOnEnter = true;
						variable = varData;
						variableSyntax = designationSyntax;
						return;
					}
					catch {
						Debug.LogError($"Error on parsing syntax : {syntax.ToString()}\nOn Script: {parserData.name}");
						throw;
					}
				}
			}
			throw new Exception($"Error on parsing syntax : {syntax.ToString()}\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}");
		}


		public static void ParseVariableDeclaration(DeclarationExpressionSyntax syntax, SemanticModel model, UGraphElement parent, out Variable variable, out VariableDesignationSyntax variableSyntax) {
			if(syntax != null && model != null && parent != null) {
				var typeSymbol = model.GetSymbolInfo(syntax.Type).Symbol as ITypeSymbol;
				if(syntax.Designation is SingleVariableDesignationSyntax designationSyntax) {
					try {
						var varData = CreateElement<Variable>(designationSyntax.Identifier.ValueText, parent);
						varData.type = ParseType(typeSymbol);
						varData.resetOnEnter = true;
						variable = varData;
						variableSyntax = designationSyntax;
						return;
					}
					catch {
						Debug.LogError($"Error on parsing syntax : {syntax.ToString()}\nOn Script: {parserData.name}");
						throw;
					}
				}
			}
			throw new Exception($"Error on parsing syntax : {syntax.ToString()}\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}");
		}

		/// <summary>
		/// Parse VariableDeclarationSyntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="action"></param>
		/// <param name="onParseValue"></param>
		/// <returns></returns>
		public static List<Variable> ParseVariableDeclaration(VariableDeclarationSyntax syntax,
														SemanticModel model,
														UGraphElement parent,
														Action<VariableDeclaratorSyntax, Variable> action = null,
														Func<VariableDeclaratorSyntax, Variable, bool> onParseValue = null) {
			if(syntax == null || model == null)
				return null;
			var variables = new List<Variable>();
			var typeSymbol = model.GetSymbolInfo(syntax.Type).Symbol as ITypeSymbol;
			foreach(var dec in syntax.Variables) {
				try {
					var varData = CreateElement<Variable>(dec.Identifier.ValueText, parent);
					varData.type = ParseType(typeSymbol);
					variables.Add(varData);

					if(varData.typeKind == SerializedTypeKind.GenericParameter) {
						varData.defaultValue = dec.Initializer != null && dec.Initializer.Value is DefaultExpressionSyntax ? new object() : null;
					}
					else {

						if(dec.Initializer != null) {
							MemberData val;
							if(onParseValue != null && onParseValue(dec, varData)) {

							}
							else if(TryParseExpression(dec.Initializer.Value, model, parent, out val)) {
								varData.defaultValue = val.Get(null);
							}
							else {
								throw new Exception("Can't parse variable:" + varData.name + " values");
							}
						}
						else if(varData.type.IsValueType) {
							varData.defaultValue = ReflectionUtils.CreateInstance(varData.type);
						}
					}
					if(action != null) {
						action(dec, varData);
					}
				}
				catch {
					Debug.LogError($"Error on parsing syntax : {dec.ToString()}\nSyntax Type: {syntax?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax)}");
					throw;
				}
			}
			return variables;
		}


		public static List<CacheNode> ParseLocalVariableDeclaration(VariableDeclarationSyntax syntax,
														SemanticModel model,
														UGraphElement parent,
														Action<VariableDeclaratorSyntax, CacheNode> action = null,
														Func<VariableDeclaratorSyntax, CacheNode, bool> onParseValue = null) {
			if(syntax == null || model == null)
				return null;
			var nodes = new List<CacheNode>();
			var typeSymbol = model.GetSymbolInfo(syntax.Type).Symbol as ITypeSymbol;
			foreach(var dec in syntax.Variables) {
				try {
					var varData = CreateNode<CacheNode>(dec.Identifier.ValueText, parent);
					varData.type = ParseType(typeSymbol);
					varData.Register();
					nodes.Add(varData);

					if(varData.type.typeKind == SerializedTypeKind.GenericParameter) {
						//TODO: fix me
						//varData.defaultValue = dec.Initializer != null && dec.Initializer.Value is DefaultExpressionSyntax ? new object() : null;
					}
					else {

						if(dec.Initializer != null) {
							MemberData val;
							if(onParseValue != null && onParseValue(dec, varData)) {

							}
							else if(TryParseExpression(dec.Initializer.Value, model, parent, out val)) {
								varData.target.AssignToDefault(val);
							}
							else {
								throw new Exception("Can't parse variable:" + varData.name + " values");
							}
						}
						else {
							varData.target.AssignToDefault(MemberData.CreateValueFromType(varData.type));
						}
					}
					if(action != null) {
						action(dec, varData);
					}
				}
				catch {
					Debug.LogError($"Error on parsing syntax : {dec.ToString()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(syntax)}");
					throw;
				}
			}
			return nodes;
		}

		/// <summary>
		/// Parse FieldDeclarationSyntax
		/// </summary>
		/// <param name="syntax"></param>
		/// <param name="model"></param>
		/// <param name="root"></param>
		/// <param name="action"></param>
		/// <returns></returns>
		public static List<Variable> ParseFieldDeclaration(BaseFieldDeclarationSyntax syntax,
													 SemanticModel model,
													 UGraphElement root,
													 Action<VariableDeclaratorSyntax, Variable> action = null) {
			var variables = ParseVariableDeclaration(syntax.Declaration, model, root, action);
			var attributes = ParseAttribute(syntax.AttributeLists, model, root);
			var summary = GetSummary(syntax, model);
			foreach(var variable in variables) {
				variable.modifier.Public = false;
				foreach(var token in syntax.Modifiers) {
					switch(token.Kind()) {
						case SyntaxKind.PublicKeyword:
							variable.modifier.Public = true;
							break;
						case SyntaxKind.PrivateKeyword:
							variable.modifier.Private = true;
							break;
						case SyntaxKind.ProtectedKeyword:
							variable.modifier.Protected = true;
							break;
						case SyntaxKind.InternalKeyword:
							variable.modifier.Internal = true;
							break;
						case SyntaxKind.ConstKeyword:
							variable.modifier.Const = true;
							break;
						case SyntaxKind.EventKeyword:
							variable.modifier.Event = true;
							break;
						case SyntaxKind.ReadOnlyKeyword:
							variable.modifier.ReadOnly = true;
							break;
						case SyntaxKind.StaticKeyword:
							variable.modifier.Static = true;
							break;
					}
				}
				if(syntax is EventFieldDeclarationSyntax) {
					variable.modifier.Event = true;
				}
				variable.comment = summary;
				variable.attributes = attributes;
			}
			return variables;
		}

		/// <summary>
		/// Return true when type are allowed to editable by the default editor.
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		public static bool AllowDirrectEdit(Type type) {
			if(allowedValueTargetTypes.Contains(type) || type.IsEnum) {
				return true;
			}
			return false;
		}
		#endregion

		#region Private Functions
		private static void ParseFieldDeclaration(BaseFieldDeclarationSyntax field, SemanticModel model, RootData root) {
			var variables = ParseFieldDeclaration(field, model, root.graphData.variableContainer, (dec, var) => {
				RegisterSymbol(GetSymbol(dec, model), root.graph, data => data.root == root.graph ? MemberData.CreateFromValue(var) : null);
			});
			foreach(var variable in variables) {
				root.variables.Add(new KeyValuePair<BaseFieldDeclarationSyntax, Variable>(field, variable));
			}
		}

		private static void ParsePropertyDeclaration(PropertyDeclarationSyntax property, SemanticModel model, RootData root) {
			var prop = CreateElement<Property>(property.Identifier.ValueText, root.graphData.propertyContainer);
			var symbol = GetSymbol(property, model) as IPropertySymbol;
			if(symbol != null) {
				prop.type = ParseType(symbol.Type);
				RegisterSymbol(symbol, root.graph, data => data.root == root.graph ? MemberData.CreateFromValue(prop) : null);
			}
			var summary = GetSummary(property, model);
			prop.comment = summary;
			prop.modifier.Public = false;
			foreach(var token in property.Modifiers) {
				switch(token.Kind()) {
					case SyntaxKind.PublicKeyword:
						prop.modifier.Public = true;
						break;
					case SyntaxKind.PrivateKeyword:
						prop.modifier.Private = true;
						break;
					case SyntaxKind.ProtectedKeyword:
						prop.modifier.Protected = true;
						break;
					case SyntaxKind.InternalKeyword:
						prop.modifier.Internal = true;
						break;
					case SyntaxKind.StaticKeyword:
						prop.modifier.Static = true;
						break;
					case SyntaxKind.AbstractKeyword:
						prop.modifier.Abstract = true;
						break;
					case SyntaxKind.VirtualKeyword:
						prop.modifier.Virtual = true;
						break;
					case SyntaxKind.OverrideKeyword:
						prop.modifier.Override = true;
						break;
				}
			}
			if(property.AccessorList != null) {
				AccessorDeclarationSyntax getAccessor = null;
				AccessorDeclarationSyntax setAccessor = null;
				foreach(var accessor in property.AccessorList.Accessors) {
					if(accessor.Kind() == SyntaxKind.GetAccessorDeclaration) {
						getAccessor = accessor;
					}
					else if(accessor.Kind() == SyntaxKind.SetAccessorDeclaration) {
						setAccessor = accessor;
					}
				}
				if(getAccessor != null && (getAccessor.Body != null || getAccessor.ExpressionBody != null)) {
					var obj = CreateElement<Function>("Getter", prop);
					obj.returnType = prop.type;
					prop.getRoot = obj;
					RegisterSymbol(symbol.GetMethod, obj, data => data.root == root.graph ? MemberData.CreateFromValue(prop) : null);
				}
				if(setAccessor != null && (setAccessor.Body != null || setAccessor.ExpressionBody != null)) {
					var obj = CreateElement<Function>("Setter", prop);
					obj.parameters = new List<ParameterData>() { new ParameterData("value", ParseType(symbol.Type)) };
					obj.returnType = typeof(void);
					prop.setRoot = obj;
					RegisterSymbol(symbol.SetMethod, obj, data => data.root == root.graph ? MemberData.CreateFromValue(prop) : null);
					RegisterSymbol(symbol.SetMethod.Parameters[0], obj, data => data.root == root.graph ? MemberData.CreateFromValue(new ParameterRef(obj, obj.parameters[0])) : null);
				}
			}
			else if(property.ExpressionBody != null) {
				var obj = CreateElement<Function>("Getter", prop);
				RegisterSymbol(symbol.GetMethod, obj, data => data.root == root.graph ? MemberData.CreateFromValue(prop) : null);
				obj.returnType = prop.type;
				prop.getRoot = obj;
			}
			var attributes = ParseAttribute(property.AttributeLists, model, root.graphData);
			prop.attributes = attributes;
			root.properties.Add(new KeyValuePair<PropertyDeclarationSyntax, Property>(property, prop));
		}

		private static void ParseConstructorDeclaration(ConstructorDeclarationSyntax constructor, SemanticModel model, RootData root) {
			var ctor = CreateElement<Constructor>(constructor.Identifier.ValueText, root.graph.GraphData.constructorContainer);
			ctor.name = constructor.Identifier.ValueText;
			var symbol = GetSymbol(constructor, model) as IMethodSymbol;
			if(symbol != null) {
				foreach(var p in symbol.Parameters) {
					var data = new ParameterData() { name = p.Name, type = ParseType(p) };
					ctor.parameters.Add(data);
					RegisterSymbol(p, ctor, d => d.root == root.graph ? MemberData.CreateFromValue(new ParameterRef(ctor, data)) : null);
				}
				RegisterSymbol(symbol, root.graph, _ => MemberData.CreateFromValue(ctor));
			}
			var summary = GetSummary(constructor, model);
			ctor.comment = summary;
			ctor.modifier.Public = false;
			foreach(var token in constructor.Modifiers) {
				switch(token.Kind()) {
					case SyntaxKind.PublicKeyword:
						ctor.modifier.Public = true;
						break;
					case SyntaxKind.PrivateKeyword:
						ctor.modifier.Private = true;
						break;
					case SyntaxKind.ProtectedKeyword:
						ctor.modifier.Protected = true;
						break;
					case SyntaxKind.InternalKeyword:
						ctor.modifier.Internal = true;
						break;
				}
			}
			root.constructors.Add(new KeyValuePair<ConstructorDeclarationSyntax, Constructor>(constructor, ctor));
		}

		private static void ParseMethodDeclaration(MethodDeclarationSyntax method, SemanticModel model, RootData root) {
			var func = CreateElement<Function>(method.Identifier.ValueText, root.graph.GraphData.functionContainer);
			func.name = method.Identifier.ValueText;
			var symbol = GetSymbol(method, model) as IMethodSymbol;
			if(symbol != null) {
				var summary = GetSummary(method, model);
				func.comment = summary;
				if(symbol.IsGenericMethod) {
					foreach(var p in symbol.TypeArguments) {
						//TODO: add support for generic
						throw new NotSupportedException("Generic Method currently is not supported");
						//uNodeUtility.AddArray(ref func.genericParameters, new GenericParameterData(p.Name));
						//RegisterSymbol(p, func);
					}
					foreach(var clause in method.ConstraintClauses) {
						var n = clause.Name.ToString();
						foreach(var cons in clause.Constraints) {
							if(cons is TypeConstraintSyntax) {
								var TCS = cons as TypeConstraintSyntax;
								foreach(var p in func.genericParameters) {
									if(p.name.Equals(n)) {
										p.typeConstraint = ParseType(GetTypeSymbol(TCS.Type, model));
										break;
									}
								}
							}
							else {
								throw new Exception("Unsupported syntax : " + cons.GetType().FullName + $"\nOn Script: {parserData.name}\nAt line: {GetLinePosition(method)}");
							}
							break;
						}
					}
				}
				foreach(var p in symbol.Parameters) {
					var type = ParseType(p).type;
					var refKind = RefKind.None;
					if(p.RefKind == Microsoft.CodeAnalysis.RefKind.Ref) {
						refKind = RefKind.Ref;
						if(type.IsByRef) {
							type = type.GetElementType();
						}
					}
					else if(p.RefKind == Microsoft.CodeAnalysis.RefKind.Out) {
						refKind = RefKind.Out;
						if(type.IsByRef) {
							type = type.GetElementType();
						}
					}
					else if(p.RefKind == Microsoft.CodeAnalysis.RefKind.In) {
						refKind = RefKind.In;
						if(type.IsByRef) {
							type = type.GetElementType();
						}
					}
					var pdata = new ParameterData() { name = p.Name, type = type, refKind = refKind };
					if(p.HasExplicitDefaultValue) {
						pdata.hasDefaultValue = true;
						pdata.defaultValue = p.ExplicitDefaultValue;
					}
					func.parameters.Add(pdata);
					RegisterSymbol(p, func, _ => MemberData.CreateFromValue(new ParameterRef(func, pdata)));
				}
				func.returnType = ParseType(symbol.ReturnType);
				RegisterSymbol(symbol, root.graph, _ => MemberData.CreateFromValue(func));
			}
			func.modifier.Public = false;
			foreach(var token in method.Modifiers) {
				switch(token.Kind()) {
					case SyntaxKind.PublicKeyword:
						func.modifier.Public = true;
						break;
					case SyntaxKind.PrivateKeyword:
						func.modifier.Private = true;
						break;
					case SyntaxKind.ProtectedKeyword:
						func.modifier.Protected = true;
						break;
					case SyntaxKind.InternalKeyword:
						func.modifier.Internal = true;
						break;
					case SyntaxKind.StaticKeyword:
						func.modifier.Static = true;
						break;
					case SyntaxKind.AbstractKeyword:
						func.modifier.Abstract = true;
						break;
					case SyntaxKind.VirtualKeyword:
						func.modifier.Virtual = true;
						break;
					case SyntaxKind.OverrideKeyword:
						func.modifier.Override = true;
						break;
					case SyntaxKind.NewKeyword:
						func.modifier.New = true;
						break;
					case SyntaxKind.SealedKeyword:
						func.modifier.Sealed = true;
						break;
					case SyntaxKind.PartialKeyword:
						func.modifier.Partial = true;
						break;
					case SyntaxKind.AsyncKeyword:
						func.modifier.Async = true;
						break;
					case SyntaxKind.UnsafeKeyword:
						func.modifier.Unsafe = true;
						break;
				}
			}
			var attributes = ParseAttribute(method.AttributeLists, model, root.graphData);
			func.attributes = attributes;
			root.methods.Add(new KeyValuePair<MethodDeclarationSyntax, Function>(method, func));
		}

		private static Action ParseMember(MemberDeclarationSyntax member, SemanticModel model, ScriptGraph scriptGraph, RootData root = null) {
			if(member is NamespaceDeclarationSyntax) {
				var syntax = member as NamespaceDeclarationSyntax;
				Action action = null;
				foreach(var m in syntax.Members) {
					action += ParseMember(m, model, scriptGraph);
				}
				scriptGraph.Namespace = syntax.Name.ToString();
				return action;
			}
			else if(member is ClassDeclarationSyntax) {
				if(root != null) {
					//TODO: add support for nested type
					throw new System.Exception("Nested Class is currently not supported");
					//return () => {//Handle nested types.
					//	if(root.root.NestedClass == null) {
					//		var go = new GameObject(gameObject.name + "-Nested");
					//		ParseNestedDeclaration(member, model, go);
					//		var nestedData = go.GetComponent<uNodeData>();
					//		if(nestedData == null) {
					//			nestedData = go.AddComponent<uNodeData>();
					//		}
					//		root.root.NestedClass = nestedData;
					//	} else {
					//		ParseNestedDeclaration(member, model, root.root.NestedClass.gameObject);
					//	}
					//};
				}
				var syntax = member as ClassDeclarationSyntax;
				var symbol = model.GetDeclaredSymbol(syntax);
				var type = ParseType(symbol.BaseType);

				var classScript = ScriptableObject.CreateInstance<ClassScript>();
				classScript.name = syntax.Identifier.ValueText;
				scriptGraph.TypeList.AddType(classScript, scriptGraph);
				if(type != null && type != typeof(object)) {
					classScript.inheritType = type;
				}
				var interfaces = symbol.Interfaces.Select(item => new SerializedType(ParseType(item))).ToList();
				if(interfaces != null && interfaces.Count > 0) {
					classScript.interfaces = interfaces;
				}
				if(symbol.IsGenericType) {
					//TODO: add support for generic type
					throw new NotImplementedException("Generic Type is currently not supported");
					//foreach(var p in symbol.TypeArguments) {
					//	RegisterSymbol(p, uNode);
					//	var GP = uNode.GenericParameters;
					//	uNodeUtility.AddList(ref GP, new GenericParameterData(p.Name));
					//	uNode.GenericParameters = GP;
					//}

					//foreach(var clause in syntax.ConstraintClauses) {
					//	var n = clause.Name.ToString();
					//	foreach(var cons in clause.Constraints) {
					//		if(cons is TypeConstraintSyntax) {
					//			var TCS = cons as TypeConstraintSyntax;
					//			foreach(var p in uNode.GenericParameters) {
					//				if(p.name.Equals(n)) {
					//					p.typeConstraint = ParseType(GetTypeSymbol(TCS.Type, model));
					//					break;
					//				}
					//			}
					//		} else {
					//			throw new Exception("Unsupported syntax : " + cons.GetType().FullName + $"\nOn Script: { parserData.name }\nAt line: {GetLinePosition(member)}");
					//		}
					//		break;
					//	}
					//}
				}
				var summary = GetSummary(symbol.GetDocumentationCommentXml());
				classScript.GraphData.comment = summary;
				classScript.modifier.Public = false;
				ParseTypeDeclarationSyntax(syntax, classScript, model);
				var data = new RootData(member, model, classScript);
				parserData.roots.Add(data);
				Action action = null;
				foreach(var m in syntax.Members) {
					action += ParseMember(m, model, scriptGraph, data);
				}
				return action;
			}
			else if(member is StructDeclarationSyntax) {
				if(root != null) {
					return () => {//Handle nested types.
								  //TODO: add support for nested type
						throw new System.Exception("Nested Class is currently not supported");

						//if(root.root.NestedClass == null) {
						//	var go = new GameObject(gameObject.name + "-Nested");
						//	ParseNestedDeclaration(member, model, go);
						//	var nestedData = go.GetComponent<uNodeData>();
						//	if(nestedData == null) {
						//		nestedData = go.AddComponent<uNodeData>();
						//	}
						//	root.root.NestedClass = nestedData;
						//} else {
						//	ParseNestedDeclaration(member, model, root.root.NestedClass.gameObject);
						//}
					};
					//throw new System.Exception("Nested Struct is not supported");
				}
				var syntax = member as StructDeclarationSyntax;

				var classScript = ScriptableObject.CreateInstance<ClassScript>();
				classScript.name = syntax.Identifier.ValueText;
				classScript.inheritType = typeof(ValueType);
				scriptGraph.TypeList.AddType(classScript, scriptGraph);

				var symbol = model.GetDeclaredSymbol(syntax);
				var interfaces = symbol.Interfaces.Select(item => new SerializedType(ParseType(item))).ToList();
				if(interfaces != null && interfaces.Count > 0) {
					classScript.interfaces = interfaces;
				}
				if(symbol.IsGenericType) {
					//TODO: add support for generic type
					throw new NotImplementedException("Generic Type is currently not supported");
					//foreach(var p in symbol.TypeArguments) {
					//	RegisterSymbol(p, uNode);
					//	var GP = uNode.GenericParameters;
					//	uNodeUtility.AddList(ref GP, new GenericParameterData(p.Name));
					//	uNode.GenericParameters = GP;
					//}
					//foreach(var clause in syntax.ConstraintClauses) {
					//	var n = clause.Name.ToString();
					//	foreach(var cons in clause.Constraints) {
					//		if(cons is TypeConstraintSyntax) {
					//			var TCS = cons as TypeConstraintSyntax;
					//			foreach(var p in uNode.GenericParameters) {
					//				if(p.name.Equals(n)) {
					//					p.typeConstraint = ParseType(GetTypeSymbol(TCS.Type, model));
					//					break;
					//				}
					//			}
					//		} else {
					//			throw new Exception("Unsupported syntax : " + cons.GetType().FullName + $"\nOn Script: { parserData.name }\nAt line: {GetLinePosition(member)}");
					//		}
					//		break;
					//	}
					//}
				}
				var summary = GetSummary(symbol.GetDocumentationCommentXml());
				classScript.GraphData.comment = summary;
				classScript.modifier.Public = false;
				ParseTypeDeclarationSyntax(syntax, classScript, model);
				var data = new RootData(member, model, classScript);
				parserData.roots.Add(data);
				Action action = null;
				foreach(var m in syntax.Members) {
					action += ParseMember(m, model, scriptGraph, data);
				}
				return action;
			}
			else if(member is InterfaceDeclarationSyntax) {
				#region Interfaces
				if(root != null) {
					return () => {//Handle nested types.
								  //TODO: add support for nested type
						throw new System.Exception("Nested Class is currently not supported");
						//if(root.root.NestedClass == null) {
						//	var go = new GameObject(gameObject.name + "-Nested");
						//	ParseNestedDeclaration(member, model, go);
						//	var nestedData = go.GetComponent<uNodeData>();
						//	if(nestedData == null) {
						//		nestedData = go.AddComponent<uNodeData>();
						//	}
						//	root.root.NestedClass = nestedData;
						//} else {
						//	ParseNestedDeclaration(member, model, root.root.NestedClass.gameObject);
						//}
					};
					//throw new System.Exception("Nested Interface is not supported");
				}
				InterfaceDeclarationSyntax syntax = member as InterfaceDeclarationSyntax;

				var interfaceData = ScriptableObject.CreateInstance<InterfaceScript>();
				scriptGraph.TypeList.AddType(interfaceData, scriptGraph);

				var symbol = GetSymbol(syntax, model) as INamedTypeSymbol;
				interfaceData.name = symbol.Name;
				interfaceData.GraphData.comment = GetSummary(symbol.GetDocumentationCommentXml());
				interfaceData.modifier.Public = false;
				foreach(var token in syntax.Modifiers) {
					switch(token.Kind()) {
						case SyntaxKind.PublicKeyword:
							interfaceData.modifier.Public = true;
							break;
						case SyntaxKind.PrivateKeyword:
							interfaceData.modifier.Private = true;
							break;
						case SyntaxKind.ProtectedKeyword:
							interfaceData.modifier.Protected = true;
							break;
						case SyntaxKind.InternalKeyword:
							interfaceData.modifier.Internal = true;
							break;
					}
				}
				var graphData = interfaceData.GraphData;
				foreach(var m in syntax.Members) {
					if(m is PropertyDeclarationSyntax) {
						var property = m as PropertyDeclarationSyntax;
						var prop = CreateElement<Property>(property.Identifier.ValueText, graphData.propertyContainer);
						var mSymbol = GetSymbol(property, model) as IPropertySymbol;
						if(mSymbol != null) {
							prop.type = ParseType(mSymbol.Type);
						}
						AccessorDeclarationSyntax getAccessor = null;
						AccessorDeclarationSyntax setAccessor = null;
						foreach(var accessor in property.AccessorList.Accessors) {
							if(accessor.Kind() == SyntaxKind.GetAccessorDeclaration) {
								getAccessor = accessor;
							}
							else if(accessor.Kind() == SyntaxKind.SetAccessorDeclaration) {
								setAccessor = accessor;
							}
						}
						if(getAccessor != null && setAccessor != null) {
							prop.accessor = PropertyAccessorKind.ReadWrite;
						}
						else if(getAccessor != null) {
							prop.accessor = PropertyAccessorKind.ReadOnly;
						}
						else if(setAccessor != null) {
							prop.accessor = PropertyAccessorKind.WriteOnly;
						}
					}
					else if(m is MethodDeclarationSyntax) {
						var method = m as MethodDeclarationSyntax;
						var func = CreateElement<Function>(method.Identifier.ValueText, graphData.functionContainer);
						var mSymbol = GetSymbol(method, model) as IMethodSymbol;
						if(mSymbol != null) {
							var summary = GetSummary(method, model);
							func.comment = summary;
							if(mSymbol.IsGenericMethod) {
								foreach(var p in mSymbol.TypeArguments) {
									uNodeUtility.AddArray(ref func.genericParameters, new GenericParameterData(p.Name));
								}
								foreach(var clause in method.ConstraintClauses) {
									var n = clause.Name.ToString();
									foreach(var cons in clause.Constraints) {
										if(cons is TypeConstraintSyntax) {
											var TCS = cons as TypeConstraintSyntax;
											foreach(var p in func.genericParameters) {
												if(p.name.Equals(n)) {
													p.typeConstraint = ParseType(GetTypeSymbol(TCS.Type, model));
													break;
												}
											}
										}
										else {
											throw new Exception("Unsupported syntax : " + cons.GetType().FullName);
										}
										break;
									}
								}
							}
							foreach(var p in mSymbol.Parameters) {
								var refKind = RefKind.None;
								if(p.RefKind == Microsoft.CodeAnalysis.RefKind.Ref) {
									refKind = RefKind.Ref;
								}
								else if(p.RefKind == Microsoft.CodeAnalysis.RefKind.Out) {
									refKind = RefKind.Out;
								}
								else if(p.RefKind == Microsoft.CodeAnalysis.RefKind.In) {
									refKind = RefKind.In;
								}
								func.parameters.Add(new ParameterData() { name = p.Name, type = ParseType(p), refKind = refKind });
							}
							func.returnType = ParseType(mSymbol.ReturnType);
						}
					}
					else {
						throw new Exception("Unsupported member declaration syntax:" + m);
					}
				}


				var data = new RootData(member, model, interfaceData);
				parserData.roots.Add(data);
				#endregion
			}
			else if(member is DelegateDeclarationSyntax) {
				throw new Exception("Delegate is not supported");
			}
			else if(member is EnumDeclarationSyntax) {
				if(root != null) {
					return () => {//Handle nested types.
								  //TODO: add support for nested type
						throw new System.Exception("Nested Class is currently not supported");
						//if(root.root.NestedClass == null) {
						//	var go = new GameObject(gameObject.name + "-Nested");
						//	ParseNestedDeclaration(member, model, go);
						//	var nestedData = go.GetComponent<uNodeData>();
						//	if(nestedData == null) {
						//		nestedData = go.AddComponent<uNodeData>();
						//	}
						//	root.root.NestedClass = nestedData;
						//} else {
						//	ParseNestedDeclaration(member, model, root.root.NestedClass.gameObject);
						//}
					};
					//throw new System.Exception("Nested Enum is not supported");
				}
				EnumDeclarationSyntax syntax = member as EnumDeclarationSyntax;

				var enumData = ScriptableObject.CreateInstance<EnumScript>();
				scriptGraph.TypeList.AddType(enumData, scriptGraph);

				var symbol = GetSymbol(syntax, model) as INamedTypeSymbol;
				enumData.name = symbol.Name;
				if(symbol.EnumUnderlyingType != null) {
					//TODO: add implicit enum type support
					//enumData.inheritFrom = ParseType(symbol.EnumUnderlyingType);
				}
				foreach(var m in syntax.Members) {
					enumData.enumerators.Add(new EnumScript.Enumerator() {
						name = GetSymbol(m, model).Name
					});
				}

				var data = new RootData(member, model, enumData);
				parserData.roots.Add(data);
			}
			else if(member is FieldDeclarationSyntax) {
				if(root == null)
					throw new System.Exception();
				ParseFieldDeclaration(member as BaseFieldDeclarationSyntax, model, root);
			}
			else if(member is EventFieldDeclarationSyntax) {
				if(root == null)
					throw new System.Exception();
				ParseFieldDeclaration(member as BaseFieldDeclarationSyntax, model, root);
			}
			else if(member is PropertyDeclarationSyntax) {
				if(root == null)
					throw new System.Exception();
				ParsePropertyDeclaration(member as PropertyDeclarationSyntax, model, root);
			}
			else if(member is MethodDeclarationSyntax) {
				if(root == null)
					throw new System.Exception();
				ParseMethodDeclaration(member as MethodDeclarationSyntax, model, root);
			}
			else if(member is ConstructorDeclarationSyntax) {
				if(root == null)
					throw new Exception();
				ParseConstructorDeclaration(member as ConstructorDeclarationSyntax, model, root);
			}
			else if(member is IndexerDeclarationSyntax) {
				throw new Exception("Indexer is not supported");
			}
			else if(member is OperatorDeclarationSyntax) {
				throw new Exception("Operator is not supported");
			}
			else if(member is DestructorDeclarationSyntax) {
				throw new Exception("Destructor is not supported");
			}
			else if(member is ConversionOperatorDeclarationSyntax) {
				Debug.Log("ConversionOperatorDeclarationSyntax is not supported, it will be skipped.");
			}
			else {
				throw new Exception(member.GetType().Name + " is not supported");
			}
			return null;
		}

		public static MemberData ParseNameOfSyntax(InvocationExpressionSyntax syntax) {
			if(syntax.Expression is IdentifierNameSyntax nameSyntax && nameSyntax.Identifier.Text == "nameof") {
				return MemberData.CreateFromValue(syntax.ArgumentList.Arguments.Last().GetLastToken().Text);
			}
			return null;
		}

		private static List<MemberData> GetMembersDataFromExpression(ExpressionSyntax expression, ParserSetting setting, out List<MemberData> parameterValues, out List<ParameterValueData> initializers) {
			if(expression == null) {
				throw new ArgumentNullException("expression");
			}
			parameterValues = null;
			initializers = null;
			List<MemberData> members = new List<MemberData>();
			if(expression is InvocationExpressionSyntax) {//Methods
				List<MemberData> parameters = null;
				var ex = expression as InvocationExpressionSyntax;
				var symbol = GetSymbol(ex, setting.model) as IMethodSymbol;
				if(symbol != null) {
					if(symbol.MethodKind != MethodKind.Ordinary && symbol.MethodKind != MethodKind.DelegateInvoke &&
						symbol.MethodKind != MethodKind.ReducedExtension) {
						throw new Exception("Couldn't handle expression:" + ex + $"\nSyntax Type: {ex?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
					}
				}
				if(ex.Expression is MemberAccessExpressionSyntax) {
					MemberAccessExpressionSyntax mae = ex.Expression as MemberAccessExpressionSyntax;
					if(symbol == null) {
						throw new Exception("Couldn't found symbol for syntax:" + ex + "\nMaybe the modifier is private, protected, or internal?\nPlease change the modifier to public and try parse again." + $"\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
					}
					if(symbol.MethodKind == MethodKind.ReducedExtension) {
						parameters = new List<MemberData>();
						var m = ParseExpression(mae.Expression, setting);
						parameters.Add(m);
					}
					else {
						bool valid = true;
						if((ex.Expression as MemberAccessExpressionSyntax).Expression is ThisExpressionSyntax) {
							if(!IsInSource<MethodDeclarationSyntax>(symbol, setting)) {
								valid = false;
							}
						}
						if(valid) {
							var m = GetMembersDataFromExpression(ex.Expression, setting, out parameters, out initializers);
							if(m != null && m.Count > 0) {
								members.AddRange(m);
								members.RemoveAt(members.Count - 1);
							}
						}
					}
					if(mae.Name is NameSyntax) {
						var nameSymbol = GetSymbol(mae.Name, setting.model);
						if(nameSymbol != null && (nameSymbol.Kind == SymbolKind.Field || nameSymbol.Kind == SymbolKind.Event || nameSymbol.Kind == SymbolKind.Local)) {//Handle field invoke.
							members.Add(ParseExpression(mae.Name, setting));
						}
						else if(ex.Expression is IdentifierNameSyntax nameSyntax) {
							if(nameSyntax.Identifier.Text == "nameof") {
								var nameofMember = ParseNameOfSyntax(ex);
								if(nameofMember != null) {
									members.Add(nameofMember);
									return members;
								}
							}
						}
					}
				}
				else if(ex.Expression is NameSyntax) {
					var nameSymbol = GetSymbol(ex.Expression, setting.model);
					if(nameSymbol != null && (nameSymbol.Kind == SymbolKind.Field || nameSymbol.Kind == SymbolKind.Event || nameSymbol.Kind == SymbolKind.Local)) {//Handle field invoke.
						members.Add(ParseExpression(ex.Expression, setting));
					}
					else if(ex.Expression is IdentifierNameSyntax nameSyntax) {
						if(nameSyntax.Identifier.Text == "nameof") {
							var nameofMember = ParseNameOfSyntax(ex);
							if(nameofMember != null) {
								members.Add(nameofMember);
								return members;
							}
						}
					}
				}
				MemberData member;
				string name = symbol.ContainingType.Name.Add(".") + symbol.Name;
				bool isSource = IsInSource<MethodDeclarationSyntax>(symbol, setting);
				if(isSource) {
					member = GetSymbolReferenceValue(symbol, setting);
				}
				else if(!symbol.IsStatic && symbol.MethodKind != MethodKind.ReducedExtension) {
					member = new MemberData(name, ParseType(symbol.ContainingType), MemberData.TargetType.Method, ParseType(symbol.ReturnType));
					if(members.Count == 0) {
						if(ex.Expression is MemberAccessExpressionSyntax) {
							member.instance = ParseExpression((ex.Expression as MemberAccessExpressionSyntax).Expression, setting);
						}
						else {
							member.instance = MemberData.This(setting.root);
						}
					}
					else {
						member.instance = MemberData.This(setting.root);
					}
				}
				else {
					member = new MemberData(name, ParseType(symbol.ContainingType), MemberData.TargetType.Method, ParseType(symbol.ReturnType));
				}
				if(member != null) {
					if(symbol.IsStatic || symbol.MethodKind == MethodKind.ReducedExtension) {
						member.isStatic = true;
					}
					if(parameters == null) {
						parameters = new List<MemberData>();
					}
					MemberData.ItemData iData = new MemberData.ItemData(symbol.Name);
					if(symbol.IsGenericMethod) {
						TypeData[] param = new TypeData[symbol.TypeArguments.Length];
						for(int i = 0; i < symbol.TypeArguments.Length; i++) {
							param[i] = MemberDataUtility.GetTypeData(ParseType(symbol.TypeArguments[i]));
						}
						iData.genericArguments = param;
					}
					IParameterSymbol paramsSymbol = null;
					int parameterLength = symbol.Parameters.Length;
					for(int x = 0; x < ex.ArgumentList.Arguments.Count; x++) {
						var param = ex.ArgumentList.Arguments[x];
						if(x + 1 >= parameterLength) {//Handle params keyword
							if(paramsSymbol == null && symbol.Parameters[parameterLength - 1].IsParams) {
								paramsSymbol = symbol.Parameters[parameterLength - 1];
							}
							var typeSymbol = GetTypeSymbol(param.Expression, setting.model);
							if(typeSymbol != null && paramsSymbol != null && !ParseType(typeSymbol).type.IsArray) {
								MemberData p;
								if(TryParseExpression(param.Expression, setting, out p)) {
									if(p.targetType == MemberData.TargetType.Values) {
										if(setting.CanCreateNode) {
											var node = CreateNode<MakeArrayNode>("ArrayCreation", setting.parent);
											node.elements[0].port.ConnectTo(p);
											node.elementType = ParseType(typeSymbol);
											parameters.Add(MemberData.CreateFromValue(new UPortRef(node.output)));
										}
										else {
											parameters.Add(new MemberData(ReflectionUtils.CreateInstance(ParseType(typeSymbol), p.Get(null))));
										}
									}
									else {
										parameters.Add(p);
									}
								}
								else {
									throw new Exception("Couldn't handle argument:" + param + $"\nSyntax Type: {param?.GetType()}\nAt line: {GetLinePosition(expression)}");
								}
								continue;
							}
						}
						MemberData m;
						if(TryParseExpression(param.Expression, setting, out m)) {
							parameters.Add(m);
						}
						else {
							throw new Exception("Couldn't handle argument:" + param + $"\nSyntax Type: {param?.GetType()}\nAt line: {GetLinePosition(expression)}");
						}
					}

					//if(paramsData != null) {
					//	if(paramsData.All(item => item.targetType == MemberData.TargetType.Values)) {
					//		List<object> pObj = new List<object>();
					//		foreach(var p in paramsData) {
					//			if(p.targetType == MemberData.TargetType.Values) {
					//				pObj.Add(p.Get());
					//			}
					//		}
					//		parameters.Add(new MemberData(ReflectionUtils.CreateInstance(ParseType(paramsSymbol).Get<Type>(), pObj.ToArray())));
					//	} else if(setting.CanCreateNode) {
					//		MultipurposeMember memberInvoke = new MultipurposeMember() { target = new MemberData(paramsSymbol.Type.Name + paramsSymbol.Name, ParseType(paramsSymbol).Get<Type>(), MemberData.TargetType.Constructor), parameters = paramsData != null ? paramsData.ToArray() : null };
					//		uNodeEditorUtility.UpdateMultipurposeMember(memberInvoke);
					//		var node = CreateComponent<MultipurposeNode>("ObjectCreation", setting.parent.transform);
					//		node.target = memberInvoke;
					//		parameters.Add(new MemberData(node, MemberData.TargetType.ValueNode));
					//	} else {
					//		throw new Exception("Couldn't handle arguments in method: " + symbol.ToDisplayString());
					//	}
					//}
					parameterValues = parameters;
					if(!isSource) {
						if(symbol.MethodKind == MethodKind.ReducedExtension) {
							List<TypeData> typeDatas = new List<TypeData>();
							typeDatas.Add(MemberDataUtility.GetTypeData(parameters[0].type, iData.genericArguments != null ? iData.genericArguments.Select(it => it.name).ToArray() : null));
							foreach(var p in symbol.Parameters) {
								typeDatas.Add(GetTypeData(p, !isSource, iData.genericArguments != null ? iData.genericArguments.Select(it => it.name).ToArray() : null));
							}
							iData.parameters = typeDatas.ToArray();
						}
						else {
							iData.parameters = symbol.Parameters.Select(item => GetTypeData(item, !isSource, iData.genericArguments != null ?
							iData.genericArguments.Select(it => it.name).ToArray() : null)).ToArray();
						}
						member.Items[member.Items.Length - 1] = iData;
					}
					members.Add(member);
				}
			}
			else if(expression is BaseObjectCreationExpressionSyntax) {//Constructor
				List<MemberData> parameters = null;
				var ex = expression as BaseObjectCreationExpressionSyntax;
				var symbol = setting.model.GetSymbolInfo(ex).Symbol as IMethodSymbol;
				if(symbol == null) {
					var sym = setting.model.GetSymbolInfo(ex);
					if(sym.CandidateReason == CandidateReason.OverloadResolutionFailure) {
						if(sym.CandidateSymbols.Length > 0) {
							foreach(var s in sym.CandidateSymbols) {
								if(s is IMethodSymbol sm) {
									if(symbol == null) {
										symbol = sm;
										continue;
									}
									if(sm.Parameters.Length == ex.ArgumentList.Arguments.Count) {
										symbol = sm;
									}
								}
							}
						}
					}
					if(symbol == null) //Throw error if the symbol cannot be resolved or null
						throw new Exception($"Symbol not found for syntax: {expression} reason: {sym.CandidateReason}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
				}
				MemberData member = new MemberData(symbol.ContainingType.Name + symbol.Name, ParseType(symbol.ContainingType), MemberData.TargetType.Constructor);
				if(member != null) {
					if(symbol.MethodKind != MethodKind.Constructor) {
						throw new Exception("Couldn't handle expression:" + expression + $"\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
					}
					member.isStatic = true;
					if(parameters == null) {
						parameters = new List<MemberData>();
					}
					List<MemberData> paramsData = null;
					IParameterSymbol paramsSymbol = null;
					int parameterLength = symbol.Parameters.Length;
					if(ex.ArgumentList != null) {
						for(int x = 0; x < ex.ArgumentList.Arguments.Count; x++) {
							var param = ex.ArgumentList.Arguments[x];
							if(x + 1 >= parameterLength) {
								if(paramsSymbol == null && symbol.Parameters[parameterLength - 1].IsParams) {
									paramsSymbol = symbol.Parameters[parameterLength - 1];
								}
								var s = GetSymbol(param.Expression, setting.model);
								if(s != null && paramsSymbol != null && !ParseType(s.ContainingType).type.IsArray) {
									if(paramsData == null) {
										paramsData = new List<MemberData>();
									}
									MemberData p;
									if(TryParseExpression(param.Expression, setting, out p)) {
										paramsData.Add(p);
									}
									else {
										throw new Exception("Couldn't handle argument:" + param + $"\nSyntax Type: {param?.GetType()}\nOn Script: {parserData.name}");
									}
									continue;
								}
							}
							MemberData m;
							if(TryParseExpression(param.Expression, setting, out m)) {
								parameters.Add(m);
							}
							else {
								throw new Exception("Couldn't handle argument:" + param + $"\nSyntax Type: {param?.GetType()}\nOn Script: {parserData.name}");
							}
						}
						if(paramsData != null) {//Handle params
							if(parameters.All(item => item.targetType == MemberData.TargetType.Values)) {
								Type type = ParseType(paramsSymbol);
								if(AllowDirrectEdit(type) || setting.parent == null) {
									List<object> pObj = new List<object>();
									foreach(var p in parameters) {
										if(p.targetType == MemberData.TargetType.Values) {
											pObj.Add(p.Get(null));
										}
									}
									parameters.Add(new MemberData(ReflectionUtils.CreateInstance(ParseType(paramsSymbol), pObj.ToArray())));
								}
								else {
									throw null;
									//MultipurposeMember memberInvoke = new MultipurposeMember() { 
									//	target = new MemberData(
									//		paramsSymbol.Type.Name + paramsSymbol.Name, 
									//		ParseType(paramsSymbol).Get<Type>(), 
									//		MemberData.TargetType.Constructor), 
									//	parameters = parameters != null ? parameters.ToArray() : null 
									//};
									//MemberDataUtility.UpdateMultipurposeMember(memberInvoke);
									//var node = CreateComponent<MultipurposeNode>("ObjectCreation", setting.parent.transform, setting.root);
									//node.target = memberInvoke;
									//parameters.Add(new MemberData(node, MemberData.TargetType.ValueNode));
								}
							}
							else if(setting.CanCreateNode) {
								throw null;
								//MultipurposeMember memberInvoke = new MultipurposeMember() { 
								//	target = new MemberData(
								//		paramsSymbol.Type.Name + paramsSymbol.Name, 
								//		ParseType(paramsSymbol).Get<Type>(), 
								//		MemberData.TargetType.Constructor), 
								//	parameters = parameters != null ? parameters.ToArray() : null 
								//};
								//MemberDataUtility.UpdateMultipurposeMember(memberInvoke);
								//var node = CreateComponent<MultipurposeNode>("ObjectCreation", setting.parent.transform, setting.root);
								//node.target = memberInvoke;
								//parameters.Add(new MemberData(node, MemberData.TargetType.ValueNode));
							}
							else {
								throw new Exception("Couldn't handle arguments in constructor: " + symbol.ToDisplayString() + $"\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
							}
						}
					}
					List<ParameterValueData> namedParameters = null;
					if(ex.Initializer != null) {
						if(setting.parent != null) {
							namedParameters = new List<ParameterValueData>();
							int index = 0;
							foreach(var param in ex.Initializer.Expressions) {
								string paramName = "Element" + index;
								if(param is AssignmentExpressionSyntax) {
									var paramEx = param as AssignmentExpressionSyntax;
									paramName = GetSymbol(paramEx.Left, setting.model).Name;
									namedParameters.Add(new ParameterValueData(paramName, ParseType(param, setting.model), ParseExpression(paramEx.Right, setting)));
								}
								else {
									if(param is InitializerExpressionSyntax initializer) {
										if(initializer.Kind() == SyntaxKind.ComplexElementInitializerExpression) {
											List<(MemberData, SerializedType)> elementValues = new List<(MemberData, SerializedType)>();
											foreach(var elementEx in initializer.Expressions) {
												var elementValue = ParseExpression(elementEx, setting);
												elementValues.Add((elementValue, ParseType(elementEx, setting.model)));
											}
											namedParameters.Add(new ParameterValueData(paramName, typeof(ParsedElementInitializer), new ParsedElementInitializer() {
												value = elementValues
											}));
											index++;
											continue;
										}
										else if(initializer.Kind() == SyntaxKind.CollectionInitializerExpression || initializer.Kind() == SyntaxKind.ComplexElementInitializerExpression) {
											Debug.LogWarning("Skipping parsing initializer: " + param.ToFullString() + $"\nOn Script: {parserData.name}\nAt line: {GetLinePosition(initializer)}");
											continue;
										}
									}
									namedParameters.Add(new ParameterValueData(paramName, ParseType(param, setting.model), ParseExpression(param, setting)));
									index++;
								}
							}
							if(namedParameters.Count > 0) {
								initializers = namedParameters;
							}
						}
						else {
							Debug.Log("Skipping parsing Initializer in constructor: " + symbol.ToDisplayString());
						}
					}
					parameterValues = parameters;
					MemberData.ItemData iData = new MemberData.ItemData(symbol.Name);
					iData.parameters = symbol.Parameters.Select(item => GetTypeData(item, null)).ToArray();
					member.Items[member.Items.Length - 1] = iData;
					Type sType = ParseType(symbol.ContainingType);
					if(parameters.Count == 0 && (AllowDirrectEdit(sType) || setting.parent == null)) {
						var value = new MemberData(ReflectionUtils.CreateInstance(sType));
						if(value.targetType != MemberData.TargetType.Null) {
							members.Add(value);
						}
						else {
							members.Add(member);
						}
					}
					else {
						if((namedParameters == null || namedParameters.Count == 0) &&
							parameters.All(item => item.targetType == MemberData.TargetType.Values) &&
							(AllowDirrectEdit(sType) || setting.parent == null)) {
							List<object> pObj = new List<object>();
							foreach(var p in parameters) {
								pObj.Add(p.Get(null));
							}
							var value = new MemberData(ReflectionUtils.CreateInstance(sType, pObj.ToArray()));
							if(value.targetType != MemberData.TargetType.Null) {
								members.Add(value);
							}
							else {
								members.Add(member);
							}
						}
						else {
							members.Add(member);
						}
					}
				}
			}
			else if(expression is ArrayCreationExpressionSyntax) {//Array Creation
				var ex = expression as ArrayCreationExpressionSyntax;
				var symbol = GetTypeSymbol(ex, setting.model);
				List<object> elements = new List<object>();
				List<ParameterValueData> namedParameters = null;
				if(ex.Initializer != null) {
					bool valid = true;
					try {
						foreach(var element in ex.Initializer.Expressions) {
							var value = ParseExpression(element, new ParserSetting(setting) { parent = null });
							if(value == null || value.IsTargetingValue == false) {
								valid = false;
								break;
							}
							elements.Add(value.Get(null));
						}
					}
					catch {
						valid = false;
					}
					if(!valid) {
						if(setting.parent != null) {
							namedParameters = new List<ParameterValueData>();
							int index = 0;
							foreach(var param in ex.Initializer.Expressions) {
								string paramName = "Element" + index;
								if(param is AssignmentExpressionSyntax) {
									var paramEx = param as AssignmentExpressionSyntax;
									paramName = GetSymbol(paramEx.Left, setting.model).Name;
									namedParameters.Add(new ParameterValueData(paramName, ParseType(param, setting.model), ParseExpression(paramEx.Right, setting)));
								}
								else {
									namedParameters.Add(new ParameterValueData(paramName, ParseType(param, setting.model), ParseExpression(param, setting)));
									index++;
								}
							}
							if(namedParameters.Count > 0) {
								if(setting.CanCreateNode) {
									initializers = namedParameters;
								}
								else {
									elements.AddRange(namedParameters.Select(p => (p.value as MemberData).Get(null)));
								}
							}
						}
						else {
							Debug.Log("Skipping parsing Initializer in array creation: " + symbol.ToDisplayString());
						}
					}
				}
				MemberData member = new MemberData(ReflectionUtils.CreateInstance(ParseType(symbol), elements.ToArray()));
				if(member != null) {
					members.Add(member);
				}
			}
			else if(expression is InitializerExpressionSyntax && expression.Kind() == SyntaxKind.ArrayInitializerExpression) {//Array Creation
				var ex = expression as InitializerExpressionSyntax;
				var symbol = GetTypeSymbol(ex, setting.model);
				List<object> elements = new List<object>();
				List<ParameterValueData> namedParameters = null;
				{
					bool valid = true;
					try {
						foreach(var element in ex.Expressions) {
							var value = ParseExpression(element, new ParserSetting(setting) { parent = null });
							if(value == null || value.IsTargetingValue == false) {
								valid = false;
								break;
							}
							elements.Add(value.Get(null));
						}
					}
					catch {
						valid = false;
					}
					if(!valid) {
						if(setting.parent != null) {
							namedParameters = new List<ParameterValueData>();
							int index = 0;
							foreach(var param in ex.Expressions) {
								string paramName = "Element" + index;
								if(param is AssignmentExpressionSyntax) {
									var paramEx = param as AssignmentExpressionSyntax;
									paramName = GetSymbol(paramEx.Left, setting.model).Name;
									namedParameters.Add(new ParameterValueData(paramName, ParseType(param, setting.model), ParseExpression(paramEx.Right, setting)));
								}
								else {
									namedParameters.Add(new ParameterValueData(paramName, ParseType(param, setting.model), ParseExpression(param, setting)));
									index++;
								}
							}
							if(namedParameters.Count > 0) {
								initializers = namedParameters;
							}
						}
						else {
							Debug.Log("Skipping parsing Initializer in array creation: " + ex);
						}
					}
				}
				MemberData member = new MemberData(ReflectionUtils.CreateInstance(ParseType(symbol), elements.ToArray()));
				if(member != null) {
					members.Add(member);
				}
			}
			else if(expression is ElementAccessExpressionSyntax) {
				List<MemberData> parameters = null;
				var ex = expression as ElementAccessExpressionSyntax;
				if(ex.Expression is MemberAccessExpressionSyntax or NameSyntax) {
					var m = GetMembersDataFromExpression(ex.Expression, setting, out parameters, out initializers);
					if(m != null && m.Count > 0) {
						members.AddRange(m);
						//members.RemoveAt(members.Count - 1);
					}
				}
				var propertySymbol = GetSymbol(ex, setting.model) as IPropertySymbol;
				MemberData member;
				string name = null;
				if(!setting.isSet) {//For get
					if(propertySymbol != null) {
						name = members.Count > 0 ? propertySymbol.GetMethod.Name : propertySymbol.ContainingType.Name.Add(".") + propertySymbol.GetMethod.Name;
						member = new MemberData(name,
							ParseType(propertySymbol.ContainingType),
							MemberData.TargetType.Method,
							ParseType(propertySymbol.Type));
					}
					else {//Array
						name = /*members.Count > 0 ? "Get" :*/ ParseType(GetTypeSymbol(ex.Expression, setting.model)).type.Name.Add(".") + "Get";
						member = new MemberData(name,
							ParseType(GetTypeSymbol(ex.Expression, setting.model)),
							MemberData.TargetType.Method,
							ParseType(GetTypeSymbol(ex, setting.model)));
					}
				}
				else {//For set
					if(propertySymbol != null) {
						name = members.Count > 0 ? propertySymbol.SetMethod.Name : propertySymbol.ContainingType.Name.Add(".") + propertySymbol.SetMethod.Name;
						member = new MemberData(name,
							ParseType(propertySymbol.ContainingType),
							MemberData.TargetType.Method,
							typeof(void));
					}
					else {//Array
						name = /*members.Count > 0 ? "Set" :*/ ParseType(GetTypeSymbol(ex.Expression, setting.model)).type.Name.Add(".") + "Set";
						member = new MemberData(name,
							ParseType(GetTypeSymbol(ex.Expression, setting.model)),
							MemberData.TargetType.Method,
							typeof(void));
					}
				}
				if(propertySymbol != null) {
					member.instance = ParseExpression(ex.Expression, setting);
				}
				if(member != null) {
					MemberData.ItemData iData = new MemberData.ItemData();
					if(parameters == null) {
						parameters = new List<MemberData>();
					}
					List<MemberData> paramsData = null;
					IParameterSymbol paramsSymbol = null;
					if(propertySymbol != null) {
						iData.name = propertySymbol.Name;
						if(propertySymbol.IsIndexer) {
							iData.name = null;
						}
						int parameterLength = propertySymbol.Parameters.Length;
						for(int x = 0; x < ex.ArgumentList.Arguments.Count; x++) {
							var param = ex.ArgumentList.Arguments[x];
							if(x + 1 >= parameterLength) {//Handle params keyword
								if(paramsSymbol == null && propertySymbol.Parameters[parameterLength - 1].IsParams) {
									paramsSymbol = propertySymbol.Parameters[parameterLength - 1];
								}
								var typeSymbol = GetTypeSymbol(param.Expression, setting.model);
								if(typeSymbol != null && paramsSymbol != null && !ParseType(typeSymbol).type.IsArray) {
									if(paramsData == null) {
										paramsData = new List<MemberData>();
									}
									MemberData p;
									if(TryParseExpression(param.Expression, setting, out p)) {
										paramsData.Add(p);
									}
									else {
										throw new Exception("Couldn't handle argument:" + param + $"\nSyntax Type: {param?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
									}
									continue;
								}
							}
							MemberData m;
							if(TryParseExpression(param.Expression, setting, out m)) {
								parameters.Add(m);
							}
							else {
								throw new Exception("Couldn't handle argument:" + param + $"\nSyntax Type: {param?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
							}
						}
					}
					else {//Array
						iData.name = setting.isSet ? "Set" : "Get";
						int parameterLength = ex.ArgumentList.Arguments.Count;
						for(int x = 0; x < ex.ArgumentList.Arguments.Count; x++) {
							var param = ex.ArgumentList.Arguments[x];
							MemberData m;
							if(TryParseExpression(param.Expression, setting, out m)) {
								parameters.Add(m);
							}
							else {
								throw new Exception("Couldn't handle argument:" + param + $"\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
							}
						}
					}
					if(paramsData != null) {
						if(parameters.All(item => item.targetType == MemberData.TargetType.Values)) {
							List<object> pObj = new List<object>();
							foreach(var p in parameters) {
								if(p.targetType == MemberData.TargetType.Values) {
									pObj.Add(p.Get(null));
								}
							}
							parameters.Add(new MemberData(ReflectionUtils.CreateInstance(ParseType(paramsSymbol), pObj.ToArray())));
						}
						else if(setting.CanCreateNode) {
							throw null;
							//MemberData mData = null;
							//var ctors = ParseType(paramsSymbol).startType.GetConstructors(MemberData.flags);
							//foreach(var ctor in ctors) {
							//	var mParameters = ctor.GetParameters();
							//	if(mParameters.Length == parameters.Count) {
							//		if(mData == null) {
							//			mData = new MemberData(ctor);
							//		}
							//		else {
							//			bool valid = true;
							//			for(int i=0;i<parameters.Count;i++) {
							//				if(parameters[i].type != mParameters[i].ParameterType) {
							//					valid = false;
							//					break;
							//				}
							//			}
							//			if(valid) {
							//				mData = new MemberData(ctor);
							//			}
							//		}
							//	}
							//}
							//var node = CreateMultipurposeNode("ObjectCreation", setting.parent, mData, parameters);
							//parameters.Add(MemberData.CreateFromValue(new UPortRef(node.@out)));
						}
						else {
							throw new Exception("Couldn't handle arguments in expression: " + ex + $"\nSyntax Type: {ex?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
						}
					}
					parameterValues = parameters;
					List<TypeData> typeDatas = new List<TypeData>();
					if(propertySymbol != null) {
						typeDatas = propertySymbol.Parameters.Select(item => GetTypeData(item, null)).ToList();
					}
					else {//Array
						foreach(var arg in ex.ArgumentList.Arguments) {
							typeDatas.Add(GetTypeData(GetTypeSymbol(arg.Expression, setting.model)));
						}
					}
					if(setting.isSet) {
						typeDatas.Add(GetTypeData(GetTypeSymbol(ex, setting.model)));
					}
					iData.parameters = typeDatas.ToArray();
					if(iData.name == null) {
						iData.name = member.Items[member.Items.Length - 1].name;
					}
					member.Items[member.Items.Length - 1] = iData;
					members.Add(member);
				}
			}
			else if(expression is MemberAccessExpressionSyntax) {
				var ex = expression as MemberAccessExpressionSyntax;
				List<MemberData> parameters = null;
				var symbol = GetSymbol(ex.Expression, setting.model);
				List<MemberData> member;
				if(symbol != null && symbol.Kind == SymbolKind.Namespace) {
					member = null;
				}
				else {
					member = GetMembersDataFromExpression(ex.Expression, setting, out parameters, out initializers);
				}
				var name = ParseExpression(ex.Name, setting.model, setting.parent);
				if(name != null && name.targetType == MemberData.TargetType.Type) {
					//Nested Type
					return new List<MemberData>() { name };
				}
				//var targetSymbol = GetSymbol(ex.Expression, setting.model);
				//if(targetSymbol != null) {
				//	if(targetSymbol is IParameterSymbol) {
				//		Debug.Log(member[0].targetType);
				//		var owner = GetSymbolOwner(targetSymbol);
				//		if(owner != null) {
				//			MultipurposeMember memberInvoke = new MultipurposeMember() { target = member[0], parameters = parameters != null ? parameters.ToArray() : null };
				//			uNodeEditorUtility.UpdateMultipurposeMember(memberInvoke);
				//			var node = CreateComponent<MultipurposeNode>(targetSymbol.Name, setting.parent.transform);
				//			node.target = memberInvoke;
				//			parameters = null;
				//			member = null;
				//			name.instance = new MemberData(node, MemberData.TargetType.ValueNode);
				//		}
				//	}
				//}
				if(member != null && member.Count > 0) {
					members.AddRange(member);
				}
				members.Add(name);
				if(parameters != null) {
					parameterValues = parameters;
				}
			}
			else {
				MemberData member;
				if(TryParseExpression(expression, setting, out member, false)) {
					members.Add(member);
				}
				else {
					throw new Exception("Couldn't handle expression:" + expression + "\n" + expression.GetType() + $"\nSyntax Type: {expression?.GetType()}\nOn Script: {parserData.name}\nAt line: {GetLinePosition(expression)}");
				}
			}
			return members;
		}
		#endregion

		private static string GetLinePosition(CSharpSyntaxNode syntax) {
			if(syntax == null)
				return null;
			var location = syntax.GetLocation();
			if(location != null) {
				var span = location.GetLineSpan();
				var line = span.Span.Start.Line + 1;
				var column = span.Span.Start.Character + 1;
				return $"({line}-{column})";
			}
			return null;
		}
	}

	#region Classes
	/// <summary>
	/// The parser result data.
	/// </summary>
	public sealed class ParserResult {
		/// <summary>
		/// The node result
		/// </summary>
		public Node node;
		/// <summary>
		/// Callback for next node
		/// </summary>
		public Action<Node> next;
	}

	public sealed class ParsedElementValue {
		public object[] values;
	}

	public sealed class ParsedElementInitializer {
		public List<(MemberData value, SerializedType type)> value;
	}

	/// <summary>
	/// The parser data.
	/// </summary>
	public sealed class ParserData {
		/// <summary>
		/// The semantic model
		/// </summary>
		public SemanticModel model;
		/// <summary>
		/// The uNodeBase object
		/// </summary>
		public IGraph root => parent.graphContainer;
		/// <summary>
		/// The parent of node if any
		/// </summary>
		public UGraphElement parent;

		public bool CanCreateNode {
			get {
				if(parent != null) {
					return parent is NodeContainer || (parent is NodeObject nodeObject && nodeObject.node is ISuperNode);
				}
				return false;
			}
		}

		/// <summary>
		/// The previous parser result
		/// </summary>
		public ParserResult previousResult;

		#region Constructors
		public ParserData() {

		}

		public ParserData(ParserSetting setting) {
			model = setting.model;
			parent = setting.parent;
		}
		#endregion
	}

	/// <summary>
	/// The parser settings
	/// </summary>
	public struct ParserSetting {
		/// <summary>
		/// The semantic model
		/// </summary>
		public SemanticModel model;
		/// <summary>
		/// The uNodeBase object
		/// </summary>
		public IGraph root => parent.graphContainer;
		/// <summary>
		/// The parent node if any
		/// </summary>
		public UGraphElement parent;

		/// <summary>
		/// Is currently is Set
		/// </summary>
		public bool isSet;

		public bool CanCreateNode {
			get {
				if(parent != null) {
					return parent is NodeContainer || (parent is NodeObject nodeObject && nodeObject.node is ISuperNode);
				}
				return false;
			}
		}

		#region Constructors
		public ParserSetting(SemanticModel model, UGraphElement parent, bool isSet = false) {
			this.model = model;
			this.parent = parent;
			this.isSet = isSet;
		}

		public ParserSetting(ParserData parser, bool isSet = false) {
			this.model = parser.model;
			this.parent = parser.parent;
			this.isSet = isSet;
		}

		public ParserSetting(ParserSetting parser, bool isSet = false) {
			this.model = parser.model;
			this.parent = parser.parent;
			this.isSet = isSet;
		}
		#endregion
	}
	#endregion
}