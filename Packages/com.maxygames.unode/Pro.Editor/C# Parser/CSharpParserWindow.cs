using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace MaxyGames.UNode.Editors {
	public class CSharpParserWindow : EditorWindow {
		public class Data {
			public bool useBlockSystem;
			public Dictionary<string, bool> assembly = new Dictionary<string, bool>();
		}
		public static CSharpParserWindow window;
		[SerializeField]
		private UnityEngine.Object targetGO;
		[SerializeField]
		private MonoScript[] targetScripts = new MonoScript[1];
		private Vector2 scrollPos;
		[SerializeField]
		private int selected;
		[SerializeField]
		private string text, path;
		[SerializeField]
		private bool assemblyT;

		private static bool hasSetup;
		private static Assembly[] assemblies;
		public static Data data {
			get {
				Setup();
				return _data;
			}
		}

		private static Data _data;

		[MenuItem("Tools/uNode/C# Parser")]
		private static void Init() {
			Setup();
			window = (CSharpParserWindow)GetWindow(typeof(CSharpParserWindow), true);
			window.minSize = new Vector2(450, 250);
			window.autoRepaintOnSceneChange = true;
			window.wantsMouseMove = true;
			window.titleContent = new GUIContent("CSharpParser");
			window.Show();
			window.Focus();
		}

		static void Setup() {
			if(!hasSetup) {
				hasSetup = true;
				assemblies = AppDomain.CurrentDomain.GetAssemblies();
				List<Assembly> ass = new List<Assembly>();
				foreach(Assembly assembly in assemblies) {
					try {
						if(string.IsNullOrEmpty(assembly.Location))
							continue;
						ass.Add(assembly);
					}
					catch { continue; }
				}
				assemblies = ass.ToArray();
				_data = uNodeEditorUtility.LoadEditorData<Data>("ParserData");
				if(_data == null) {
					_data = new Data();
					SaveData();
				}
				else {
					if(_data.assembly == null) {
						_data.assembly = new Dictionary<string, bool>();
					}
				}
			}
		}

		static void SaveData() {
			uNodeEditorUtility.SaveEditorData(data, "ParserData");
		}

		void OnGUI() {
			if(!hasSetup) {
				Setup();
			}
			HandleKeyboard();
			selected = GUILayout.Toolbar(selected, new string[] { "From MonoScript", "From Source", "From File", "Settings" });
			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
			if(selected == 0) {
				uNodeGUIUtility.EditValueLayouted(new GUIContent("Scripts"), targetScripts, typeof(MonoScript[]), (value) => {
					targetScripts = value as MonoScript[];
				}, new uNodeUtility.EditValueSettings() { attributes = new object[] { new ObjectTypeAttribute(typeof(MonoScript)) } });
				EditorGUILayout.HelpBox("Note: files with the same name will be overwritten", MessageType.Info);
				if(GUILayout.Button("Batch Add Scripts From Folder")) {
					var path = uNodeEditorUtility.GetRelativePath(EditorUtility.OpenFolderPanel("Select Scripts Folder", "Assets", ""));
					if(!string.IsNullOrEmpty(path)) {
						if(Directory.Exists(path)) {
							var dir = new DirectoryInfo(path);
							var files = dir.GetFiles("*", SearchOption.AllDirectories);
							List<MonoScript> scripts = new List<MonoScript>(targetScripts);
							foreach(var file in files) {
								if(file.Extension == ".cs") {
									var mono = AssetDatabase.LoadAssetAtPath<MonoScript>(uNodeEditorUtility.GetRelativePath(file.FullName));
									if(mono != null) {
										scripts.Add(mono);
									}
								}
							}
							targetScripts = scripts.Distinct().ToArray();
						}
					}
				}
				//targetGO = EditorGUILayout.ObjectField("Script", targetGO, typeof(MonoScript), false);
			}
			else if(selected == 1) {
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.PrefixLabel("Script");
				text = EditorGUILayout.TextArea(text, GUILayout.ExpandHeight(true));
				EditorGUILayout.EndHorizontal();
			}
			else if(selected == 2) {
				path = EditorGUILayout.TextField("Path", path);
				if(GUILayout.Button("Load File")) {
					path = EditorUtility.OpenFilePanel("Load Script File", "", "cs");
				}
			}
			else if(selected == 3) {
				var useBlockSystem = EditorGUILayout.Toggle("Use Block System", data.useBlockSystem);
				if(useBlockSystem != data.useBlockSystem) {
					data.useBlockSystem = useBlockSystem;
					SaveData();
				}
				assemblyT = EditorGUILayout.Foldout(assemblyT, "Excluded Assembly");
				if(assemblyT) {
					EditorGUILayout.BeginVertical("Box");
					foreach(Assembly assembly in assemblies) {
						string assemblyName = assembly.GetName().Name;
						bool flag = data.assembly.ContainsKey(assemblyName);
						bool oldValue = flag;
						EditorGUILayout.BeginHorizontal();
						GUILayout.Space(20);
						flag = GUILayout.Toggle(flag, assemblyName);
						EditorGUILayout.EndHorizontal();
						if(oldValue != flag) {
							if(flag) {
								data.assembly.Add(assemblyName, false);
							}
							else {
								data.assembly.Remove(assemblyName);
							}
							uNodeEditorUtility.SaveEditorData(data, "ParserData");
							break;
						}
					}
				}
				if(GUILayout.Button(new GUIContent("Reset"))) {
					_data = new Data();
					SaveData();
				}
			}
			EditorGUILayout.EndScrollView();
			if(selected != 3) {

				EditorGUILayout.HelpBox("Don't use parser to mix between c# and graph ex: manually edit generated c# script and re-parsing it, otherwise the graph will be rebuild and thus this is not very recommended.\nIf you want to mix c# and uNode, don't parse the edited script and just use it along with uNode", MessageType.Warning);
				EditorGUILayout.HelpBox("Tips: After parsing success, use Place Fit feature to auto arrage nodes by 'right clicking on the graph canvas > place fit nodes'", MessageType.Info);

				if(GUILayout.Button(new GUIContent("Parse"))) {
					CSharpParser.option_UseBlockSystem = data.useBlockSystem;

					List<Assembly> ass = new List<Assembly>();
					foreach(Assembly assembly in assemblies) {
						try {
							if(string.IsNullOrEmpty(assembly.Location))
								continue;
							if(!data.assembly.ContainsKey(assembly.GetName().Name)) {
								ass.Add(assembly);
							}
						}
						catch { continue; }
					}
					RoslynUtility.assemblies = ass;

					string script = null;
					if(selected == 0) {
						//var assemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies();
						//Dictionary<UnityEditor.Compilation.Assembly, List<Microsoft.CodeAnalysis.SyntaxTree>> assemblyMap = new Dictionary<UnityEditor.Compilation.Assembly, List<Microsoft.CodeAnalysis.SyntaxTree>>();
						ParseMonoScripts(targetScripts);
						return;
					}
					else if(selected == 1) {
						if(!string.IsNullOrEmpty(text)) {
							script = text;
						}
						else {
							throw new NullReferenceException("Can't parse, Script is null or empty");
						}
					}
					else if(selected == 2) {
						if(!string.IsNullOrEmpty(path)) {
							script = System.IO.File.ReadAllText(path);
						}
						else {
							throw new NullReferenceException("Can't parse, Path is null or empty");
						}
					}
					{//Do parse and save graph
						var asset = CSharpParser.StartParse(script, "ParsedGraph");
						RoslynUtility.assemblies = null;

						var savePath = EditorUtility.SaveFilePanelInProject("Save graph asset", "", "asset", "");
						if(!string.IsNullOrEmpty(savePath)) {
							AssetDatabase.CreateAsset(asset, savePath);
							foreach(var subAsset in asset.TypeList) {
								if(subAsset != null) {
									AssetDatabase.AddObjectToAsset(subAsset, asset);
								}
							}
						}
						AssetDatabase.SaveAssets();
					}
				}
			}
		}

		void HandleKeyboard() {
			Event current = Event.current;
			if(current.type == EventType.KeyDown) {
				if(current.keyCode == KeyCode.Escape) {
					Close();
					return;
				}
			}
			if(current.type == EventType.KeyDown) {
				Focus();
			}
		}

		public static void ParseMonoScripts(params MonoScript[] scripts) {
			bool hasParse = false;
			int progress = 0;
			int successCount = 0;
			//var references = RoslynUtility.GetSyntaxTreesFromFiles(targetScripts.Where(t => t != null).Select(t => AssetDatabase.GetAssetPath(t)));
			foreach(var s in scripts) {
				if(s != null) {
					try {
						var scriptPath = AssetDatabase.GetAssetPath(s);
						EditorUtility.DisplayProgressBar("Parsing Scripts", scriptPath, (float)progress / (float)scripts.Length);
						//var assembly = assemblies.FirstOrDefault(a => a.sourceFiles.Contains(scriptPath));
						//if(!assemblyMap.TryGetValue(assembly, out var references)) {
						//	references = RoslynUtility.GetSyntaxTreesFromFiles(assembly.sourceFiles.Where(s => s != scriptPath), assembly.defines);
						//	assemblyMap[assembly] = references;
						//}
						hasParse = true;
						progress++;
						var asset = CSharpParser.StartParse(s.text, s.name/*, references: references*/);
						Debug.Log("Successfull parsing script: " + scriptPath);
						successCount++;
						AssetDatabase.CreateAsset(asset, AssetDatabase.GetAssetPath(s).RemoveLast(s.name.Length + 3) + asset.name + ".asset");
						foreach(var subAsset in asset.TypeList) {
							if(subAsset != null) {
								AssetDatabase.AddObjectToAsset(subAsset, asset);
							}
						}
						if(scripts.Count(m => m != null) == 1) {
							uNodeEditor.Open(asset);
						}
					}
					catch(Exception ex) {
						Debug.LogException(ex, s);
					}
				}
			}
			EditorUtility.ClearProgressBar();
			Debug.Log("Parsed Scripts: " + successCount + " of " + scripts.Length + "\nUse 'Place Fit Nodes' features to auto place fit the nodes.");
			if(!hasParse) {
				throw new NullReferenceException("Can't parse, Scripts is null");
			}
			AssetDatabase.SaveAssets();
			uNodeEditor.RefreshEditor(true);
		}


		[MenuItem("Assets/Parse Script", true)]
		private static bool Validate() {
			return Selection.objects.All(o => o is MonoScript);
		}

		[MenuItem("Assets/Parse Script", false, 1001)]
		private static void DoProcess() {
			var scripts = Selection.objects.Select(o => o as MonoScript);
			ParseMonoScripts(scripts.ToArray());
		}
	}

	[CustomEditor(typeof(MonoScript), true)]
	class MonoScriptEditor : Editor {
		[NonSerialized]
		private GUIStyle m_TextStyle;
		[NonSerialized]
		private string currentText, highlightedText;
		[NonSerialized]
		System.Threading.Thread highlightThread;

		public override void OnInspectorGUI() {
			if(m_TextStyle == null) {
				m_TextStyle = new GUIStyle("ScriptText");
				m_TextStyle.richText = true;
			}

			var asset = target as MonoScript;
			if(GUILayout.Button("Parse Script to Graph")) {
				CSharpParserWindow.ParseMonoScripts(asset);
			}

			if(targets.Length == 1) {
				var assetPath = AssetDatabase.GetAssetPath(target);
				var assemblyName = UnityEditor.Compilation.CompilationPipeline.GetAssemblyNameFromScriptPath(assetPath);
				// assemblyName is null for MonoScript's inside assemblies.
				if(assemblyName != null) {
					GUILayout.Label("Assembly Information", EditorStyles.boldLabel);

					EditorGUILayout.LabelField("Filename", assemblyName);

					var assemblyDefinitionFile = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(assetPath);

					if(assemblyDefinitionFile != null) {
						var assemblyDefinitionFileAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assemblyDefinitionFile);

						using(new EditorGUI.DisabledScope(true)) {
							EditorGUILayout.ObjectField("Definition File", assemblyDefinitionFileAsset, typeof(TextAsset), false);
						}
					}

					EditorGUILayout.Space();
				}
			}

			var text = asset.text;
			var rect = uNodeGUIUtility.GetRect(Mathf.Clamp(text.Split('\n').Length, 0, 300));
			rect.width += rect.x;
			rect.x = 0;

			if(currentText != text) {
				currentText = text;
				highlightedText = SyntaxHighlighter.CSharpSyntaxHighlighter.GetRichText(text, false);
				if(highlightThread != null && highlightThread.IsAlive) {
					highlightThread.Abort();
				}
				highlightThread = uNodeThreadUtility.CreateThread(() => {
					highlightedText = SyntaxHighlighter.CSharpSyntaxHighlighter.GetRichTextAsync(text);
					highlightThread = null;
					uNodeThreadUtility.Queue(() => {
						if(this != null) {
							Repaint();
						}
					});
				});
				highlightThread.Start();
			}

			EditorGUI.SelectableLabel(rect, highlightedText, m_TextStyle);
		}
	}
}