using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

namespace MaxyGames.UNode.Editors {
	class ParserGraphManipulator : GraphManipulator {
		private const string KEY_StartParse = "//[START_PARSE]";
		private const string KEY_EndParse = "//[END_PARSE]";

		public override bool IsValid(string action) {
			return action is nameof(ContextMenuForGraph) or nameof(ContextMenuForGraphCanvas);
		}

		public override IEnumerable<ContextMenuItem> ContextMenuForGraph(Vector2 mousePosition) {
			if(graph is IClassGraph cls && cls.IsInterface == false || graph is IClassDefinition) {
				yield return new ContextMenuItem("Insert c# code to parse", evt => {
					string text = string.Empty;
					Vector2 scrollPos = default;
					bool parseClass = false;
					ActionWindow.Show(() => {
						scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
						text = EditorGUILayout.TextArea(text, GUILayout.ExpandHeight(true));
						EditorGUILayout.EndScrollView();
						if(graphData.RootOwner is IScriptGraph) {
							parseClass = EditorGUILayout.Toggle("Check to insert c# class", parseClass);
						}
						if(parseClass) {
							EditorGUILayout.HelpBox("Only insert full declaration of c# class, struct, interface, or enum", MessageType.Info);
						}
						else {
							EditorGUILayout.HelpBox("Only insert c# variable, properties, or methods", MessageType.Info);
						}
						if(GUILayout.Button("Insert")) {
							if(parseClass) {
								InsertCodeToScriptGraph(graphData.RootOwner as IScriptGraph, text);
							}
							else {
								InsertCodeToGraph(graph, text);
							}
							graphEditor.Refresh();
						}
					});
				});


			}
			yield break;
		}

		public override IEnumerable<ContextMenuItem> ContextMenuForGraphCanvas(Vector2 mousePosition) {
			if(graph is IClassGraph cls && cls.IsInterface == false || graph is IClassDefinition) {
				yield return new ContextMenuItem("Insert c# code to parse", evt => {
					string text = string.Empty;
					Vector2 scrollPos = default;
					ActionWindow.Show(() => {
						scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
						text = EditorGUILayout.TextArea(text, GUILayout.ExpandHeight(true));
						EditorGUILayout.EndScrollView();
						EditorGUILayout.HelpBox("Only insert c# statement or expression", MessageType.Info);
						if(GUILayout.Button("Insert")) {
							InsertCodeToGraphCanvas(graph, graphData.currentCanvas, mousePosition, text);
							graphEditor.Refresh();
						}
					});
				});
			}
			yield break;
		}

		static void InsertCodeToGraphCanvas(IGraph graph, UGraphElement parent, Vector2 mousePosition, string code) {
			string className = null;
			var func = parent.GetObjectInParent<BaseFunction>();
			var script = GenerationUtility.GenerateCSharpScriptForIdentifier(graph as UnityEngine.Object, onSuccess: data => {
				var (_, cls) = data.classes.First();
				className = cls.name;

				string method;
				if(func != null) {
					method = $"{CG.Type(func.ReturnType())} M_ParseMe({string.Join(", ", func.parameters.Select(p => new CG.MPData(p).GenerateCode()))}) " + "{\n" + code.AddTabAfterNewLine(false) + "\n}";
				}
				else {
					method = "void M_ParseMe() {\n" + code.AddTabAfterNewLine(false) + "\n}";
				}

				cls.RegisterAdditionalContent(method.AddFirst($"\n{KEY_StartParse}\n\n\n").Add($"\n\n\n{KEY_EndParse}\n"));
				data.BuildScript();
			});
			var generatedScript = script.ToScript(out var informations);
			Debug.Log("parsing script: \n" + generatedScript);

			var lines = generatedScript.Split('\n');
			int startLine = 0;
			int endLine = 0;
			for(int i = 0; i < lines.Length; i++) {
				if(lines[i].Contains(KEY_StartParse, System.StringComparison.Ordinal)) {
					startLine = i;
				}
				if(lines[i].Contains(KEY_EndParse, System.StringComparison.Ordinal)) {
					endLine = i;
					break;
				}
			}
			uNodeEditorUtility.RegisterUndo(graph);

			CSharpParser.option_UseBlockSystem = false;
			var scriptGraph = CSharpParser.StartParse(generatedScript, "parsed");
			var data = CSharpParser.parserData;
			foreach(var r in data.roots) {
				if(r.graph.GetGraphName() == className) {
					Dictionary<UGraphElement, UGraphElement> declarationMap = new();
					List<UGraphElement> duplicateMap = new();

					foreach(var (syntax, element) in r.variables) {
						var linePosition = syntax.GetLocation().GetLineSpan().Span.Start.Line;
						if(linePosition >= startLine && linePosition <= endLine) {
							//elements.Add(element);
						}
						else {
							var original = graph.GetVariable(element.name);
							if(original != null) {
								declarationMap[element] = original;
							}
						}
					}
					foreach(var (syntax, element) in r.properties) {
						var linePosition = syntax.GetLocation().GetLineSpan().Span.Start.Line;
						if(linePosition >= startLine && linePosition <= endLine) {
							//elements.Add(element);
						}
						else {
							var original = graph.GetProperty(element.name);
							if(original != null) {
								declarationMap[element] = original;
							}
						}
					}
					foreach(var (syntax, element) in r.methods) {
						var linePosition = syntax.GetLocation().GetLineSpan().Span.Start.Line;
						if(linePosition >= startLine && linePosition <= endLine) {
							if(func != null) {
								declarationMap[element] = func;
							}
							var childs = element.GetObjectsInChildren().Where(e => e is not NodeObject node || node.node is not BaseEntryNode);
							GraphUtility.CopyPaste.Copy(childs.ToArray());
							var graphElements = GraphUtility.CopyPaste.Paste(parent);
							foreach(var ele in graphElements) {
								if(ele is NodeObject node) {
									node.position.position += mousePosition;
								}
								duplicateMap.Add(ele);
							}
						}
						else {
							var original = graph.GetFunction(element.name, element.ParameterTypes);
							if(original != null) {
								declarationMap[element] = original;
							}
						}
					}

					RedirectReferences(r.graph, graph, declarationMap, duplicateMap);
				}
			}

			Debug.Log("Success parsing script to graph");
		}

		static void InsertCodeToGraph(IGraph graph, string code) {
			string className = null;
			var script = GenerationUtility.GenerateCSharpScriptForIdentifier(graph as UnityEngine.Object, onSuccess: data => {
				var (_, cls) = data.classes.First();
				className = cls.name;
				cls.RegisterAdditionalContent(code.AddFirst($"\n{KEY_StartParse}\n\n\n").Add($"\n\n\n{KEY_EndParse}\n"));
				data.BuildScript();
			});
			var generatedScript = script.ToScript(out var informations);
			Debug.Log("parsing script: \n" + generatedScript);

			var lines = generatedScript.Split('\n');
			int startLine = 0;
			int endLine = 0;
			for(int i = 0; i < lines.Length; i++) {
				if(lines[i].Contains(KEY_StartParse, System.StringComparison.Ordinal)) {
					startLine = i;
				}
				if(lines[i].Contains(KEY_EndParse, System.StringComparison.Ordinal)) {
					endLine = i;
					break;
				}
			}

			uNodeEditorUtility.RegisterUndo(graph);

			CSharpParser.option_UseBlockSystem = false;
			var scriptGraph = CSharpParser.StartParse(generatedScript, "parsed");
			var data = CSharpParser.parserData;
			foreach(var r in data.roots) {
				if(r.graph.GetGraphName() == className) {
					Dictionary<UGraphElement, UGraphElement> declarationMap = new();
					List<UGraphElement> duplicateMap = new();
					List<UGraphElement> elements = new();

					foreach(var (syntax, element) in r.variables) {
						var linePosition = syntax.GetLocation().GetLineSpan().Span.Start.Line;
						if(linePosition >= startLine && linePosition <= endLine) {
							elements.Add(element);
						}
						else {
							var original = graph.GetVariable(element.name);
							if(original != null) {
								declarationMap[element] = original;
							}
						}
					}
					foreach(var (syntax, element) in r.properties) {
						var linePosition = syntax.GetLocation().GetLineSpan().Span.Start.Line;
						if(linePosition >= startLine && linePosition <= endLine) {
							elements.Add(element);
						}
						else {
							var original = graph.GetProperty(element.name);
							if(original != null) {
								declarationMap[element] = original;
							}
						}
					}
					foreach(var (syntax, element) in r.methods) {
						var linePosition = syntax.GetLocation().GetLineSpan().Span.Start.Line;
						if(linePosition >= startLine && linePosition <= endLine) {
							elements.Add(element);
						}
						else {
							var original = graph.GetFunction(element.name, element.ParameterTypes);
							if(original != null) {
								declarationMap[element] = original;
							}
						}
					}
					foreach(var element in elements) {
						if(element is Variable) {
							GraphUtility.CopyPaste.Copy(element);
							var ele = GraphUtility.CopyPaste.Paste(graph.GraphData.variableContainer)[0];
							declarationMap[element] = ele;
							duplicateMap.Add(ele);
							if(graph is not IScriptGraph) {
								var variable = ele as Variable;
								if(variable.modifier.Static) {
									variable.modifier.Static = false;
								}
							}
						}
						else if(element is Property) {
							GraphUtility.CopyPaste.Copy(element);
							var ele = GraphUtility.CopyPaste.Paste(graph.GraphData.propertyContainer)[0];
							declarationMap[element] = ele;
							duplicateMap.Add(ele);
							if(graph is not IScriptGraph) {
								var variable = ele as Property;
								if(variable.modifier.Static) {
									variable.modifier.Static = false;
								}
							}
						}
						else if(element is Function) {
							GraphUtility.CopyPaste.Copy(element);
							var ele = GraphUtility.CopyPaste.Paste(graph.GraphData.functionContainer)[0];
							declarationMap[element] = ele;
							duplicateMap.Add(ele);
							if(graph is not IScriptGraph) {
								var variable = ele as Function;
								if(variable.modifier.Static) {
									variable.modifier.Static = false;
								}
							}
						}
					}
					RedirectReferences(r.graph, graph, declarationMap, duplicateMap);
				}
			}

			Debug.Log("Success parsing script to graph");
		}

		static void InsertCodeToScriptGraph(IScriptGraph graph, string code) {
			var lines = code.Split('\n');
			var generatedScript = "#pragma warning disable\n";
			if(lines.Length > 0) {
				var ns = graph.GetUsingNamespaces();
				for(int i = 0; i < lines.Length; i++) {
					foreach(var n in ns) {
						if(lines[i].Contains("using " + n)) {
							ns.Remove(n);
							break;
						}
					}
				}
				generatedScript += CG.Flow(string.Join("\n", ns.Select(n => "using " + n + ";")), code.AddLineInFirst());
			}
			Debug.Log("parsing script: \n" + generatedScript);

			uNodeEditorUtility.RegisterUndo(graph as UnityEngine.Object);

			CSharpParser.option_UseBlockSystem = false;
			var scriptGraph = CSharpParser.StartParse(generatedScript, "parsed");
			var data = CSharpParser.parserData;
			foreach(var r in data.roots) {
				if(r.scriptGraphType is UnityEngine.Object uobj) {
					graph.TypeList.AddType(uobj as IScriptGraphType, graph);
					AssetDatabase.AddObjectToAsset(uobj, graph as UnityEngine.Object);
				}
			}
			Debug.Log("Success parsing script to graph");
		}

		private static void RedirectReferences(IGraph from, IGraph to, Dictionary<UGraphElement, UGraphElement> declarationMap, IEnumerable<UGraphElement> elementToAnalyze) {
			void Analize(UGraphElement element) {
				bool changed = false;
				GraphUtility.Analizer.AnalizeObject(element, obj => {
					if(obj is MemberData mData) {
						if(mData.IsTargetingUNode) {
							for(int i = 0; i < mData.Items.Length; i++) {
								var item = mData.Items[i];
								if(item != null && item.reference != null) {
									var refVal = item.GetReferenceValue();
									if(refVal is UGraphElement graphElement) {
										if(declarationMap.TryGetValue(graphElement, out var validElement)) {
											var reference = item.reference;
											if(reference is UReference) {
												(reference as UReference).SetGraphElement(validElement);
											}
											else {
												item.reference = BaseReference.FromValue(validElement);
											}
											changed = true;
										}
									}
									else if(item.reference is UReference) {
										var uref = item.reference as UReference;
										graphElement = uref.GetGraphElement();
										if(declarationMap.TryGetValue(graphElement, out var validElement)) {
											uref.SetGraphElement(validElement);
											changed = true;
										}
									}
								}
							}
						}
						else if(mData.targetType == MemberData.TargetType.Self) {
							var self = mData.Get(null);
							if(self == from) {
								mData.CopyFrom(MemberData.This(to));
								changed = true;
							}
						}
					}
					else if(obj is UReference) {
						var uref = obj as UReference;
						var graphElement = uref.GetGraphElement();
						if(declarationMap.TryGetValue(graphElement, out var validElement)) {
							uref.SetGraphElement(validElement);
							changed = true;
						}
					}
					return false;
				});
				if(element is NodeObject nodeObject) {
					var references = nodeObject.serializedData.References;
					for(int i = 0; i < references.Count; i++) {
						if(references[i] is MemberData mData) {
							if(mData.Items != null) {
								foreach(var item in mData.Items) {
									if(item.reference?.ReferenceValue is UGraphElement referenceElement) {
										if(declarationMap.TryGetValue(referenceElement, out var validReference)) {
											var reference = item.reference;
											if(reference is UReference) {
												(reference as UReference).SetGraphElement(validReference);
											}
											else {
												item.reference = BaseReference.FromValue(validReference);
											}
											changed = true;
										}
									}
								}
							}
						}
					}
					if(changed) {
						nodeObject.node = null;
						(nodeObject as ISerializationCallbackReceiver).OnAfterDeserialize();
					}
				}
			}
			foreach(var element in elementToAnalyze) {
				Analize(element);
				var childs = element.GetObjectsInChildren(true);
				foreach(var child in childs) {
					Analize(child);
				}
			}
		}
	}
}