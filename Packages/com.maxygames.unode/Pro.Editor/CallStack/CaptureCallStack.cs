//#define UNODE_DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Linq;
using System.Reflection;

namespace MaxyGames.UNode.Editors {

	public class CaptureCallStack : EditorWindow {
		#region Instances
		static CaptureCallStack window;

		//[MenuItem("Tools/uNode/Call Stack")]
		public static CaptureCallStack ShowWindow() {
			window = (CaptureCallStack)GetWindow(typeof(CaptureCallStack));
			window.titleContent = new GUIContent("Call Stack");
			window.minSize = new Vector2(250, 250);
			window.Show();
			return window;
		}

		[Serializable]
		public class FrameData {
			public string file;
			public int line, column;

			public int unityObjectID;
			public string debugDisplay;

			public string typeName;
			public string method;
			public string[] parameters;
			public string[] gparameters;

			public WeakReference instance;

			public Type type { get; private set; }
			private UnityEngine.Object m_unityObject;
			public UnityEngine.Object unityObject {
				get {
					if(unityObjectID != 0 && m_unityObject == null) {
						m_unityObject = EditorUtility.InstanceIDToObject(unityObjectID) ?? m_unityObject;
						if(m_unityObject is RuntimeBehaviour behaviour) {
							var components = behaviour.GetComponents<IRuntimeClassContainer>();
							foreach(var c in components) {
								if(object.ReferenceEquals(c.RuntimeClass, m_unityObject)) {
									m_unityObject = c as UnityEngine.Object;
									unityObjectID = m_unityObject.GetHashCode();
									break;
								}
							}
						}
					}
					return m_unityObject;
				}
			}
			public UGraphElement element { get; private set; }
			public object debugTarget { get; private set; }

			public bool IsInSource => string.IsNullOrEmpty(file) == false && line > 0;

			internal void Initialize() {
				type = typeName.ToType(false);
				if(unityObject != null) { }
				if(GraphException.ParseMessage(debugDisplay, out _, out var element, out var debugId)) {
					if(element != null) {
						this.element = element;
						debugTarget = GraphDebug.GetDebugObject(debugId);
					}
				}
				if(this.element == null && string.IsNullOrEmpty(file) == false) {
					this.element = uNodeEditor.GetElementFromScript(file, line, column);
				}
			}
		}

		[Serializable]
		public class Data {
			public Texture icon;
			public string label;

			public FrameData frame = new();
		}
		[SerializeField]
		List<Data> datas = new();
		ListView listView;

		private void OnEnable() {
			window = this;
			foreach(var d in datas) {
				d.frame.Initialize();
			}
			listView = new ListView(datas,
				makeItem: () => {
					return new VisualElement();
				},
				bindItem: (parent, index) => {
					var data = datas[index];
					parent.tooltip = string.Empty;

					var ve = new VisualElement();
					//ve.StretchToParentSize();
					ve.style.paddingLeft = 5;
					parent.Add(ve);

					//var image = new Image() {
					//	name = "element-icon",
					//};
					//image.image = data.icon;
					//ve.Add(image);
					var label = new Label();
					label.text = data.label;
					label.style.height = new Length(50, LengthUnit.Percent);
					label.style.borderTopWidth = 1;
					label.style.borderTopColor = new Color(0.3773585f, 0.3773585f, 0.3773585f);
					ve.Add(label);

					if(data.frame.element != null) {
						if(string.IsNullOrEmpty(data.frame.file) == false) {
							parent.tooltip = $"Script location: \n{data.frame.file}({data.frame.line}:{data.frame.column})";
						}
						label.style.unityFontStyleAndWeight = FontStyle.Bold;

						var subItem = new VisualElement();
						subItem.style.flexDirection = FlexDirection.Row;
						subItem.style.height = new Length(50, LengthUnit.Percent);
						subItem.style.unityFontStyleAndWeight = FontStyle.Bold;
						ve.Add(subItem);

						var paths = ErrorCheckWindow.GetElementPathWithIcon(data.frame.element, true, true);
						for(int i = 0; i < paths.Count; i++) {
							var path = paths[i];
							if(path.icon != null) {
								var img = new Image() {
									image = path.icon,
								};
								img.style.width = 16;
								img.style.height = 16;
								img.style.flexShrink = 0;
								subItem.Add(img);
							}
							var nm = path.name;
							if(i != paths.Count - 1) {
								nm += " »";
							}
							var lbl = new Label(nm) {
								enableRichText = true
							};
							subItem.Add(lbl);
						}
					}
					else if(string.IsNullOrEmpty(data.frame.file) == false) {
						var subItem = new VisualElement();
						subItem.style.flexDirection = FlexDirection.Row;
						subItem.style.height = new Length(50, LengthUnit.Percent);
						ve.Add(subItem);
						if(data.frame.type != null) {
							var img = new Image() {
								image = uNodeEditorUtility.GetTypeIcon(data.frame.type),
							};
							img.style.width = 16;
							img.style.height = 16;
							img.style.flexShrink = 0;
							subItem.Add(img);
						}
						var lbl = new Label($"{data.frame.file}({data.frame.line}:{data.frame.column})");
						subItem.Add(lbl);
					}
					else {
						label.style.height = new Length(100, LengthUnit.Percent);
						//label.style.unityTextAlign = TextAnchor.MiddleLeft;
					}

					ve.RegisterCallback<PointerDownEvent>(evt => {
						if(evt.button == 0 && evt.clickCount == 2) {
							if(data.frame.element != null) {
								uNodeEditor.Highlight(data.frame.element);
								if(data.frame.debugTarget != null) {
									if(uNodeEditor.window != null) {
										var graphData = uNodeEditor.window.graphData;
										if(graphData.debugAnyScript) {
											graphData.SetAutoDebugTarget(data.frame.debugTarget);
										}
									}
								}
								return;
							}
							if(data.frame.unityObject != null) {
								EditorGUIUtility.PingObject(data.frame.unityObject);
								return;
							}
							if(string.IsNullOrEmpty(data.frame.file) == false) {
								uNodeEditor.Highlight(data.frame.file, data.frame.line, data.frame.column);
								return;
							}
						}
						else if(evt.button == 1) {
							GenericMenu menu = new GenericMenu();
							if(data.frame.element != null) {
								menu.AddItem(new GUIContent("Open"), false, () => {
									uNodeEditor.Highlight(data.frame.element);
									if(data.frame.debugTarget != null) {
										if(uNodeEditor.window != null) {
											var graphData = uNodeEditor.window.graphData;
											if(graphData.debugAnyScript) {
												graphData.SetAutoDebugTarget(data.frame.debugTarget);
											}
										}
									}
								});
							}
							if(string.IsNullOrEmpty(data.frame.file) == false) {
								if(uNodeEditor.GetElementFromScript(data.frame.file, data.frame.line, data.frame.column)) {
									menu.AddItem(new GUIContent("Open Graph"), false, () => {
										uNodeEditor.Highlight(data.frame.file, data.frame.line, data.frame.column);
									});
								}
							}
							menu.AddItem(new GUIContent("Open Script"), false, () => {
								UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(data.frame.file, data.frame.line);
								//var method = type.GetMemberCached(data.frame.method);
								//if(method != null) {
								//	GraphUtility.GoToDefinition(method, data.frame.file);
								//}
								//else {
								//	GraphUtility.GoToDefinition(type, data.frame.file);
								//}
							});
							if(data.frame.unityObject != null) {
								menu.AddItem(new GUIContent("Ping: " + data.frame.unityObject.name), false, () => {
									EditorGUIUtility.PingObject(data.frame.unityObject);
								});
							}
							if(data.frame.instance != null) {
								var value = data.frame.instance.Target;
								if(value != null && value.Equals(null) == false) {
									var position = evt.position.ToScreenPoint();
									menu.AddItem(new GUIContent("Inspect Instance"), false, () => {
										UBind bind = null;
										if(value is Node) {
											value = (value as Node).nodeObject;
										}
										if(value is UnityEngine.Object uobj) {
											if(uobj == null) {
												Debug.Log("Trying to inspect null value, probably it has been destroyed or you're leaving playmode?");
												return;
											}
											bind = UBind.FromObject(value as UnityEngine.Object);
										}
										else if(value is UGraphElement element) {
											if(element == null) {
												Debug.Log("Trying to inspect null value, probably it has been destroyed or you're leaving playmode?");
												return;
											}
											bind = UBind.FromGraphElement(value as UGraphElement);
										}
										else {
											bind = new UBindValue(value);
										}
										ActionWindow.Show(
											() => {
												EditorGUILayout.LabelField(value.GetType().PrettyName(true), EditorStyles.centeredGreyMiniLabel);
												EditorGUI.BeginDisabledGroup(true);
												if(value is UGraphElement || value is UnityEngine.Object || value is UPort) {
													uNodeGUI.DrawReference(new GUIContent("Reference"), value, value.GetType());
												}
												UInspector.Draw(bind, label: new GUIContent("Value"), flags: BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic);
												//uNodeGUIUtility.EditValueLayouted(new GUIContent("value"), value, value.GetType());
												//uNodeGUIUtility.ShowFields(value, null, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
												EditorGUI.EndDisabledGroup();
											}).autoFocus = false;
										if(value is UnityEngine.Object unityObjet) {
											EditorGUIUtility.PingObject(unityObjet);
										}
									});
								}
							}
							if(menu.GetItemCount() > 0) {
								menu.ShowAsContext();
							}
							evt.StopPropagation();
						}
					});
				});
			listView.unbindItem += (VisualElement ve, int index) => {
				if(ve.childCount > 0) {
					ve.RemoveAt(0);
				}
			};
			listView.fixedItemHeight = 36;
			listView.selectionType = SelectionType.Single;
			listView.selectionChanged += SelectionChanged;
			listView.itemsChosen += SelectionChanged;
			listView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
			listView.horizontalScrollingEnabled = true;
			listView.style.flexGrow = 1;
			//var toolbar = new Toolbar();
			//toolbar.Add(new ToolbarButton() {
			//	text = "Refresh",
			//	clickable = new Clickable(() => {

			//	}),
			//});
			//toolbar.Add(new ToolbarSpacer() { flex = true });
			//rootVisualElement.Add(toolbar);
			rootVisualElement.Add(listView);
		}

		private void SelectionChanged(IEnumerable<object> enumerable) {

		}

		public void ChangeData(IEnumerable<Data> datas) {
			this.datas.Clear();
			this.datas.AddRange(datas);
			listView?.Rebuild();
			//Focus();
		}
		#endregion

		#region Statics
		static bool hasCreate;
		static CaptureCallStack() {
			GraphDebug.Breakpoint.onBreakPointHit += (_, _, _) => {
				if(hasCreate == false) {
					hasCreate = true;
					Capture();
				}
			};
		}

		[InitializeOnEnterPlayMode]
		static void DoResets() {
			hasCreate = false;
			EditorApplication.pauseStateChanged -= OnUnPause;
			EditorApplication.pauseStateChanged += OnUnPause;
		}

		private static void OnUnPause(PauseState obj) {
			if(obj == PauseState.Unpaused) {
				hasCreate = false;
			}
		}

		public static void Capture() {
			if(UnityEditor.Compilation.CompilationPipeline.codeOptimization == UnityEditor.Compilation.CodeOptimization.Release) {
				Debug.Log("Captuing call stack is limited when not in debug mode. Please enable it first to get fully working call stack ( can open graph, ping object, etc ), help url for how to switch to debug mode: <a href=\"https://docs.unity3d.com/Manual/managed-code-debugging.html#DebugInEditor\">https://docs.unity3d.com/Manual/managed-code-debugging.html</a>\nWithout debug mode is useful only for c# graphs or compiled graphs but without ability to ping object");
				CaptureWithoutDebug();
				return;
			}

			var path = string.Empty;
			var monoScript = uNodeEditorUtility.GetMonoScript(typeof(CaptureCallStack));
			if(monoScript != null) {
				path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoScript)) + "\\MonoDebugger\\uNodeMonoDebugger.dll";
				if(File.Exists(path) == false) {
					path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoScript)) + "\\MonoDebugger\\uNodeMonoDebugger.exe";
				}
			}
			if(File.Exists(path) == false) {
				return;
			}
#if UNODE_DEV || UNODE_DEBUG
			Debug.Log("Start capturing call stack: " + path);
#endif
			var outputPath = Path.GetFullPath("uNode3Data/callstack.txt");
			if(File.Exists(outputPath)) {
				File.Delete(outputPath);
			}
			Process process;
			if(Application.platform == RuntimePlatform.WindowsEditor) {
				if(path.EndsWith(".exe")) {
					process = new Process {
						StartInfo = new ProcessStartInfo {
							FileName = path,
							WindowStyle = ProcessWindowStyle.Hidden,
							Arguments = $"--threadId:{Thread.CurrentThread.ManagedThreadId} --output:\"{outputPath}\"",//--manualExit
							RedirectStandardOutput = false,
							UseShellExecute = true,
							CreateNoWindow = false
						}
					};
				}
				else {
					if(IsMonoInstalled() == false) {
						Debug.Log("Mono is not installed or not in PATH.\nPlease rename the uNodeMonoDebugger.dll extension from .dll to .exe or install Mono from https://www.mono-project.com/download/stable/\nWithout install mono, capturing call stack is limited, please install it for get full features.");
						CaptureWithoutDebug();
						return;
					}
					process = new Process {
						StartInfo = new ProcessStartInfo {
							FileName = FindMonoExecutable(),
							WindowStyle = ProcessWindowStyle.Hidden,
							Arguments = $"\"{path}\"  --threadId:{Thread.CurrentThread.ManagedThreadId} --output:\"{outputPath}\"",
							RedirectStandardOutput = false,
							UseShellExecute = true,
							CreateNoWindow = false
						}
					};
				}
			}
			else {//For Mac OS and Linux
				if(IsMonoInstalled() == false) {
					Debug.Log("Mono is not installed or not in PATH.\nPlease install Mono from https://www.mono-project.com/download/stable/\nWithout install mono, capturing call stack is limited, please install it for get full features.");
					CaptureWithoutDebug();
					return;
				}
				process = new Process {
					StartInfo = new ProcessStartInfo {
						FileName = FindMonoExecutable(),
						WindowStyle = ProcessWindowStyle.Hidden,
						Arguments = $"\"{path}\"  --threadId:{Thread.CurrentThread.ManagedThreadId} --output:\"{outputPath}\"",
						RedirectStandardOutput = false,
						UseShellExecute = true,
						CreateNoWindow = false
					}
				};
			}

			Stopwatch watch = new();
			watch.Start();
#if UNODE_DEV || UNODE_DEBUG
			StackTrace stackTrace = new StackTrace(true);
			Debug.Log("Capturing, call stack count: " + stackTrace.GetFrames().Length);
#endif

			process.Start();
			while(true) {
				if(File.Exists(outputPath)) {
					//process.CloseMainWindow();
					break;
				}
				if(watch.ElapsedMilliseconds > 3000) {
					Debug.LogError("Timeout on trying capturing call stack, probably there's attached debug from IDE or something else.\nRestarting Unity should fix it.");
					process.CloseMainWindow();
					registry.Clear();
					return;
				}
			}
			watch.Stop();
			uNodeThreadUtility.ExecuteAfterCondition(() => process.HasExited, () => {
				string[] texts = File.ReadAllLines(outputPath);
#if UNODE_DEV || UNODE_DEBUG
				Debug.Log("Capturing call stack completed. Total Elapsed ms: " + watch.ElapsedMilliseconds + "\nOutput:\n" + string.Join('\n', texts));
#else
				Debug.Log("Capturing call stack completed. Total Elapsed ms: " + watch.ElapsedMilliseconds);
#endif

				const string key = "@=";
				List<string> datas = new();
				foreach(var data in texts) {
					if(!string.IsNullOrEmpty(data)) {
						if(data.StartsWith(key)) {
							datas.Add(data);
						}
					}
				}
				if(datas.Count == 0) {
					registry.Clear();
					Debug.Log("No call stack captured. probably there's attached debug from IDE, connection timeout or something else. Restarting Unity should fix it.\n" + "Elapsed ms: " + watch.ElapsedMilliseconds);
					return;
				}
				else {
					ParseOutput(datas);
				}
				registry.Clear();
			});
		}

		static Dictionary<int, object> registry = new();
		static int nextId = 1;

		public static int RegisterInstance(object obj) {
			int id = Interlocked.Increment(ref nextId);
			registry[id] = obj;
			return id;
		}

		public static object ResolveInstance(int ptr) {
			registry.TryGetValue(ptr, out var obj);
			return obj;
		}

		private static void CaptureWithoutDebug() {
			StackTrace stackTraces = new StackTrace(true);
			var frames = stackTraces.GetFrames();
			List<Data> result = new(frames.Length);
			foreach(var s in frames) {
				var data = new Data();
				var method = s.GetMethod();
				var frame = data.frame;
				frame.file = s.GetFileName();
				frame.line = s.GetFileLineNumber() - 1;
				frame.column = s.GetFileColumnNumber() - 1;
				frame.typeName = method.DeclaringType.FullName;
				frame.method = method.Name;
				frame.parameters = method.GetParameters().Select(p => p.ParameterType.FullName).ToArray();
				frame.gparameters = method.GetGenericArguments().Select(p => p.FullName).ToArray();
				frame.Initialize();
				data.label = method.DeclaringType.PrettyName(true) + "." + EditorReflectionUtility.GetPrettyMethodName(method);
				result.Add(data);
			}

			//Remove unnescessary stack
			var index = result.FindLastIndex(d => d.frame.typeName.StartsWith("MaxyGames.UNode.Editors.CaptureCallStack")) + 1;
			if(index >= 0) {
				for(int i = 0; i < index + 1; i++) {
					if(result.Count == 0) break;
					if(i == index && result[0].frame.type != typeof(GraphDebug)) {
						continue;
					}
					result.RemoveAt(0);
				}
				while(result.Count > 0 && result[0].frame.type == typeof(GraphDebug)) {
					result.RemoveAt(0);
				}
			}

			var win = ShowWindow();
			win.ChangeData(result);
		}

		const string key_frame = "@=frame:";
		const string key_method = "@=method:";
		const string key_type = "@=type:";
		const string key_file = "@=file:";
		const string key_debugDisplay = "@=debugDisplay:";
		const string key_unityInstance = "@=unity:";
		const string key_this = "@=this:";
		static void ParseOutput(List<string> datas) {
			List<Data> result = new(32);

			for(int x = 0; x < datas.Count; x++) {
				if(datas[x].StartsWith(key_frame)) {
					int y;
					var data = new Data();
					result.Add(data);
					data.label = datas[x].Substring(datas[x].IndexOf(key_frame) + key_frame.Length);
					var frame = data.frame;
					for(y = x + 1; y < datas.Count; y++) {
						var s = datas[y];
						if(s.StartsWith(key_frame)) {
							break;
						}
						if(s.StartsWith(key_type)) {
							var str = s.Substring(s.IndexOf(key_type) + key_type.Length);
							frame.typeName = str;
						}
						if(s.StartsWith(key_file)) {
							var str = s.Substring(s.IndexOf(key_file) + key_file.Length);
							var strs = str.Split("=>");
							var path = strs[0];
							strs = strs[1].Split(':');
							var line = int.Parse(strs[0]);
							var column = int.Parse(strs[1]);
							frame.file = path;
							frame.line = line - 1;
							frame.column = column - 1;
						}
						if(s.StartsWith(key_debugDisplay)) {
							frame.debugDisplay = s.Substring(s.IndexOf(key_debugDisplay) + key_debugDisplay.Length);
						}
						if(s.StartsWith(key_unityInstance)) {
							frame.unityObjectID = int.Parse(s.Substring(s.IndexOf(key_unityInstance) + key_unityInstance.Length));
						}
						if(s.StartsWith(key_method)) {
							var str = s.Substring(s.IndexOf(key_method) + key_method.Length);
							var strs = str.Split('~');
							var name = strs[0];
							var gparameters = strs[1].Split(", ", StringSplitOptions.RemoveEmptyEntries);
							var parameters = strs[2].Split(", ", StringSplitOptions.RemoveEmptyEntries);
							frame.method = name;
							frame.parameters = parameters;
							frame.gparameters = gparameters;
							//Debug.Log($"Method: {name} has parameters: ({string.Join(',', parameters)}){parameters.Length} and ({string.Join(',', gparameters)}){gparameters.Length} generic parameters");
						}
						if(s.StartsWith(key_this)) {
							var str = s.Substring(s.IndexOf(key_this) + key_this.Length);
							var num = int.Parse(str);
							var val = ResolveInstance(num);
							if(val != null) {
								frame.instance = new(val);
							}
						}
					}
					frame.Initialize();
				}
			}

			//Remove unnescessary stack
			var index = result.FindLastIndex(d => d.frame.typeName.StartsWith("MaxyGames.UNode.Editors.CaptureCallStack")) + 1;
			if(index >= 0) {
				for(int i = 0; i < index + 1; i++) {
					if(result.Count == 0) break;
					result.RemoveAt(0);
				}
				while(result.Count > 0 && result[0].frame.type == typeof(GraphDebug)) {
					result.RemoveAt(0);
				}
			}

			var win = ShowWindow();
			win.ChangeData(result);
		}

		static string monoPath;
		static string FindMonoExecutable() {
			if(monoPath == null) {
				monoPath = string.Empty;
				if(IsMonoWorking("mono")) {
					monoPath = "mono";
					return monoPath;
				}
				string[] knowMonoPaths;
				if(Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer) {
					knowMonoPaths = new string[] {
						"/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono",
						"/usr/bin/mono",
						"/usr/local/bin/mono",
						"/opt/homebrew/bin/mono",
					};
				}
				else if(Application.platform == RuntimePlatform.LinuxEditor || Application.platform == RuntimePlatform.LinuxPlayer) {
					knowMonoPaths = new string[] {
						"/usr/bin/mono",
						"/usr/lib/mono/4.5/",
						"/usr/local/bin/mono",
					};
				}
				else {
					knowMonoPaths = new string[] {
						@"C:\Program Files\Mono\bin\mono.exe",
						@"C:\Program Files (x86)\Mono\bin\mono.exe",
					};
				}
				foreach(var path in knowMonoPaths) {
					if(File.Exists(path)) {
						if(IsMonoWorking(path)) {
							monoPath = path;
							return monoPath;
						}
					}
				}
			}
			return monoPath;
		}

		static bool IsMonoInstalled() {
			return string.IsNullOrEmpty(FindMonoExecutable()) == false;
		}

		static bool IsMonoWorking(string path) {
			try {
				var info = new ProcessStartInfo {
					FileName = path,
					Arguments = "--version",
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true,
				};
				using(var process = Process.Start(info)) {
					process.WaitForExit();
					return process.ExitCode == 0;
				}
			}
			catch { }
			return false;
		}
		#endregion
	}
}